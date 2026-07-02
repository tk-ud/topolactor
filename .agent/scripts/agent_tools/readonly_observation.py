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


TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID = "topology_seed_discussion_ssot_seed_structure_v1"

TOPOLOGY_SEED_DISCUSSION_BOUNDARY = BOUNDARY | {
    "discussion_draft_only": True,
    "candidate_seed_json_only": True,
    "seed_adoption_judgment": False,
    "proof_completion_judgment": False,
    "db_connection": False,
    "external_api_connection": False,
    "ai_api_call": False,
    "writes_seed_sql": False,
    "writes_manifest": False,
    "writes_ssot": False,
    "writes_tmp_json": False,
    "adoption_requires_separate_human_judgment_or_change": True,
}

QUESTION_SPACE_ORDER = [
    "sql_attention_observation",
    "topology_manifest_authoring",
    "admin_contents_authoring",
    "admin_ui_builder_authoring",
    "admin_manifests_navigation",
    "runtime_manifest_dispatch",
    "seed_runtime_import",
    "db_topology_wiring",
]

QUESTION_SPACE_META = {
    "sql_attention_observation": {
        "ssot_sources": ["docs/design/sql-attention-logs-ssot.yaml"],
        "validation_refs": ["db/sql_attention_logs_tables.sql", "backend/runtime/SeedRuntime.cs"],
        "question": "SQL Attention SSOT elements を seed discussion candidate に含めるか",
        "root": "sql_attention_observation",
    },
    "topology_manifest_authoring": {
        "ssot_sources": ["docs/design/db-schema.yaml", "docs/design/admin-console-workflow-ssot.yaml"],
        "validation_refs": ["db/topology_tables.sql", "db/manifest_tables.sql", "frontend/islands/ManifestsAdmin.tsx"],
        "question": "topology manifest / hub relation SSOT elements を seed discussion candidate に含めるか",
        "root": "topology_manifest_authoring",
    },
    "admin_contents_authoring": {
        "ssot_sources": ["docs/design/admin-console-workflow-ssot.yaml", "docs/design/db-schema.yaml"],
        "validation_refs": ["frontend/islands/ContentsScreenDesignPanel.tsx", "db/topology_tables.sql", "db/seed_empty.sql"],
        "question": "/admin/contents SSOT elements を seed discussion candidate に含めるか",
        "root": "admin_contents",
    },
    "admin_ui_builder_authoring": {
        "ssot_sources": ["docs/design/admin-console-workflow-ssot.yaml", "docs/design/db-schema.yaml"],
        "validation_refs": ["frontend/islands/UiBuilderAdmin.tsx", "db/topology_tables.sql"],
        "question": "/admin/ui-builder SSOT elements を seed discussion candidate に含めるか",
        "root": "admin_ui_builder",
    },
    "admin_manifests_navigation": {
        "ssot_sources": ["docs/design/admin-console-workflow-ssot.yaml", "docs/design/db-schema.yaml"],
        "validation_refs": ["frontend/islands/ManifestsAdmin.tsx", "frontend/islands/HubNavigationAdmin.tsx", "db/topology_tables.sql"],
        "question": "/admin/manifests navigation SSOT elements を seed discussion candidate に含めるか",
        "root": "admin_manifests",
    },
    "runtime_manifest_dispatch": {
        "ssot_sources": ["docs/design/runtime-orchestration-ssot.yaml"],
        "validation_refs": ["backend/runtime/SeedRuntime.cs", "db/manifest_tables.sql", "db/seed_empty.sql"],
        "question": "runtime manifest dispatch SSOT elements を seed discussion candidate に含めるか",
        "root": "runtime_manifest_dispatch",
    },
    "seed_runtime_import": {
        "ssot_sources": ["docs/design/runtime-orchestration-ssot.yaml"],
        "validation_refs": ["backend/runtime/SeedRuntime.cs", "backend/repository/SeedImportApplyRepository.cs", "db/seed_empty.sql"],
        "question": "SeedRuntime import boundary SSOT elements を seed discussion candidate に含めるか",
        "root": "seed_runtime_import",
    },
    "db_topology_wiring": {
        "ssot_sources": ["docs/design/db-schema.yaml", "docs/design/runtime-orchestration-ssot.yaml"],
        "validation_refs": ["db/topology_tables.sql", "db/manifest_tables.sql", "db/seed_empty.sql"],
        "question": "DB topology wiring SSOT elements を seed discussion candidate に含めるか",
        "root": "db_topology_wiring",
    },
}

SPACE_PATH_HINTS = {
    "admin_contents_authoring": ("content", "contents", "step", "screen", "entity", "relation", "table", "column", "aggregation", "search"),
    "admin_ui_builder_authoring": ("ui", "builder", "canvas", "component", "layout", "package", "wiring", "design", "css", "html"),
    "admin_manifests_navigation": ("manifest", "hub", "navigation", "relation", "sequence"),
    "topology_manifest_authoring": ("topology", "manifest", "hub", "relation"),
    "runtime_manifest_dispatch": ("runtime", "dispatch", "dispatcher", "manifest", "projection", "route"),
    "seed_runtime_import": ("seed", "import", "runtime", "validate", "preview", "fail", "fallback"),
    "db_topology_wiring": ("topology", "manifest", "hub", "physical", "binding", "wiring", "table"),
    "sql_attention_observation": ("sql", "attention", "logs", "current", "diff", "candidate", "evidence", "norm", "phase"),
}


def _ssot_scalar_preview(value):
    if isinstance(value, (dict, list)):
        return None
    return value


def _path_join(base: str, key: str) -> str:
    return f"{base}.{key}" if base else key


def _walk_ssot_elements(source_ref: str, obj, cur_path: str = ""):
    if isinstance(obj, dict):
        yield cur_path or "$", "object", obj
        for key in sorted(obj.keys(), key=str):
            yield from _walk_ssot_elements(source_ref, obj[key], _path_join(cur_path, str(key)))
    elif isinstance(obj, list):
        yield cur_path or "$", "list", obj
        for idx, value in enumerate(obj):
            yield from _walk_ssot_elements(source_ref, value, f"{cur_path}[{idx}]")
    else:
        yield cur_path or "$", "scalar", obj


def _safe_key(source_ref: str, path: str) -> str:
    raw = f"{Path(source_ref).stem}.{path}"
    return "".join(ch if ch.isalnum() else "_" for ch in raw).strip("_")[:180]


def _path_tokens(path: str):
    return [t for t in path.replace("[", ".").replace("]", "").replace("-", "_").split(".") if t and not t.isdigit()]


def _ssot_fragment(root: str, source_ref: str, path: str, kind: str, value):
    node = {root: {"ssot_elements": {}}}
    cur = node[root]["ssot_elements"]
    for token in _path_tokens(path):
        cur = cur.setdefault(token, {})
    cur["__source_ref"] = source_ref
    cur["__ssot_path"] = path
    cur["__kind"] = kind
    cur["value"] = _ssot_scalar_preview(value)
    return node


def _category_for(kind: str, path: str) -> str:
    top = path.split(".", 1)[0].split("[", 1)[0] if path else "$"
    return f"ssot_{kind}:{top}"


def _matches_space(space: str, source_ref: str, path: str, value) -> bool:
    if space == "sql_attention_observation":
        return True
    text = f"{source_ref} {path} {value if not isinstance(value, (dict, list)) else ''}".lower()
    hints = SPACE_PATH_HINTS.get(space, ())
    return any(h in text for h in hints)


def _ssot_elements_for_space(space: str):
    meta = QUESTION_SPACE_META[space]
    elements = []
    seen = set()
    for source_ref in meta["ssot_sources"]:
        data = yaml.load_file(str(REPO_ROOT / source_ref))
        for path, kind, value in _walk_ssot_elements(source_ref, data):
            if path == "$" or not _matches_space(space, source_ref, path, value):
                continue
            key = _safe_key(source_ref, path)
            dedupe = (source_ref, path)
            if dedupe in seen:
                continue
            seen.add(dedupe)
            elements.append({
                "index": len(elements),
                "source_ref": source_ref,
                "path": path,
                "key": key,
                "question_space": space,
                "question": f"SSOT {source_ref}:{path} ({kind}) を {space} seed structure candidate に含めるか",
                "answer_type": "0_or_1",
                "json_fragment": _ssot_fragment(meta["root"], source_ref, path, kind, value),
                "category": _category_for(kind, path),
                "validation_ref": meta["validation_refs"],
            })
    return elements


def _space_bit_count(space: str) -> int:
    return len(_ssot_elements_for_space(space))

def _nested_fragment(root: str, dotted_path: str):
    cur = {root: {}}
    node = cur[root]
    parts = dotted_path.split(".")
    for part in parts[:-1]:
        node = node.setdefault(part, {})
    node[parts[-1]] = None
    return cur


def _fragment_for_space_key(space: str, key: str):
    root = QUESTION_SPACE_META[space]["root"]
    fragment = _nested_fragment(root, key)
    if space == "admin_contents_authoring" and key == "step2.logical_tables.table_ref":
        fragment[root]["step2"]["logical_tables"] = {
            "logical_table_count": None,
            "tables": [{"tableRef": None, "columns": [{"columnName": None, "dataType": None, "enumGroupId": None, "required": None}]}],
        }
    if space == "admin_ui_builder_authoring" and key == "design_inspector.linkHref":
        fragment[root]["design_inspector"]["linkHref"] = None
        fragment[root]["design_inspector"].setdefault("linkTarget", None)
    if space == "sql_attention_observation" and key == "resolver.physical_table_manifest_bindings":
        fragment[root].setdefault("resolver", {})["physical_table_manifest_bindings"] = {
            "physical_table_id": None,
            "topology_manifest_id": None,
            "binding_evidence_json": None,
            "no_implicit_fallback": True,
        }
    if space == "topology_manifest_authoring" and key == "hubs.hub_relations.sequence_position":
        fragment[root]["hubs"]["hub_relations"]["sequence_position"] = None
        fragment[root]["hubs"]["hub_relations"].setdefault("relation_config", None)
    if space == "seed_runtime_import" and key == "import_apply.conflict_check":
        fragment[root].setdefault("import_apply", {})["conflict_check"] = {"required": True, "fail_close": True}
    return fragment


def question_space_selectors():
    selectors = []
    for index, space in enumerate(QUESTION_SPACE_ORDER):
        meta = QUESTION_SPACE_META[space]
        selectors.append({
            "index": index,
            "question_space": space,
            "surface": meta["ssot_sources"],
            "question": meta["question"],
            "answer_type": "0_or_1",
        })
    return selectors


def topology_seed_question_bits(space: str):
    if space not in QUESTION_SPACE_META:
        raise ValueError(f"unknown question_space: {space}")
    return _ssot_elements_for_space(space)


def stage1_schema():
    return {
        "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
        "stage": 1,
        "answer_format": "binary_array_or_selected_spaces",
        "question_spaces": question_space_selectors(),
    }


def stage2_schema(space: str):
    return {
        "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
        "stage": 2,
        "question_space": space,
        "answer_format": "binary_array",
        "bits": topology_seed_question_bits(space),
    }


def _deep_merge(dst, src):
    for key, value in src.items():
        if isinstance(value, dict) and isinstance(dst.get(key), dict):
            _deep_merge(dst[key], value)
        elif isinstance(value, dict):
            dst[key] = json.loads(json.dumps(value))
        elif isinstance(value, list) and isinstance(dst.get(key), list):
            if not dst[key]:
                dst[key] = json.loads(json.dumps(value))
        else:
            dst[key] = json.loads(json.dumps(value))
    return dst


def _load_json_file(path_value: str):
    path = Path(path_value)
    if not path.is_absolute():
        path = Path.cwd() / path
    with open(path, "r", encoding="utf-8") as f:
        return path, json.load(f)


def _parse_bits(bits_text: str):
    try:
        bits = json.loads(bits_text)
    except json.JSONDecodeError as exc:
        raise ValueError(f"--bits must be a JSON array: {exc}") from exc
    if not isinstance(bits, list):
        raise ValueError("--bits must be a JSON array")
    return bits


def _stage1_selected_spaces(answers):
    if not isinstance(answers, dict):
        raise ValueError("stage1 answers JSON must be an object")
    if isinstance(answers.get("selected_spaces"), list):
        selected = [s for s in answers["selected_spaces"] if s in QUESTION_SPACE_META]
        return selected
    bits = answers.get("bits")
    if not isinstance(bits, list):
        raise ValueError("stage1 answers must contain selected_spaces or bits array")
    selected = []
    for idx, value in enumerate(bits):
        if idx >= len(QUESTION_SPACE_ORDER):
            raise ValueError(f"stage1 bits index outside schema: {idx}")
        if value in (1, True, "1", "true", "True"):
            selected.append(QUESTION_SPACE_ORDER[idx])
        elif value in (0, False, "0", "false", "False", None):
            continue
        else:
            raise ValueError(f"stage1 bits[{idx}] must be 0 or 1")
    return selected


def _stage2_bits_from_answers(answers):
    if not isinstance(answers, dict):
        raise ValueError("stage2 answers JSON must be an object")
    space = answers.get("question_space") or answers.get("space")
    if space not in QUESTION_SPACE_META:
        raise ValueError("stage2 answers must include a valid question_space")
    bits = answers.get("bits")
    if not isinstance(bits, list):
        raise ValueError("stage2 answers must include a bits array")
    return space, bits


def _normalize_enabled_indexes(bits, schema_bits):
    enabled = []
    for idx, value in enumerate(bits):
        if idx >= len(schema_bits):
            raise ValueError(f"bits array has index outside schema: {idx}")
        if value in (1, True, "1", "true", "True"):
            enabled.append(idx)
        elif value in (0, False, "0", "false", "False", None):
            continue
        else:
            raise ValueError(f"bits[{idx}] must be 0 or 1")
    return enabled


def _base_template(space: str):
    return {
        "discussion_only": True,
        "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
        "question_space": space,
        "enabled_keys": [],
        "sql_attention_observation": {},
        "topology_manifest_authoring": {},
        "admin_contents": {},
        "admin_ui_builder": {},
        "admin_manifests": {},
        "runtime_manifest_dispatch": {},
        "seed_runtime_import": {},
        "db_topology_wiring": {},
        "unresolved_questions": [],
    }



def _runtime_declarations_for_seed_candidate(space: str, enabled_keys: list[str]):
    if not enabled_keys:
        return []
    return [{
        "name": f"topology-seed-discussion:{space}:selected-ssot-elements",
        "target": "topology_seed_discussion",
        "layer": space,
        "action": "selected_ssot_elements",
        "runtimeDestination": "topology_transform_runtime",
    }]


def _empty_seed_candidate_payload(space: str, enabled_keys: list[str]):
    return {
        "version": 1,
        "runtimes": _runtime_declarations_for_seed_candidate(space, enabled_keys),
        "discussion_metadata": {
            "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
            "question_space": space,
            "enabled_keys": enabled_keys,
            "source_tool": "topology-seed-discussion",
            "status": "seed_candidate_draft_not_adopted",
        },
    }


def _validate_seed_candidate_payload(payload):
    errors = []
    if not isinstance(payload, dict):
        return ["seed_candidate_payload must be an object"]
    if "version" not in payload:
        errors.append("seed_candidate_payload.version is required")
    if "runtimes" not in payload:
        errors.append("seed_candidate_payload.runtimes is required")
    elif not isinstance(payload.get("runtimes"), list):
        errors.append("seed_candidate_payload.runtimes must be an array")
    else:
        for idx, runtime in enumerate(payload["runtimes"]):
            if not isinstance(runtime, dict):
                errors.append(f"seed_candidate_payload.runtimes[{idx}] must be an object")
                continue
            for field in ("name", "target", "layer", "action"):
                if not isinstance(runtime.get(field), str) or not runtime.get(field, "").strip():
                    errors.append(f"seed_candidate_payload.runtimes[{idx}].{field} must be a non-empty string")
            destination = runtime.get("runtimeDestination", "topology_transform_runtime")
            if destination not in ("topology_transform_runtime", "admin_runtime"):
                errors.append(f"seed_candidate_payload.runtimes[{idx}].runtimeDestination is not allowed")
            if (str(runtime.get("target", "")).lower() == "admin" and
                    str(runtime.get("layer", "")).lower() == "seed_runtime" and
                    str(runtime.get("action", "")).lower() == "import"):
                errors.append(f"seed_candidate_payload.runtimes[{idx}] recursive seed import route is forbidden")
    return errors

def _build_tmp_template(space: str, bits):
    schema_bits = topology_seed_question_bits(space)
    enabled_indexes = _normalize_enabled_indexes(bits, schema_bits)
    template = _base_template(space)
    enabled_keys = []
    disabled_keys = []
    for bit in schema_bits:
        if bit["index"] in enabled_indexes:
            enabled_keys.append(bit["key"])
            _deep_merge(template, bit["json_fragment"])
        else:
            disabled_keys.append(bit["key"])
    template["enabled_keys"] = enabled_keys
    template["discussion_metadata"] = {
        "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
        "question_space": space,
        "enabled_keys": enabled_keys,
        "status": "discussion_draft",
    }
    template["seed_candidate_payload"] = _empty_seed_candidate_payload(space, enabled_keys)
    return enabled_keys, disabled_keys, template


def topology_seed_discussion(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only topology seed discussion JSON draft helper.")
    sub = parser.add_subparsers(dest="mode", required=True)
    inspect_parser = sub.add_parser("inspect", help="Emit stage1 question_space selector or stage2 space schema.")
    inspect_parser.add_argument("--space", choices=QUESTION_SPACE_ORDER, default=None)
    expand_parser = sub.add_parser("expand", help="Expand stage1 answers into selected stage2 schemas.")
    expand_parser.add_argument("--answers", required=True, help="Stage1 answers JSON; read-only.")
    template_parser = sub.add_parser("build-template", help="Merge enabled bit fragments into a tmp JSON template.")
    template_parser.add_argument("--space", choices=QUESTION_SPACE_ORDER, default=None)
    template_source = template_parser.add_mutually_exclusive_group(required=True)
    template_source.add_argument("--bits", help="JSON binary array, e.g. '[1,0,1]'. Requires --space.")
    template_source.add_argument("--answers", help="Stage2 answers JSON containing question_space and bits array; read-only.")
    build_parser = sub.add_parser("build", help="Build candidate discussion JSON from AI-filled tmp JSON.")
    build_parser.add_argument("--answers", required=True, help="Path to AI-filled tmp JSON to read; never modified.")
    args = parser.parse_args(argv)

    if args.mode == "inspect":
        if args.space:
            return _json({
                "tool": "topology-seed-discussion",
                "mode": "inspect",
                "stage": 2,
                "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
                "question_bit_schema": stage2_schema(args.space),
                "answers_template": {
                    "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
                    "stage": 2,
                    "question_space": args.space,
                    "bits": [0 for _ in topology_seed_question_bits(args.space)],
                    "counts": {"total_bits": _space_bit_count(args.space)},
                    "ids": {},
                    "notes": {},
                },
            })
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "inspect",
            "stage": 1,
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "question_space_selector": stage1_schema(),
            "answers_template": {
                "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
                "stage": 1,
                "bits": [0 for _ in QUESTION_SPACE_ORDER],
                "selected_spaces": [],
                "notes": {},
            },
        })

    if args.mode == "expand":
        try:
            _, answers = _load_json_file(args.answers)
            selected_spaces = _stage1_selected_spaces(answers)
        except (OSError, json.JSONDecodeError, ValueError) as exc:
            sys.stderr.write(f"FAIL: {exc}\n")
            return 1
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "expand",
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "selected_spaces": selected_spaces,
            "stage2_schemas": [stage2_schema(space) for space in selected_spaces],
        })

    if args.mode == "build-template":
        try:
            if args.answers is not None:
                _, answers = _load_json_file(args.answers)
                space, bits = _stage2_bits_from_answers(answers)
            else:
                if args.space is None:
                    raise ValueError("--space is required when using --bits")
                space = args.space
                bits = _parse_bits(args.bits)
            enabled_keys, disabled_keys, template = _build_tmp_template(space, bits)
        except (OSError, json.JSONDecodeError, ValueError) as exc:
            sys.stderr.write(f"FAIL: {exc}\n")
            return 1
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "build-template",
            "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
            "question_space": space,
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "enabled_keys": enabled_keys,
            "disabled_keys": disabled_keys,
            "discussion_metadata": template["discussion_metadata"],
            "seed_candidate_payload": template["seed_candidate_payload"],
            "tmp_json_template": template,
            "usage_note": "Caller may redirect stdout to /tmp/topology-seed-discussion.tmp.json. This tool does not write files.",
        })

    try:
        answers_path, tmp_json = _load_json_file(args.answers)
    except OSError as exc:
        sys.stderr.write(f"FAIL: cannot read answers JSON: {exc}\n")
        return 1
    except json.JSONDecodeError as exc:
        sys.stderr.write(f"FAIL: invalid answers JSON: {exc}\n")
        return 1
    if not isinstance(tmp_json, dict):
        sys.stderr.write("FAIL: answers JSON root must be an object\n")
        return 1
    candidate_sections = {
        "sql_attention_observation": tmp_json.get("sql_attention_observation", {}),
        "topology_manifest_authoring": tmp_json.get("topology_manifest_authoring", {}),
        "admin_contents": tmp_json.get("admin_contents", {}),
        "admin_ui_builder": tmp_json.get("admin_ui_builder", {}),
        "admin_manifests": tmp_json.get("admin_manifests", {}),
        "runtime_manifest_dispatch": tmp_json.get("runtime_manifest_dispatch", {}),
        "seed_runtime_import": tmp_json.get("seed_runtime_import", {}),
        "db_topology_wiring": tmp_json.get("db_topology_wiring", {}),
    }
    candidate_sections = {k: v if isinstance(v, dict) else {} for k, v in candidate_sections.items()}
    unresolved = tmp_json.get("unresolved_questions", []) if isinstance(tmp_json.get("unresolved_questions", []), list) else []
    enabled_keys = tmp_json.get("enabled_keys", []) if isinstance(tmp_json.get("enabled_keys", []), list) else []
    seed_candidate_payload = tmp_json.get("seed_candidate_payload")
    if seed_candidate_payload is None:
        seed_candidate_payload = _empty_seed_candidate_payload(str(tmp_json.get("question_space") or "unknown"), enabled_keys)
    seed_errors = _validate_seed_candidate_payload(seed_candidate_payload)
    if seed_errors:
        sys.stderr.write("FAIL: invalid seed_candidate_payload: " + "; ".join(seed_errors) + "\n")
        return 1
    discussion_metadata = tmp_json.get("discussion_metadata") if isinstance(tmp_json.get("discussion_metadata"), dict) else {
        "schema_id": tmp_json.get("schema_id"),
        "question_space": tmp_json.get("question_space"),
        "enabled_keys": enabled_keys,
        "status": "discussion_draft",
    }
    return _json({
        "tool": "topology-seed-discussion",
        "mode": "build",
        "source_file": str(answers_path),
        "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
        "discussion_metadata": discussion_metadata,
        "discussion_result": {
            "status": "discussion_draft",
            "question_space": tmp_json.get("question_space"),
            "enabled_keys": enabled_keys,
            "candidates": candidate_sections,
            "unresolved_questions": unresolved,
        },
        "seed_candidate_payload": seed_candidate_payload,
        "candidate_seed_json": seed_candidate_payload,
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
