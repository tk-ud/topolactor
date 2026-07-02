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


TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID = "topology_seed_discussion_admin_ui_v1"

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

TOPOLOGY_SEED_BASE_TEMPLATE = {
    "discussion_only": True,
    "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
    "enabled_keys": [],
    "authoring_mode": {},
    "admin_contents": {},
    "admin_ui_builder": {},
    "admin_manifests": {},
    "runtime_manifest_dispatch": {},
    "unresolved_questions": [],
}


def _fragment_for_key(key: str):
    parts = key.split(".")
    leaf = parts[-1]
    if key.startswith("authoring."):
        return {"authoring_mode": {leaf: None}}
    if key == "contents.step2.logical_tables":
        return {
            "admin_contents": {
                "step2_logical_tables": {
                    "logical_table_count": None,
                    "tables": [
                        {
                            "tableRef": None,
                            "columns": [
                                {
                                    "columnName": None,
                                    "dataType": None,
                                    "enumGroupId": None,
                                    "required": None,
                                }
                            ],
                        }
                    ],
                }
            }
        }
    if key.startswith("contents.step1."):
        return {"admin_contents": {"step1_manifest_shell": {leaf: None}}}
    if key.startswith("contents.step2."):
        return {"admin_contents": {"step2_logical_tables": {leaf: None}}}
    if key.startswith("contents.step2_5."):
        return {"admin_contents": {"step2_5_relationship_configuration": {leaf: None}}}
    if key.startswith("contents.step3."):
        return {"admin_contents": {"step3_physical_table_and_page_binding": {leaf: None}}}
    if key.startswith("contents.aggregation."):
        return {"admin_contents": {"aggregation": {leaf: None}}}
    if key.startswith("contents.search."):
        return {"admin_contents": {"search": {leaf: None}}}
    if key.startswith("ui_builder."):
        return {"admin_ui_builder": {leaf: None}}
    if key.startswith("manifests."):
        return {"admin_manifests": {leaf: None}}
    if key.startswith("runtime."):
        return {"runtime_manifest_dispatch": {leaf: None}}
    return {"unmapped": {key: None}}


def _bit(key: str, surface: str, question: str):
    return {"key": key, "surface": surface, "question": question, "json_fragment": _fragment_for_key(key)}


def topology_seed_question_bits():
    raw_bits = [
        _bit("authoring.new_create", "authoring-mode", "新規作成として seed discussion に含めるか"),
        _bit("authoring.canonical_clone", "authoring-mode", "canonical clone として seed discussion に含めるか"),
        _bit("authoring.replacement_clone", "authoring-mode", "replacement clone として seed discussion に含めるか"),
        _bit("authoring.demo_seed", "authoring-mode", "demo seed 用候補として含めるか"),
        _bit("authoring.default_bootstrap", "authoring-mode", "default bootstrap 用候補として含めるか"),
        _bit("authoring.production_bootstrap_candidate", "authoring-mode", "production bootstrap candidate として含めるか"),
        _bit("authoring.preserve_ids", "authoring-mode", "既存ID保持を議論対象に含めるか"),
        _bit("authoring.regenerate_ids", "authoring-mode", "ID再生成を議論対象に含めるか"),
        _bit("authoring.deprecate_old_manifest", "authoring-mode", "旧manifest deprecate を議論対象に含めるか"),
        _bit("contents.step1.manifest_shell", "/admin/contents", "step1 manifest shell を seed discussion に含めるか"),
        _bit("contents.step1.topology_label", "/admin/contents", "step1 topology label を seed discussion に含めるか"),
        _bit("contents.step1.draft_lifecycle", "/admin/contents", "step1 draft lifecycle を seed discussion に含めるか"),
        _bit("contents.step2.logical_tables", "/admin/contents", "logical table definitions を含めるか"),
        _bit("contents.step2.multiple_logical_tables", "/admin/contents", "複数 logical table を含めるか"),
        _bit("contents.step2.columns", "/admin/contents", "column definitions を含めるか"),
        _bit("contents.step2.enum_columns", "/admin/contents", "enum columns を含めるか"),
        _bit("contents.step2.enum_group_refs", "/admin/contents", "enum group refs を含めるか"),
        _bit("contents.step2_5.relations", "/admin/contents", "step2.5 relations を含めるか"),
        _bit("contents.step2_5.local_relation", "/admin/contents", "local relation side を含めるか"),
        _bit("contents.step2_5.draft_remote_relation", "/admin/contents", "draft remote relation を含めるか"),
        _bit("contents.step2_5.active_remote_manifest_relation", "/admin/contents", "active remote manifest relation を含めるか"),
        _bit("contents.step2_5.relation_config", "/admin/contents", "relation_config を含めるか"),
        _bit("contents.step2_5.remote_target_ambiguity_check", "/admin/contents", "remote target ambiguity check を含めるか"),
        _bit("contents.step3.physical_table_binding", "/admin/contents", "physical table binding を含めるか"),
        _bit("contents.step3.multiple_physical_tables", "/admin/contents", "複数 physical table を含めるか"),
        _bit("contents.step3.page_screen_label", "/admin/contents", "page/screen label を含めるか"),
        _bit("contents.step3.operation_kinds", "/admin/contents", "operation kinds を含めるか"),
        _bit("contents.step3.operation_entity_bindings", "/admin/contents", "operation entity bindings を含めるか"),
        _bit("contents.step3.initial_data_rows", "/admin/contents", "initial data rows を含めるか"),
        _bit("contents.step3.enum_backed_initial_data", "/admin/contents", "enum backed initial data を含めるか"),
        _bit("contents.step3.display_columns_derivation", "/admin/contents", "display columns derivation を含めるか"),
        _bit("contents.step3.display_column_mode", "/admin/contents", "displayColumnMode を含めるか"),
        _bit("contents.aggregation.enabled", "/admin/contents", "aggregation を含めるか"),
        _bit("contents.aggregation.aggregation_key", "/admin/contents", "aggregation key を含めるか"),
        _bit("contents.aggregation.aggregation_measures", "/admin/contents", "aggregation measures を含めるか"),
        _bit("contents.aggregation.having_conditions", "/admin/contents", "having conditions を含めるか"),
        _bit("contents.search.enabled", "/admin/contents", "search を含めるか"),
        _bit("contents.search.search_key_columns", "/admin/contents", "search key columns を含めるか"),
        _bit("contents.search.search_conditions", "/admin/contents", "search conditions を含めるか"),
        _bit("contents.search.logical_connector", "/admin/contents", "logical connector を含めるか"),
        _bit("ui_builder.route_key", "/admin/ui-builder", "route key を含めるか"),
        _bit("ui_builder.package", "/admin/ui-builder", "package を含めるか"),
        _bit("ui_builder.package_auto_generation", "/admin/ui-builder", "package auto generation を含めるか"),
        _bit("ui_builder.layout", "/admin/ui-builder", "layout を含めるか"),
        _bit("ui_builder.wiring", "/admin/ui-builder", "wiring を含めるか"),
        _bit("ui_builder.component_auto_registration", "/admin/ui-builder", "component auto registration を含めるか"),
        _bit("ui_builder.canvas_nodes", "/admin/ui-builder", "canvas nodes を含めるか"),
        _bit("ui_builder.catalog_component_nodes", "/admin/ui-builder", "catalog component nodes を含めるか"),
        _bit("ui_builder.structural_html_nodes", "/admin/ui-builder", "structural html nodes を含めるか"),
        _bit("ui_builder.parent_node_id", "/admin/ui-builder", "parent node id を含めるか"),
        _bit("ui_builder.slot_key", "/admin/ui-builder", "slot key を含めるか"),
        _bit("ui_builder.order_index", "/admin/ui-builder", "order index を含めるか"),
        _bit("ui_builder.tmp_draft", "/admin/ui-builder", "tmp draft を含めるか"),
        _bit("ui_builder.layout_patch_preview_validate_apply", "/admin/ui-builder", "layout patch preview/validate/apply を含めるか"),
        _bit("ui_builder.component_style_design", "/admin/ui-builder", "component style design を含めるか"),
        _bit("ui_builder.promoted_design_boundary", "/admin/ui-builder", "promoted design boundary を含めるか"),
        _bit("manifests.hub", "/admin/manifests", "hub を含めるか"),
        _bit("manifests.existing_hub", "/admin/manifests", "existing hub を含めるか"),
        _bit("manifests.new_hub", "/admin/manifests", "new hub を含めるか"),
        _bit("manifests.topology_manifest", "/admin/manifests", "topology manifest を含めるか"),
        _bit("manifests.hub_relations", "/admin/manifests", "hub relations を含めるか"),
        _bit("manifests.related_hub", "/admin/manifests", "related hub を含めるか"),
        _bit("manifests.sequence_position", "/admin/manifests", "sequence position を含めるか"),
        _bit("manifests.relation_config", "/admin/manifests", "relation config を含めるか"),
        _bit("manifests.navigation", "/admin/manifests", "navigation を含めるか"),
        _bit("manifests.page_group_continuity", "/admin/manifests", "page group continuity を含めるか"),
        _bit("manifests.remote_relationship_target", "/admin/manifests", "remote relationship target を含めるか"),
        _bit("runtime.dispatcher_mapping", "runtime-manifest-dispatch", "dispatcher mapping を含めるか"),
        _bit("runtime.role", "runtime-manifest-dispatch", "role axis を含めるか"),
        _bit("runtime.target", "runtime-manifest-dispatch", "target axis を含めるか"),
        _bit("runtime.layer", "runtime-manifest-dispatch", "layer axis を含めるか"),
        _bit("runtime.action", "runtime-manifest-dispatch", "action axis を含めるか"),
        _bit("runtime.runtime_mapping", "runtime-manifest-dispatch", "runtime mapping を含めるか"),
        _bit("runtime.runtime_destination", "runtime-manifest-dispatch", "runtime destination を含めるか"),
        _bit("runtime.db_notify_projection_mapping", "runtime-manifest-dispatch", "db_notify projection mapping を含めるか"),
        _bit("runtime.projection_constructor_mapping", "runtime-manifest-dispatch", "projection constructor mapping を含めるか"),
        _bit("runtime.screen_data_shape", "runtime-manifest-dispatch", "screen data shape を含めるか"),
        _bit("runtime.active_manifest_conflict", "runtime-manifest-dispatch", "active manifest conflict check を含めるか"),
        _bit("runtime.fail_close_missing_manifest", "runtime-manifest-dispatch", "missing manifest fail-close を含めるか"),
    ]
    return [{"index": i, **bit} for i, bit in enumerate(raw_bits)]


def topology_seed_question_schema():
    return {
        "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
        "answer_format": "binary_array",
        "bits": topology_seed_question_bits(),
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


def _bits_from_answers(answers):
    if not isinstance(answers, dict) or "bits" not in answers:
        raise ValueError("answers JSON must be an object with a bits array")
    bits = answers["bits"]
    if not isinstance(bits, list):
        raise ValueError("answers.bits must be an array")
    return bits


def _parse_bits(bits_text: str):
    try:
        bits = json.loads(bits_text)
    except json.JSONDecodeError as exc:
        raise ValueError(f"--bits must be a JSON array: {exc}") from exc
    if not isinstance(bits, list):
        raise ValueError("--bits must be a JSON array")
    return bits


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


def _build_tmp_template(bits):
    schema_bits = topology_seed_question_bits()
    enabled_indexes = _normalize_enabled_indexes(bits, schema_bits)
    template = json.loads(json.dumps(TOPOLOGY_SEED_BASE_TEMPLATE))
    enabled_keys = []
    disabled_keys = []
    for bit in schema_bits:
        if bit["index"] in enabled_indexes:
            enabled_keys.append(bit["key"])
            _deep_merge(template, bit["json_fragment"])
        else:
            disabled_keys.append(bit["key"])
    template["enabled_keys"] = enabled_keys
    return enabled_keys, disabled_keys, template


def topology_seed_discussion(argv: list[str]) -> int:
    rejected = _reject_mutation_args(argv)
    if rejected is not None:
        return rejected
    parser = argparse.ArgumentParser(description="Read-only topology seed discussion JSON draft helper.")
    sub = parser.add_subparsers(dest="mode", required=True)
    sub.add_parser("inspect", help="Emit admin UI question bit schema for seed discussion.")
    template_parser = sub.add_parser("build-template", help="Merge enabled bit fragments into a tmp JSON template.")
    template_source = template_parser.add_mutually_exclusive_group(required=True)
    template_source.add_argument("--bits", help="JSON binary array, e.g. '[1,0,1]'.")
    template_source.add_argument("--answers", help="Answers JSON file containing a bits array; read-only.")
    build_parser = sub.add_parser("build", help="Build candidate discussion JSON from AI-filled tmp JSON.")
    build_parser.add_argument("--answers", required=True, help="Path to AI-filled tmp JSON to read; never modified.")
    args = parser.parse_args(argv)

    schema = topology_seed_question_schema()
    if args.mode == "inspect":
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "inspect",
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "question_bit_schema": schema,
            "answers_template": {
                "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
                "bits": [0 for _ in schema["bits"]],
                "counts": {
                    "total_bits": len(schema["bits"]),
                    "admin_contents_bits": sum(1 for b in schema["bits"] if b["surface"] == "/admin/contents"),
                    "admin_ui_builder_bits": sum(1 for b in schema["bits"] if b["surface"] == "/admin/ui-builder"),
                    "admin_manifests_bits": sum(1 for b in schema["bits"] if b["surface"] == "/admin/manifests"),
                },
                "ids": {},
                "notes": {},
            },
        })

    if args.mode == "build-template":
        try:
            bits = _parse_bits(args.bits) if args.bits is not None else _bits_from_answers(_load_json_file(args.answers)[1])
            enabled_keys, disabled_keys, template = _build_tmp_template(bits)
        except (OSError, json.JSONDecodeError, ValueError) as exc:
            sys.stderr.write(f"FAIL: {exc}\n")
            return 1
        return _json({
            "tool": "topology-seed-discussion",
            "mode": "build-template",
            "schema_id": TOPOLOGY_SEED_DISCUSSION_SCHEMA_ID,
            "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
            "enabled_keys": enabled_keys,
            "disabled_keys": disabled_keys,
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
    enabled_keys = tmp_json.get("enabled_keys", []) if isinstance(tmp_json.get("enabled_keys", []), list) else []
    admin_contents = tmp_json.get("admin_contents", {}) if isinstance(tmp_json.get("admin_contents", {}), dict) else {}
    admin_ui_builder = tmp_json.get("admin_ui_builder", {}) if isinstance(tmp_json.get("admin_ui_builder", {}), dict) else {}
    admin_manifests = tmp_json.get("admin_manifests", {}) if isinstance(tmp_json.get("admin_manifests", {}), dict) else {}
    runtime_manifest_dispatch = tmp_json.get("runtime_manifest_dispatch", {}) if isinstance(tmp_json.get("runtime_manifest_dispatch", {}), dict) else {}
    unresolved = tmp_json.get("unresolved_questions", []) if isinstance(tmp_json.get("unresolved_questions", []), list) else []
    return _json({
        "tool": "topology-seed-discussion",
        "mode": "build",
        "source_file": str(answers_path),
        "boundary": TOPOLOGY_SEED_DISCUSSION_BOUNDARY,
        "discussion_result": {
            "status": "discussion_draft",
            "enabled_keys": enabled_keys,
            "admin_contents_candidate": admin_contents,
            "admin_ui_builder_candidate": admin_ui_builder,
            "admin_manifests_candidate": admin_manifests,
            "runtime_manifest_dispatch_candidate": runtime_manifest_dispatch,
            "unresolved_questions": unresolved,
        },
        "candidate_seed_json": {
            "discussion_only": True,
            "admin_contents": admin_contents,
            "admin_ui_builder": admin_ui_builder,
            "admin_manifests": admin_manifests,
            "runtime_manifest_dispatch": runtime_manifest_dispatch,
        },
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
