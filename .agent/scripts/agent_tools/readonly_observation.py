#!/usr/bin/env python3
"""Read-only implementation bodies for .agent/tools observation entrypoints.

Python3 stdlib only. These helpers emit JSON to stdout and do not write files.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_DIR = REPO_ROOT / ".agent" / "scripts"
sys.path.insert(0, str(SCRIPT_DIR / "lib"))
import minimal_yaml as yaml  # noqa: E402

BOUNDARY = {
    "read_only": True,
    "writes_repo_files": False,
    "authority_boundary": "observation_only_not_ssot_authority_not_proof_completion_not_completion_judgment_not_semantic_audit_judgment_not_implemented_status",
    "prohibited_judgments": ["implemented", "partial", "not_started", "proof_passed", "semantic_completion"],
}


def _json(data) -> int:
    sys.stdout.write(json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
    return 0


def _reject_mutation_args(argv: list[str]) -> int | None:
    mutation_args = {"--output", "-o", "--write", "--save", "--in-place", "--apply", "--update-seed", "--update-manifest", "--db-url", "--connection-string"}
    for arg in argv:
        opt = arg.split("=", 1)[0]
        if opt in mutation_args:
            sys.stderr.write(f"FAIL: mutation/file-write option is not exposed by .agent/tools: {opt}\n")
            return 2
    return None


def directory_map(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only repo directory surface observation as JSON stdout.")
    parser.add_argument("--root", default="docs")
    parser.add_argument("--depth", type=int, default=None)
    args = parser.parse_args(argv)

    emitter_path = SCRIPT_DIR / "emit-directory-tree-json.py"
    spec = importlib.util.spec_from_file_location("emit_directory_tree_json", emitter_path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    # Reuse the emitter body without exposing its --output file-write option.
    return mod.main(["--root", args.root, *( ["--depth", str(args.depth)] if args.depth is not None else [] )])


def _walk_matches(obj, query: str, cur_path: str = ""):
    q = query.lower()
    matches = []
    if isinstance(obj, dict):
        for key, value in obj.items():
            path = f"{cur_path}.{key}" if cur_path else str(key)
            key_hit = q in str(key).lower()
            val_hit = not isinstance(value, (dict, list)) and q in str(value).lower()
            if key_hit or val_hit:
                matches.append({"path": path, "key": key, "value": value})
            matches.extend(_walk_matches(value, query, path))
    elif isinstance(obj, list):
        for idx, value in enumerate(obj):
            path = f"{cur_path}[{idx}]"
            if not isinstance(value, (dict, list)) and q in str(value).lower():
                matches.append({"path": path, "value": value})
            matches.extend(_walk_matches(value, query, path))
    return matches


def ssot_map_query(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only .agent/docs/ssot-map.yaml observation query.")
    parser.add_argument("--query", default=None, help="Case-insensitive text query over keys/scalars.")
    parser.add_argument("--surface", default=None, help="Filter mapping entries by change_surfaces text.")
    parser.add_argument("--path", default=None, help="Filter mapping entries by required/supporting doc or protocol path text.")
    args = parser.parse_args(argv)

    source = ".agent/docs/ssot-map.yaml"
    data = yaml.load_file(str(REPO_ROOT / source))
    entries = yaml.arr(data.get("mapping")) if isinstance(data, dict) else []
    filtered = []
    for entry in entries:
        text = json.dumps(entry, ensure_ascii=False).lower()
        if args.query and args.query.lower() not in text:
            continue
        if args.surface and args.surface.lower() not in " ".join(str(x) for x in yaml.arr(entry.get("change_surfaces"))).lower():
            continue
        if args.path:
            paths = yaml.arr(entry.get("required_docs")) + yaml.arr(entry.get("supporting_docs")) + yaml.arr(entry.get("protocols"))
            if args.path.lower() not in " ".join(str(x) for x in paths).lower():
                continue
        filtered.append(entry)
    return _json({
        "tool": "ssot-map-query",
        "source_file": source,
        "query": {"query": args.query, "surface": args.surface, "path": args.path},
        "boundary": BOUNDARY,
        "matched_count": len(filtered),
        "matched_entries": filtered,
    })


def proof_surface_map(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only proof surface graph observation.")
    parser.add_argument("--all", action="store_true", help="Return all observed proof entries.")
    parser.add_argument("--proof-id", default=None, help="Filter by proof_id.")
    parser.add_argument("--bundle-id", default=None, help="Filter by reverse lookup bundle_id.")
    args = parser.parse_args(argv)
    if not args.all and not args.proof_id and not args.bundle_id:
        args.all = True

    manifest_source = "docs/design/test-proof-manifest-ssot.yaml"
    bundles_source = ".agent/docs/test-bundles.yaml"
    manifest = yaml.load_file(str(REPO_ROOT / manifest_source))
    bundles = yaml.load_file(str(REPO_ROOT / bundles_source))
    proofs = yaml.arr(manifest.get("chronological_scope_index")) if isinstance(manifest, dict) else []
    reverse = yaml.arr(bundles.get("reverse_lookup")) if isinstance(bundles, dict) else []

    bundle_by_proof = {}
    for bundle in reverse:
        if args.bundle_id and bundle.get("bundle_id") != args.bundle_id:
            continue
        for proof_ref in yaml.arr(bundle.get("proof_refs")):
            bundle_by_proof.setdefault(proof_ref, []).append(bundle)

    observed = []
    for proof in proofs:
        pid = proof.get("proof_id")
        if args.proof_id and pid != args.proof_id:
            continue
        if args.bundle_id and pid not in bundle_by_proof:
            continue
        observed.append({
            "proof_id": pid,
            "proof_order": proof.get("proof_order"),
            "scope_phase": proof.get("scope_phase"),
            "domain": proof.get("domain"),
            "proof_type": proof.get("proof_type"),
            "ssot_refs": yaml.arr(proof.get("ssot_refs")),
            "implementation_files": yaml.arr(proof.get("implementation_files")),
            "test_files": yaml.arr(proof.get("test_files")),
            "runner_surfaces": yaml.arr(proof.get("runner_surfaces")),
            "evidence_inputs": yaml.arr(proof.get("evidence_inputs")),
            "workflow_jobs": yaml.arr(proof.get("workflow_jobs")),
            "does_not_prove": yaml.arr(proof.get("does_not_prove")),
            "reverse_lookup_bundles": bundle_by_proof.get(pid, []),
        })
    return _json({
        "tool": "proof-surface-map",
        "source_files": [manifest_source, bundles_source],
        "query": {"all": args.all, "proof_id": args.proof_id, "bundle_id": args.bundle_id},
        "boundary": BOUNDARY | {"runner_execution": False, "proof_completion_judgment": False},
        "observed_count": len(observed),
        "proof_surfaces": observed,
    })


TOPOLOGY_SEED_DISCUSSION_BOUNDARY = BOUNDARY | {
    "discussion_draft_only": True,
    "seed_adoption_judgment": False,
    "proof_completion_judgment": False,
    "db_connection": False,
    "external_api_connection": False,
    "ai_api_call": False,
    "writes_seed_sql": False,
    "writes_manifest": False,
    "writes_ssot": False,
    "adoption_requires_separate_human_judgment_or_change": True,
}

TOPOLOGY_SEED_REFERENCE_FILES = [
    "docs/design/db-schema.yaml",
    "docs/design/runtime-orchestration-ssot.yaml",
    "docs/design/pipeline-continuity-ssot.yaml",
    "docs/design/admin-console-workflow-ssot.yaml",
    "docs/design/test-proof-manifest-ssot.yaml",
    "db/topology_tables.sql",
    "db/manifest_tables.sql",
    "db/seed_empty.sql",
    "db/demo_seed.sql",
    "backend/runtime/SeedRuntime.cs",
    "backend/repository/SeedJsonRepository.cs",
    "backend/repository/SeedImportApplyRepository.cs",
    "backend/repository/ManifestRepository.cs",
    "backend/repository/ManifestTopologyValidator.cs",
    "backend/runtime/ManifestDispatcher.cs",
]


def _file_observation(path: str):
    full = REPO_ROOT / path
    if not full.is_file():
        return {"path": path, "exists": False, "size_bytes": None}
    return {"path": path, "exists": True, "size_bytes": full.stat().st_size}


def topology_seed_discussion(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only topology seed discussion JSON draft helper.")
    sub = parser.add_subparsers(dest="mode", required=True)
    sub.add_parser("inspect", help="Emit seed discussion JSON templates from read-only repo observation.")
    build_parser = sub.add_parser("build", help="Build candidate discussion JSON from answers JSON.")
    build_parser.add_argument("--answers", required=True, help="Path to answers JSON to read; never modified.")
    args = parser.parse_args(argv)

    if args.mode == "inspect":
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "inspect",
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "observed_reference_files": [_file_observation(path) for path in TOPOLOGY_SEED_REFERENCE_FILES],
            "manifest_template": {
                "manifest_id": "discussion-only-placeholder",
                "role": "<role-axis>",
                "target": "<target-axis>",
                "layer": "<layer-axis>",
                "action": "<action-axis>",
                "runtime_destination": "<registered-runtime-destination>",
                "topology_entries": [],
                "notes": "Draft only; not a manifest adoption decision and not written by this tool.",
            },
            "seed_candidate_template": {
                "candidate_id": "<discussion-candidate-id>",
                "candidate_scope": "<what seed surface is being discussed>",
                "source_files_considered": TOPOLOGY_SEED_REFERENCE_FILES,
                "proposed_records": [],
                "non_goals": [
                    "Do not generate or save seed SQL from this tool.",
                    "Do not reverse existing implementation into authority.",
                ],
            },
            "ssot_link_template": {
                "db_schema_refs": ["docs/design/db-schema.yaml"],
                "runtime_refs": ["docs/design/runtime-orchestration-ssot.yaml", "docs/design/pipeline-continuity-ssot.yaml"],
                "admin_refs": ["docs/design/admin-console-workflow-ssot.yaml"],
                "proof_refs": ["docs/design/test-proof-manifest-ssot.yaml"],
                "authority_note": "Links are discussion references only; output is not SSOT authority.",
            },
            "topology_axis_template": {
                "hub": "<hub meaning space>",
                "topology_manifest": "<manifest grouping>",
                "physical_table": "<physical table binding if applicable>",
                "runtime_axis": {"role": "<role>", "target": "<target>", "layer": "<layer>", "action": "<action>"},
                "ui_axis": {"package": "<package>", "layout": "<layout>", "wiring": "<wiring>"},
            },
            "implementation_reference_template": {
                "seed_runtime": "backend/runtime/SeedRuntime.cs",
                "seed_json_repository": "backend/repository/SeedJsonRepository.cs",
                "seed_import_apply_repository": "backend/repository/SeedImportApplyRepository.cs",
                "manifest_repository": "backend/repository/ManifestRepository.cs",
                "manifest_validator": "backend/repository/ManifestTopologyValidator.cs",
                "manifest_dispatcher": "backend/runtime/ManifestDispatcher.cs",
            },
            "evidence_reference_template": {
                "seed_files": ["db/seed_empty.sql", "db/demo_seed.sql"],
                "schema_files": ["db/topology_tables.sql", "db/manifest_tables.sql"],
                "does_not_prove": [
                    "Seed presence alone does not prove runtime behavior.",
                    "Discussion output does not prove proof completion.",
                ],
            },
            "questions": [
                "Which topology meaning space is the proposed seed candidate meant to discuss?",
                "Which SSOT references constrain the candidate?",
                "Which manifest axes are required before any adoption decision?",
                "Which existing implementation files are reference-only context?",
                "What remains unresolved before a human adoption decision or separate change?",
            ],
        })

    answers_path = Path(args.answers)
    if not answers_path.is_absolute():
        answers_path = Path.cwd() / answers_path
    try:
        with open(answers_path, "r", encoding="utf-8") as f:
            answers = json.load(f)
    except OSError as exc:
        sys.stderr.write(f"FAIL: cannot read answers JSON: {exc}\n")
        return 1
    except json.JSONDecodeError as exc:
        sys.stderr.write(f"FAIL: invalid answers JSON: {exc}\n")
        return 1

    unresolved = []
    if isinstance(answers, dict):
        unresolved = [q for q in answers.get("unresolved_questions", []) if str(q).strip()]
        candidate_input = answers.get("candidate_seed_json", answers.get("candidate", {}))
        discussion_notes = answers.get("discussion_notes", [])
    else:
        candidate_input = {}
        discussion_notes = []
        unresolved = ["answers JSON root is not an object"]

    return _json({
        "tool": "topology-seed-discussion",
        "mode": "build",
        "source_file": str(answers_path),
        "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
        "discussion_result": {
            "status": "discussion_draft",
            "notes": discussion_notes,
            "not_authority": [
                "Not SSOT authority.",
                "Not seed adoption judgment.",
                "Not proof completion.",
                "Not implemented / partial / not_started evidence.",
            ],
        },
        "candidate_seed_json": candidate_input,
        "unresolved_questions": unresolved,
        "adoption_boundary": {
            "requires_separate_human_judgment_or_change": True,
            "this_tool_writes_seed_sql": False,
            "this_tool_writes_manifest": False,
            "this_tool_writes_ssot": False,
            "this_tool_connects_to_db_or_api": False,
        },
    })


def main(argv=None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    if not argv:
        sys.stderr.write("FAIL: tool name required\n")
        return 2
    tool, rest = argv[0], argv[1:]
    if tool == "directory-map":
        return directory_map(rest)
    if tool == "ssot-map-query":
        return ssot_map_query(rest)
    if tool == "proof-surface-map":
        return proof_surface_map(rest)
    if tool == "topology-seed-discussion":
        return topology_seed_discussion(rest)
    sys.stderr.write(f"FAIL: unknown tool: {tool}\n")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
