#!/usr/bin/env python3
"""check_react_schema_topology_seed_translator.py -- executable proof surface.

SSOT: docs/design/react-schema-topology-seed-translator-ssot.yaml
Tool under test: .agent/tools/react-schema-topology-seed-translator
                 (-> .agent/scripts/react_schema_topology_seed_translator.py)
Fixtures:
  .agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json
  .agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.topology-seed.input.json

Runs the acceptance checks for both implemented modes -- generate-react-schema
(the original credential-management-0092 implementation task) and
generate-topology-seed (its follow-up, converting that mode's own output into
a topology_ui_seed_contract candidate) -- against their golden fixtures end
to end, plus negative-path scenarios built as ephemeral tmp fixtures (no
extra committed fixture files needed per scenario). round-trip-check stays
not_implemented_out_of_scope. Structured JSON assertions live here (Python3
stdlib only); the paired check-*.sh is a bash CI entrypoint/orchestration
wrapper only.
"""
from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL = REPO_ROOT / ".agent" / "tools" / "react-schema-topology-seed-translator"
FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "credential-management-0092.input.json"
TOPOLOGY_SEED_FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "credential-management-0092.topology-seed.input.json"
ADMIN_ENUM_AE200_TOPOLOGY_SEED_FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "admin-enum-ae200.topology-seed.input.json"
CRUD_SCHEMA_FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "physical-search-crud-aggregate.react-schema.json"
CRUD_TOPOLOGY_SEED_FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "physical-search-crud-aggregate.topology-seed.input.json"
AGENT_TMP_DIR = REPO_ROOT / ".agent" / "tmp"
SEED_EMPTY_PATH = REPO_ROOT / "db" / "seed_empty.sql"
CRUD_PRESET_SEED_SQL_PATH = REPO_ROOT / "db" / "physical_search_crud_aggregate_preset_seed.sql"

# Mirrors frontend/tests/presetSeedLineContract.test.ts REQUIRED_NODE_FIELDS,
# so the seed-first connection checks below apply the same layout node shape
# contract as the existing generic seed gate.
REQUIRED_NODE_FIELDS = ["nodeId", "nodeKind", "componentKey", "componentKind", "parentNodeId", "orderIndex"]
COMPILE_SNAPSHOT_BLOCK_RE = re.compile(r"\$\$([\s\S]*?)\$\$::jsonb")
BACKEND_INSTANCE_TARGET_REF_RE = re.compile(r"^instance-port:[^:]+:[^:]+:[^:]+$")


def layout_patch_from_seed_runtime_interaction(seed_action_record):
    """Connect a topology seed action record to the backend layout_patch_json shape.

    The proof surface deliberately leaves translator-output-only checks and builds
    the minimal nodes[].runtimeInteractions[] payload consumed by
    NpgsqlUiTopologyRepository.ValidateRuntimeInteractions /
    ApplyConfirmedLayoutPatchAsync before AssignRuntimeInteractionIds.
    """
    return {
        "nodes": [{
            "nodeId": seed_action_record.get("key") or seed_action_record.get("actionKey") or "seed-action-node",
            "nodeKind": "catalog_component",
            "componentKey": "button.primitive",
            "componentKind": "action/button",
            "runtimeInteractions": seed_action_record.get("runtimeInteractions") or [],
        }]
    }


def validate_runtime_interactions_boundary_equivalent(layout_patch_json, approved_instance_target_refs):
    """Python proof mirror of backend ValidateRuntimeInteractions targetRef boundary.

    This is intentionally small and vocabulary-focused: it catches seed/template
    candidate targetRef drift before the proof claims ApplyConfirmedLayoutPatchAsync
    can reach AssignRuntimeInteractionIds.
    """
    nodes = layout_patch_json.get("nodes") if isinstance(layout_patch_json, dict) else None
    if not isinstance(nodes, list):
        return "LAYOUT_PATCH_NODES_MUST_BE_ARRAY"
    for source_node in nodes:
        interactions = source_node.get("runtimeInteractions") if isinstance(source_node, dict) else None
        if interactions is None:
            continue
        if not isinstance(interactions, list):
            return "RUNTIME_INTERACTIONS_MUST_BE_ARRAY"
        for interaction in interactions:
            if not isinstance(interaction, dict):
                return "RUNTIME_INTERACTION_MUST_BE_OBJECT"
            trigger = interaction.get("trigger")
            if not isinstance(trigger, str) or not trigger.strip():
                return "RUNTIME_INTERACTION_TRIGGER_REQUIRED"
            action_type = interaction.get("actionType")
            if not isinstance(action_type, str) or not action_type.strip():
                return "RUNTIME_INTERACTION_ACTION_TYPE_REQUIRED"
            if action_type == "dispatchInstanceOperation":
                target_ref = interaction.get("instanceTargetRef")
                if not isinstance(target_ref, str) or not target_ref.strip():
                    return "RUNTIME_INTERACTION_INSTANCE_TARGET_REF_REQUIRED"
                if not BACKEND_INSTANCE_TARGET_REF_RE.match(target_ref):
                    return f"RUNTIME_INTERACTION_INSTANCE_TARGET_REF_INVALID:{target_ref}"
                if target_ref not in approved_instance_target_refs:
                    return f"RUNTIME_INTERACTION_INSTANCE_TARGET_REF_NOT_APPROVED:{target_ref}"
            elif action_type == "dispatchExternalPort":
                target_ref = interaction.get("portTargetRef")
                if not isinstance(target_ref, str) or not target_ref.strip():
                    return "RUNTIME_INTERACTION_PORT_TARGET_REF_REQUIRED"
                if not target_ref.startswith("external-port:"):
                    return f"RUNTIME_INTERACTION_PORT_TARGET_REF_INVALID:{target_ref}"
            payload_from = interaction.get("payloadFrom")
            if payload_from is not None:
                if not isinstance(payload_from, dict):
                    return "RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT"
                for key, value in payload_from.items():
                    if not isinstance(value, str):
                        return f"RUNTIME_INTERACTION_PAYLOAD_FROM_VALUE_MUST_BE_STRING:{key}"
    return None


SCENARIO_UUID = "bad04ed9-7d39-4cfc-a22c-db2684d4cb0a"

TARGET_MANIFEST_UUID = "00000000-0000-0000-0000-000000000092"
TARGET_MANIFEST_KEY = "auth.external.credential_management.projection"
AUTH_USER_BOUNDARY_UUID = "00000000-0000-0000-0000-000000000091"
BUNDLE = "auth-external-credential-management-topology-projection"

STMT_RE = re.compile(r'INSERT INTO\s+([A-Za-z0-9_.]+)', re.IGNORECASE)


def resolve_seed_evidence_from_seed_file(target_uuid, target_manifest_key):
    """Independently resolves seed evidence straight from db/seed_empty.sql.

    This is the evidence/proof verification side of the seedEvidence contract:
    the translator itself never reads db/*.sql (see translator_input_authority
    in the SSOT and passthrough_seed_evidence() in the implementation body);
    this test script is where that reading is allowed to happen, so the
    fixture's pre-supplied seedEvidence can be checked against the real seed
    file instead of trusted blindly.
    """
    text = SEED_EMPTY_PATH.read_text(encoding="utf-8")
    matches = list(STMT_RE.finditer(text))
    manifest_hit = False
    other_tables = []
    for i, m in enumerate(matches):
        start = m.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        span = text[start:end]
        if target_uuid not in span:
            continue
        table = m.group(1)
        table_lower = table.strip().lower()
        if table_lower == "manifest" and target_manifest_key in span:
            manifest_hit = True
        elif "manifest" not in table_lower:
            other_tables.append(table)
    if not manifest_hit:
        return None
    evidence = {
        "screenUuidNamespace": "manifest.manifest_id",
        "screenUuid": target_uuid,
        "manifestKey": target_manifest_key,
        "bundle": BUNDLE,
        "ignoredSameUuidOtherNamespace": None,
    }
    other_unique = sorted(set(other_tables))
    if other_unique:
        first = other_unique[0]
        namespace = f"{first}.structure_map_id" if "structure_maps" in first else f"{first}.id"
        evidence["ignoredSameUuidOtherNamespace"] = {
            "namespace": namespace,
            "uuid": target_uuid,
            "reason": "separate namespace; not credential management screen UUID",
        }
    return evidence


def extract_compile_snapshot(sql_text, seed_file_label):
    """Python port of extractCompileSnapshot() in presetSeedLineContract.test.ts.

    Test/proof-layer-only reading of a real preset seed SQL file's compile
    snapshot INSERT (5 ordered $$...$$::jsonb blocks: layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json). The translator body never does
    this; only this check script's seed-first connection checks do.
    """
    marker = "INSERT INTO topology.mock_preset_compile_snapshot"
    idx = sql_text.find(marker)
    if idx == -1:
        raise AssertionError(f"{seed_file_label}: compile snapshot INSERT not found")
    section = sql_text[idx:]
    blocks = [json.loads(m.group(1).strip()) for m in COMPILE_SNAPSHOT_BLOCK_RE.finditer(section)]
    if len(blocks) < 5:
        raise AssertionError(f"{seed_file_label}: expected >=5 jsonb blocks in compile snapshot section, got {len(blocks)}")
    return {
        "layoutPatchJson": blocks[0],
        "packageMembershipCandidateJson": blocks[1],
        "wiringCandidateJson": blocks[2],
        "styleCandidateJson": blocks[3],
        "unresolvedJson": blocks[4],
    }

FAILURES = []
PASS_COUNT = 0


def ok(label):
    global PASS_COUNT
    PASS_COUNT += 1
    print(f"OK  [{PASS_COUNT}] {label}")


def fail(label):
    FAILURES.append(label)
    print(f"FAIL: {label}", file=sys.stderr)


def expect(label, condition):
    if condition:
        ok(label)
    else:
        fail(label)


def run_tool(args, timeout=30):
    proc = subprocess.run(
        [str(TOOL), *args],
        cwd=str(REPO_ROOT),
        capture_output=True,
        text=True,
        timeout=timeout,
    )
    return proc


def run_generate(input_path, extra_args=None):
    args = ["generate-react-schema", "--input", str(input_path)]
    if extra_args:
        args.extend(extra_args)
    proc = run_tool(args)
    try:
        doc = json.loads(proc.stdout)
    except json.JSONDecodeError:
        doc = None
    return proc, doc


def run_generate_topology_seed(input_path, extra_args=None):
    args = ["generate-topology-seed", "--input", str(input_path)]
    if extra_args:
        args.extend(extra_args)
    proc = run_tool(args)
    try:
        doc = json.loads(proc.stdout)
    except json.JSONDecodeError:
        doc = None
    return proc, doc


def rule_ids(doc):
    return [e.get("ruleId") for e in (doc or {}).get("validationErrors", [])]


def record_byte_size(wrapper):
    """storage_adoption_contract.constraint.budget_measurement: the manifest.topology
    / idx_manifest_topology GIN index budget is a UTF-8 byte budget, not a Python
    character-count budget -- multi-byte (e.g. non-ASCII) content must be measured
    as encoded bytes to match what Postgres actually stores."""
    return len(json.dumps(wrapper, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))


_TMP_FIXTURE_COUNTER = [0]


def _next_tmp_name(prefix):
    _TMP_FIXTURE_COUNTER[0] += 1
    return f"{prefix}_{_TMP_FIXTURE_COUNTER[0]}.json"


def write_tmp_fixture(input_text, base=None, tmpdir=None):
    payload = dict(base) if base else {
        "schemaId": "topolactor.translator_input.v1",
        "mode": "generate_react_schema",
        "targetSurface": "auth.external.credential_management.projection",
        "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
    }
    payload["inputText"] = input_text
    path = Path(tmpdir) / _next_tmp_name("tmp_fixture")
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def write_topology_seed_tmp_fixture(input_text_json_str, tmpdir=None):
    payload = {
        "schemaId": "topolactor.translator_input.v1",
        "mode": "generate_topology_ui_seed",
        "targetSurface": "auth.external.credential_management.projection",
        "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
        "inputText": input_text_json_str,
    }
    path = Path(tmpdir) / _next_tmp_name("tmp_topology_seed_fixture")
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def find_node(node, key):
    if node.get("key") == key:
        return node
    for c in node.get("children") or []:
        found = find_node(c, key)
        if found is not None:
            return found
    return None


def collect_keys_by_kind(node, kind, acc=None):
    if acc is None:
        acc = []
    if node.get("kind") == kind:
        acc.append(node.get("key"))
    for c in node.get("children") or []:
        collect_keys_by_kind(c, kind, acc)
    return acc


def collect_wiring_lanes(node, acc=None):
    if acc is None:
        acc = set()
    eb = node.get("eventBinding")
    if eb and eb.get("wiringLane"):
        acc.add(eb["wiringLane"])
    for c in node.get("children") or []:
        collect_wiring_lanes(c, acc)
    return acc


def main():
    AGENT_TMP_DIR.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=str(AGENT_TMP_DIR)) as tmpdir:
        # 1. golden fixture generates translated.json
        out_path = Path(tmpdir) / "translated.json"
        proc, doc = run_generate(FIXTURE, extra_args=[
            "--output", str(out_path),
            "--scenario-uuid", SCENARIO_UUID,
        ])
        expect("1. credential-management-0092 fixture generates translated.json", out_path.is_file() and doc is not None)

        expect("2. translated.json.schemaId == topolactor.translator_output.v1", doc and doc.get("schemaId") == "topolactor.translator_output.v1")
        expect("3. translated.json.scenario.uuid exists", doc and dig(doc, "scenario", "uuid") == SCENARIO_UUID)

        # seedEvidence in translated.json is a passthrough of the fixture's
        # pre-supplied seedEvidence (the translator body never reads db/*.sql
        # itself -- see passthrough_seed_evidence() in the implementation).
        # This test independently re-resolves the same evidence straight from
        # db/seed_empty.sql and cross-checks the fixture's claim against it,
        # so evidence verification stays here rather than inside the translator.
        independent_evidence = resolve_seed_evidence_from_seed_file(TARGET_MANIFEST_UUID, TARGET_MANIFEST_KEY)
        expect("4a. db/seed_empty.sql independently resolves manifest 0092 evidence", independent_evidence is not None)

        seed_evidence = (doc or {}).get("seedEvidence") or {}
        expect("4. seedEvidence.screenUuidNamespace == manifest.manifest_id", seed_evidence.get("screenUuidNamespace") == "manifest.manifest_id")
        expect("5. seedEvidence.screenUuid == 00000000-0000-0000-0000-000000000092", seed_evidence.get("screenUuid") == "00000000-0000-0000-0000-000000000092")
        expect("6. seedEvidence.manifestKey == auth.external.credential_management.projection", seed_evidence.get("manifestKey") == "auth.external.credential_management.projection")
        expect("7. seedEvidence.relatedAuthUserBoundaryManifestId == 00000000-0000-0000-0000-000000000091", seed_evidence.get("relatedAuthUserBoundaryManifestId") == "00000000-0000-0000-0000-000000000091")
        expect(
            "7a. fixture-supplied seedEvidence matches independent db/seed_empty.sql resolution "
            "(screenUuid/manifestKey/ignoredSameUuidOtherNamespace)",
            independent_evidence is not None
            and seed_evidence.get("screenUuid") == independent_evidence["screenUuid"]
            and seed_evidence.get("manifestKey") == independent_evidence["manifestKey"]
            and seed_evidence.get("ignoredSameUuidOtherNamespace") == independent_evidence["ignoredSameUuidOtherNamespace"],
        )

        no_db_refs_in_output = "db/seed_empty.sql" not in json.dumps(doc.get("reactSchemaCandidate")) if doc else False
        expect("7b. no db/*.sql path appears in reactSchemaCandidate.sourceYamlRefs (seed evidence stays out of sourceYamlRefs)", no_db_refs_in_output)

        fixture_envelope = json.loads(FIXTURE.read_text(encoding="utf-8"))

        # 7c. envelope-level knownGapRefs are not silently dropped: they must
        # surface in both unresolvedGaps and exchangeReport.knownGapRefs. Uses a
        # synthetic tmp fixture (not the credential-management-0092 golden
        # fixture) so this generic-behavior test doesn't depend on that fixture
        # continuing to carry an open gap -- instance_settings_projection_category_not_yet_represented
        # was resolved and removed from the golden fixture's knownGapRefs.
        gap_probe_envelope = {
            "schemaId": "topolactor.translator_input.v1",
            "mode": "generate_react_schema",
            "targetSurface": "auth.external.credential_management.projection",
            "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
            "knownGapRefs": ["synthetic_probe_gap_for_propagation_check"],
            "inputText": "[projection key=p label=x sourceYamlRefs=a]\n[/projection]\n",
        }
        gap_probe_path = Path(tmpdir) / _next_tmp_name("tmp_gap_probe_fixture")
        gap_probe_path.write_text(json.dumps(gap_probe_envelope), encoding="utf-8")
        _, gap_probe_doc = run_generate(gap_probe_path)
        envelope_gaps = {"synthetic_probe_gap_for_propagation_check"}
        expect(
            "7c. envelope-level knownGapRefs propagate into unresolvedGaps and exchangeReport.knownGapRefs",
            envelope_gaps.issubset(set((gap_probe_doc or {}).get("unresolvedGaps") or []))
            and envelope_gaps.issubset(set(dig(gap_probe_doc or {}, "exchangeReport", "knownGapRefs") or [])),
        )

        schema_candidate = (doc or {}).get("reactSchemaCandidate") or {}
        expect("8. reactSchemaCandidate.schema == topolactor.react_schema.v1", schema_candidate.get("schema") == "topolactor.react_schema.v1")
        expect("9. reactSchemaCandidate.surface == auth.external.credential_management.projection", schema_candidate.get("surface") == "auth.external.credential_management.projection")

        root = schema_candidate.get("root") or {}
        category_keys = collect_keys_by_kind(root, "Category")
        expect("10. reactSchemaCandidate contains categories user_auth/external/instance_settings", set(["user_auth", "external", "instance_settings"]).issubset(set(category_keys)))

        form_keys = set(collect_keys_by_kind(root, "Form"))
        expected_forms = {"instance_settings_import_form", "instance_address_form", "instance_operation_binding_form", "instance_operation_approval_form"}
        expect("11. reactSchemaCandidate contains the four instance_settings forms", expected_forms.issubset(form_keys))

        action_keys = set(collect_keys_by_kind(root, "Action"))
        expected_actions = {"json_template_download", "json_import", "validate", "preview", "apply", "approve"}
        expect("12. reactSchemaCandidate contains the six instance_settings actions", expected_actions.issubset(action_keys))

        lanes = collect_wiring_lanes(root)
        expect("13. reactSchemaCandidate uses external_instance_wiring and internal_instance_wiring lanes", {"external_instance_wiring", "internal_instance_wiring"}.issubset(lanes))

        translated_text = out_path.read_text(encoding="utf-8")
        protected_terms = ["plaintext_secret", "connection_string", "endpoint_real_value", "credential_plaintext", "private_key_material", "runtime_only_decrypted_payload", "raw_SQL", "unapproved_executable_function_or_schema_authority"]
        expect("14. protected boundary vocabulary is absent from translated.json", not any(t in translated_text for t in protected_terms))

        # 15. unknown free prose becomes Unresolved / validationErrors
        tmp15 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\nsome unrecognized free prose line\n[/projection]\n", tmpdir=tmpdir)
        _, doc15 = run_generate(tmp15)
        expect("15. unknown free prose becomes Unresolved / validationErrors", doc15 is not None and any(e.get("unitKind") == "unresolved" for e in doc15.get("normalizedInputElements", [])))

        # 16. action without wiringLane becomes blocking validationError
        tmp16 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[action key=a1 actionRef=content_bundle:search authorityMarker=validation_only sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        _, doc16 = run_generate(tmp16)
        expect("16. action without wiringLane becomes blocking validationError", "ACTION_OR_STEP_MISSING_WIRING_LANE" in rule_ids(doc16))

        # 17. invalid external_instance targetRef becomes blocking validationError
        tmp17 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[action key=a1 actionRef=not-a-valid-ref wiringLane=external_instance_wiring authorityMarker=validation_only sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        _, doc17 = run_generate(tmp17)
        expect("17. invalid external_instance targetRef becomes blocking validationError", "TARGET_REF_SHAPE_MISMATCH" in rule_ids(doc17))

        # 18. unknown componentKind without knownGapRef becomes blocking validationError
        tmp18 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[field key=f1 control=totally/unknown_kind required=false sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        _, doc18 = run_generate(tmp18)
        expect("18. unknown componentKind without knownGapRef becomes blocking validationError", "UNKNOWN_COMPONENT_KIND" in rule_ids(doc18))

        # 19. raw HTML tag emission attempt becomes blocking validationError
        tmp19 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n<script>alert(1)</script>\n[/projection]\n", tmpdir=tmpdir)
        _, doc19 = run_generate(tmp19)
        expect("19. raw HTML tag emission attempt becomes blocking validationError", "RAW_HTML_TAG_EMISSION_ATTEMPT" in rule_ids(doc19))

        # 19a. generate-react-schema rejects an envelope whose mode is not generate_react_schema
        tmp19a = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        payload19a = json.loads(tmp19a.read_text(encoding="utf-8"))
        payload19a["mode"] = "round_trip_check"
        tmp19a.write_text(json.dumps(payload19a), encoding="utf-8")
        _, doc19a = run_generate(tmp19a)
        expect("19a. generate-react-schema + envelope.mode=round_trip_check becomes blocking MODE_MISMATCH", "MODE_MISMATCH" in rule_ids(doc19a))

        # 19b. field/form/section without label becomes blocking validationError (no fallback-to-key)
        tmp19b_field = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[field key=f1 control=form_input/form_field required=false sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19b_field = run_generate(tmp19b_field)
        expect("19b. field without label becomes blocking MISSING_LABEL (no fallback-to-key)", "MISSING_LABEL" in rule_ids(doc19b_field))

        tmp19b_form = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[form key=f1 target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n[field key=fl label=lbl control=form_input/form_field required=false sourceYamlRefs=a]\n[/form]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19b_form = run_generate(tmp19b_form)
        expect("19b. form without label becomes blocking MISSING_LABEL (no fallback-to-key)", "MISSING_LABEL" in rule_ids(doc19b_form))

        tmp19b_section = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[section key=s1 sourceYamlRefs=a]\n[/section]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19b_section = run_generate(tmp19b_section)
        expect("19b. section without label becomes blocking MISSING_LABEL (no fallback-to-key)", "MISSING_LABEL" in rule_ids(doc19b_section))

        # 19c. enum-restricted targetRef placeholder rejects an out-of-enum value
        tmp19c = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[form key=f1 label=lbl target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n[action key=a1 actionRef=instance:not_allowed_port_kind:x:y wiringLane=external_instance_wiring authorityMarker=validation_only sourceYamlRefs=a]\n[/form]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19c = run_generate(tmp19c)
        expect("19c. instance:<enum> targetRef with an out-of-enum first segment becomes blocking TARGET_REF_SHAPE_MISMATCH", "TARGET_REF_SHAPE_MISMATCH" in rule_ids(doc19c))

        # 19d. Form with no Field children becomes blocking validationError
        tmp19d = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[form key=f1 label=lbl target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n[/form]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19d = run_generate(tmp19d)
        expect("19d. Form with no Field children becomes blocking EMPTY_FORM", "EMPTY_FORM" in rule_ids(doc19d))

        # 19e. Action directly under Section (not Form/Workflow) becomes blocking validationError
        tmp19e = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[section key=s1 label=lbl sourceYamlRefs=a]\n[action key=a1 actionRef=instance:db_instance_port:x:y wiringLane=external_instance_wiring authorityMarker=validation_only sourceYamlRefs=a]\n[/section]\n[/projection]\n", tmpdir=tmpdir)
        _, doc19e = run_generate(tmp19e)
        expect("19e. Action directly under Section becomes blocking ACTION_NOT_OWNED_BY_FORM_OR_WORKFLOW", "ACTION_NOT_OWNED_BY_FORM_OR_WORKFLOW" in rule_ids(doc19e))

        # 20. generate-topology-seed is now implemented (see checks 24+ below);
        # round-trip-check remains the only unimplemented mode.
        proc21 = run_tool(["round-trip-check", "--input", str(FIXTURE)])
        try:
            doc21 = json.loads(proc21.stdout)
        except json.JSONDecodeError:
            doc21 = None
        expect("21. round-trip-check returns not_implemented / out_of_scope", proc21.returncode != 0 and doc21 is not None and doc21.get("status") == "not_implemented_out_of_scope")

        # 22. tool does not require backend/frontend/nginx/DB (structural check:
        # the implementation body imports no db/http/socket client libraries)
        impl_text = (REPO_ROOT / ".agent" / "scripts" / "react_schema_topology_seed_translator.py").read_text(encoding="utf-8")
        forbidden_imports = ["psycopg2", "psycopg", "sqlalchemy", "requests", "httpx", "socket", "asyncpg"]
        expect("22. tool does not require backend/frontend/nginx/DB (no DB/network client imports)", not any(imp in impl_text for imp in forbidden_imports))

        # 23. tool does not treat db/*.sql as translator source authority: the
        # translator body's executable code (docstring and `#` comments
        # excluded, since those are exactly where the boundary is explained)
        # must contain zero ".sql" references or db/ path opens. Reading
        # db/seed_empty.sql for seedEvidence resolution is this test script's
        # job (resolve_seed_evidence_from_seed_file above), not the translator's.
        code_only_lines = []
        docstring_seen = False
        docstring_active = False
        for line in impl_text.splitlines():
            stripped = line.strip()
            if not docstring_seen and stripped.startswith('"""'):
                docstring_seen = True
                if not (stripped.count('"""') >= 2 and len(stripped) > 3):
                    docstring_active = True
                continue
            if docstring_active:
                if '"""' in stripped:
                    docstring_active = False
                continue
            if stripped.startswith("#"):
                continue
            code_only_lines.append(line)
        sql_refs_in_translator_code = [line for line in code_only_lines if ".sql" in line.lower() or "db/" in line]
        expect(
            "23. tool does not treat db/*.sql as translator source authority (zero db/*.sql references in executable code; seedEvidence is passthrough-only)",
            "translator input authority" in impl_text.lower() and not sql_refs_in_translator_code,
        )

        # --- generate-topology-seed (topology_ui_seed_contract) ---

        out_path_seed = Path(tmpdir) / "translated-seed.json"
        proc_ts, doc_ts = run_generate_topology_seed(TOPOLOGY_SEED_FIXTURE, extra_args=[
            "--output", str(out_path_seed),
            "--scenario-uuid", SCENARIO_UUID,
        ])
        expect("24. credential-management-0092 topology-seed fixture generates a topologyUiSeedCandidate", out_path_seed.is_file() and doc_ts is not None and not rule_ids(doc_ts))

        tuc = (doc_ts or {}).get("topologyUiSeedCandidate") or {}
        expect("25. topologyUiSeedCandidate.schema == topolactor.topology_ui_seed.v1", tuc.get("schema") == "topolactor.topology_ui_seed.v1")
        expect("26. topologyUiSeedCandidate.role == draft_intake_artifact_not_active_topology", tuc.get("role") == "draft_intake_artifact_not_active_topology")
        expect("27. reactSchemaCandidate is carried through verbatim for audit", (doc_ts or {}).get("reactSchemaCandidate", {}).get("schema") == "topolactor.react_schema.v1")
        expect("28. exchangeReport.outputSeedSchemaId == topolactor.topology_ui_seed.v1", dig(doc_ts or {}, "exchangeReport", "outputSeedSchemaId") == "topolactor.topology_ui_seed.v1")
        expect("29. topologyUiSeedCandidate.projections is populated", bool(tuc.get("projections")))

        proj = (tuc.get("projections") or [{}])[0]
        expect("30. root projection record recordType == topology_ui_projection and surface is set", proj.get("recordType") == "topology_ui_projection" and proj.get("surface") == "auth.external.credential_management.projection")

        category_record_keys = {c.get("categoryKey") for c in proj.get("categories") or []}
        expect("31. topology_ui_category records preserve user_auth/external/instance_settings", {"user_auth", "external", "instance_settings"}.issubset(category_record_keys))

        instance_settings_cat = next((c for c in proj.get("categories") or [] if c.get("categoryKey") == "instance_settings"), {})
        section0 = (instance_settings_cat.get("sections") or [{}])[0]
        form_records = [c for c in section0.get("children") or [] if c.get("recordType") == "topology_ui_form"]
        expect("32. topology_ui_form records preserve the four instance_settings forms", {"instance_settings_import_form", "instance_address_form", "instance_operation_binding_form", "instance_operation_approval_form"}.issubset({f.get("formKey") for f in form_records}))

        all_action_records = []

        def collect_actions(rec, acc):
            if rec.get("recordType") == "topology_ui_action":
                acc.append(rec)
            for key in ("categories", "sections", "children", "fields", "actions", "steps"):
                for c in rec.get(key) or []:
                    if isinstance(c, dict):
                        collect_actions(c, acc)

        collect_actions(proj, all_action_records)
        expect("33. topology_ui_action records preserve the six instance_settings actions", {"json_template_download", "json_import", "validate", "preview", "apply", "approve"}.issubset({a.get("actionKey") for a in all_action_records}))
        expect(
            "34. topology_ui_action records preserve identity (sourceReactPath/sourceYamlRefs/authorityMarker/eventBinding)",
            all(a.get("sourceReactPath") and a.get("sourceYamlRefs") and a.get("authorityMarker") and a.get("eventBinding") for a in all_action_records),
        )

        # 35: instance_settings_projection_category_not_yet_represented is resolved
        # (was previously asserted as a propagating unresolved gap; the fixtures no
        # longer declare it and the golden run must not report it either -- a
        # resolved gap must not keep surfacing as if still open). Cross-checked
        # against the actual seed store, not just the fixture/tool claim, so a
        # "resolved" status can't be asserted without the real representation
        # existing.
        expect(
            "35. instance_settings_projection_category_not_yet_represented no longer appears in topology-seed unresolvedGaps/exchangeReport.knownGapRefs (resolved, not silently re-surfaced)",
            "instance_settings_projection_category_not_yet_represented" not in ((doc_ts or {}).get("unresolvedGaps") or [])
            and "instance_settings_projection_category_not_yet_represented" not in (dig(doc_ts or {}, "exchangeReport", "knownGapRefs") or []),
        )
        seed_empty_text = SEED_EMPTY_PATH.read_text(encoding="utf-8")
        expect(
            "35a. the gap-resolving instance_settings forms are actually represented as topology_ui_seed_record entries in the real seed store (claim matches seed content, not just fixture/tool output)",
            all(
                f'"formKey":"{form_key}"' in seed_empty_text
                for form_key in ("instance_settings_import_form", "instance_address_form", "instance_operation_binding_form", "instance_operation_approval_form")
            ),
        )

        no_active_topology_write_claim = "activeTopology" not in json.dumps(tuc) and "runtimeExecute" not in json.dumps(tuc)
        expect("36. topologyUiSeedCandidate carries no active-topology/execution-authority claim", no_active_topology_write_claim)

        # storage_adoption_contract (added after PR #573's idx_manifest_topology
        # index row size failure): topologyUiSeedFlatRecords is the seed-safe
        # adoption shape, derived from the same tree as topologyUiSeedCandidate.
        flat_records = (doc_ts or {}).get("topologyUiSeedFlatRecords") or []
        expect("36a. topologyUiSeedFlatRecords is populated for the golden fixture", bool(flat_records))
        golden_manifest_refs = ((doc_ts or {}).get("adoptionCandidates") or {}).get("manifestRefsCandidate")
        expect(
            "36b. manifestRefsCandidate -- the only bucket actually adopted into manifest.topology -- "
            "fits the idx_manifest_topology GIN index storage budget (UTF-8 byte length) for the golden "
            "fixture; layoutAdoptionCandidates/wiringAdoptionCandidates/tensorAdoptionCandidates-bound "
            "flat records are not subject to this budget at all (see storage_adoption_contract."
            "target_storage.applies_only_to_note) and may legitimately exceed it",
            bool(golden_manifest_refs) and record_byte_size(golden_manifest_refs) <= 2712,
        )
        flat_keys = {r.get("record", {}).get("key") for r in flat_records}
        root_flat = [r for r in flat_records if r.get("parentKey") is None]
        expect("36c. exactly one flattened record has parentKey == null (the root projection)", len(root_flat) == 1 and root_flat[0].get("record", {}).get("recordType") == "topology_ui_projection")
        expect(
            "36d. every non-root flattened record's parentKey resolves to another flattened record's key (parentKey tree is fully reconstructible)",
            all(r.get("parentKey") in flat_keys for r in flat_records if r.get("parentKey") is not None),
        )
        expect(
            "36e. flattened action records still preserve authorityMarker/eventBinding after flattening",
            all(
                r.get("record", {}).get("authorityMarker") and r.get("record", {}).get("eventBinding")
                for r in flat_records
                if r.get("record", {}).get("recordType") == "topology_ui_action"
            ),
        )

        # 36k-36q: storage_adoption_contract.adoption_candidate_separation_contract
        # -- the actual seed-safe adoption shape built from flat_records above.
        adoption = (doc_ts or {}).get("adoptionCandidates") or {}
        expect("36k. adoptionCandidates is populated for the golden fixture", bool(adoption))
        expect("36l. packageAdoptionCandidates has exactly one entry (one Projection record)", len(adoption.get("packageAdoptionCandidates") or []) == 1)
        expect(
            "36l1. packageAdoptionCandidates shape matches topology.components_package_design (packageKey + layout array), never the ui_component_package shape",
            all("packageKey" in c and "layout" in c and isinstance(c["layout"], list) for c in adoption.get("packageAdoptionCandidates") or []),
        )
        expect(
            "36l2. componentGroupBundleAdoptionCandidates is a distinct bucket (topology.ui_component_package, tensor-FK-only) with a key different from packageAdoptionCandidates",
            len(adoption.get("componentGroupBundleAdoptionCandidates") or []) == 1
            and (adoption["componentGroupBundleAdoptionCandidates"][0].get("componentGroupBundleKey") != (adoption.get("packageAdoptionCandidates") or [{}])[0].get("packageKey")),
        )
        expect("36m. layoutAdoptionCandidates is populated (category/section/form/field tree)", bool(adoption.get("layoutAdoptionCandidates")))
        layout_records_list = dig(adoption, "layoutAdoptionCandidates") or []
        layout_tree_records = dig(layout_records_list[0], "layoutSchemaJson", "records") if layout_records_list else []
        expect(
            "36m1. layoutAdoptionCandidates' layout tree carries the topology_ui_action records too (PRIMARY storage bucket per primary_and_derived_candidate_relationship, not only wiring/tensor derived candidates)",
            any(dig(w, "record", "recordType") == "topology_ui_action" for w in layout_tree_records or []),
        )
        wiring_candidates_list = adoption.get("wiringAdoptionCandidates") or []
        wiring_actions = dig(wiring_candidates_list[0], "wiringSchemaJson", "actions") if wiring_candidates_list else []
        expect(
            "36n. wiringAdoptionCandidates is ONE aggregate entry per Projection (never one per Action/Step -- cardinality_note) whose wiringSchemaJson.actions[] preserves all six instance_settings actions",
            len(wiring_candidates_list) == 1
            and {"json_template_download", "json_import", "validate", "preview", "apply", "approve"}.issubset(
                {a.get("sourceRecordKey") for a in wiring_actions or []}
            ),
        )
        tensor_action_keys = set()
        for tensor in adoption.get("tensorAdoptionCandidates") or []:
            for node in dig(tensor, "layoutPatchJson", "nodes") or []:
                for interaction in node.get("runtimeInteractions") or []:
                    if interaction.get("sourceActionKey"):
                        tensor_action_keys.add(interaction["sourceActionKey"])
        expect(
            "36o. every wiring candidate's action is reachable from a tensorAdoptionCandidates runtimeInteractions[] entry (runtime_interactions_not_persisted_layout_path is clean on the golden fixture)",
            {"validate", "preview", "apply", "approve"}.issubset(tensor_action_keys),
        )
        expect(
            "36p. tensor runtimeInteractions entries carry idempotency route fields (trigger/actionType/instanceTargetRef/payloadFrom) and never runtimeInteractionId",
            all(
                interaction.get("trigger") and interaction.get("actionType") and interaction.get("instanceTargetRef") and "payloadFrom" in interaction and "runtimeInteractionId" not in interaction
                for tensor in adoption.get("tensorAdoptionCandidates") or []
                for node in dig(tensor, "layoutPatchJson", "nodes") or []
                for interaction in node.get("runtimeInteractions") or []
                if interaction.get("actionType") == "dispatchInstanceOperation"
            ),
        )
        manifest_refs = adoption.get("manifestRefsCandidate") or {}
        expect(
            "36q. manifestRefsCandidate carries only ref/vector fields (type/packageIds/layoutId/wiringId/tensorId), never record/fields/actions/categories",
            manifest_refs.get("type") == "ui_projection"
            and not ({"record", "fields", "actions", "columns", "sections", "categories"} & manifest_refs.keys())
            and manifest_refs.get("packageIds") and manifest_refs.get("layoutId") and manifest_refs.get("wiringId") and manifest_refs.get("tensorId"),
        )
        component_group_bundle_keys = {f"<{c.get('componentGroupBundleKey')}>" for c in adoption.get("componentGroupBundleAdoptionCandidates") or []}
        package_keys = {f"<{c.get('packageKey')}>" for c in adoption.get("packageAdoptionCandidates") or []}
        expect(
            "36q1. manifestRefsCandidate.packageIds references packageAdoptionCandidates (topology.components_package_design) keys, never componentGroupBundleAdoptionCandidates (topology.ui_component_package) keys",
            bool(manifest_refs.get("packageIds"))
            and set(manifest_refs["packageIds"]).issubset(package_keys)
            and not (set(manifest_refs["packageIds"]) & component_group_bundle_keys),
        )
        expect(
            "36q2. tensorAdoptionCandidates packageIdRef references componentGroupBundleAdoptionCandidates (topology.ui_component_package) keys, never packageAdoptionCandidates (topology.components_package_design) keys",
            all(
                t.get("packageIdRef") in component_group_bundle_keys and t.get("packageIdRef") not in package_keys
                for t in adoption.get("tensorAdoptionCandidates") or []
            ),
        )
        layout_keys_36q3 = {f"<{c.get('layoutKey')}>" for c in adoption.get("layoutAdoptionCandidates") or []}
        wiring_keys_36q3 = {f"<{c.get('wiringKey')}>" for c in wiring_candidates_list}
        tensor_keys_36q3 = {f"<{c.get('tensorKey')}>" for c in adoption.get("tensorAdoptionCandidates") or []}
        expect(
            "36q3. manifestRefsCandidate's packageIds/layoutId/wiringId/tensorId each resolve to an actually-emitted candidate key in the matching bucket (manifest_refs_candidate_reference_resolution -- refs-only is not by itself proof the refs are wired correctly; PR580 review finding was exactly a wiringId that resolved to nothing)",
            set(manifest_refs.get("packageIds") or []).issubset(package_keys)
            and manifest_refs.get("layoutId") in layout_keys_36q3
            and manifest_refs.get("wiringId") in wiring_keys_36q3
            and manifest_refs.get("tensorId") in tensor_keys_36q3,
        )
        expect(
            "36r. the eight new top_ssot_violation rule ids do not fire on the golden fixture (clean positive path)",
            not (rule_ids(doc_ts) and set(rule_ids(doc_ts)) & {
                "MANIFEST_TOPOLOGY_CONTAINS_UI_PAYLOAD_MATERIAL", "FLATTENED_SEED_RECORD_USED_AS_MANIFEST_FINAL_SHAPE",
                "UI_PAYLOAD_NOT_SPLIT_TO_PACKAGE_LAYOUT_DESIGN_WIRING_TENSOR", "RUNTIME_INTERACTIONS_NOT_PERSISTED_LAYOUT_PATH",
                "IDEMPOTENCY_CARRIER_MISSING_FOR_RUNTIME_DISPATCH", "CREDENTIAL_SECRET_PROJECTION_DETECTED",
                "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH", "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED",
            }),
        )

        # 36r1: cross-file proof that the SSOT's documented package authority target
        # actually agrees with docs/design/db-schema.yaml -- the real "DB design
        # authority" this whole fix exists to track, read independently of the
        # translator's own self-reported behavior above (which could pass even if
        # both files drifted the same wrong way together).
        db_schema_text = (REPO_ROOT / "docs" / "design" / "db-schema.yaml").read_text(encoding="utf-8")
        translator_ssot_text = (REPO_ROOT / "docs" / "design" / "react-schema-topology-seed-translator-ssot.yaml").read_text(encoding="utf-8")
        packages_block_match = re.search(r"\n    packages:\n(?:.+\n)+?      manifest_reference: (\S+)", db_schema_text)
        expect(
            "36r2. docs/design/db-schema.yaml packages entry declares manifest_reference: manifest.topology[ui_projection].packageIds (the DB design authority this fix tracks)",
            packages_block_match is not None and packages_block_match.group(1) == "manifest.topology[ui_projection].packageIds",
        )
        package_target_table_match = re.search(r"packageAdoptionCandidates:\n\s+target_table: (\S+)", translator_ssot_text)
        expect(
            "36r3. translator SSOT packageAdoptionCandidates.target_table agrees with docs/design/db-schema.yaml's manifest-facing package authority (topology.components_package_design, not topology.ui_component_package)",
            package_target_table_match is not None and package_target_table_match.group(1) == "topology.components_package_design",
        )

        # 36s-36y: unit-level negative-path proof that validate_adoption_candidates
        # actually detects each of the six top_ssot_violation_rule_definitions --
        # these are translator-self-consistency guards a well-formed CLI input
        # cannot organically trigger (the translator itself never emits a bad
        # manifestRefsCandidate), so they are exercised by importing the
        # implementation module directly and feeding it hand-built candidate
        # bundles, per this file's role as the structured-assertion layer.
        sys.path.insert(0, str(REPO_ROOT / ".agent" / "scripts"))
        import react_schema_topology_seed_translator as translator_impl  # noqa: E402

        bad_manifest_refs_payload = translator_impl.validate_adoption_candidates(
            {"manifestRefsCandidate": {"type": "ui_projection", "packageIds": ["x"], "record": {"key": "leak"}}},
            [],
        )
        expect("36s. manifest_topology_contains_ui_payload_material fires when manifestRefsCandidate carries a `record` key", "MANIFEST_TOPOLOGY_CONTAINS_UI_PAYLOAD_MATERIAL" in [e["ruleId"] for e in bad_manifest_refs_payload])

        bad_manifest_refs_type = translator_impl.validate_adoption_candidates(
            {"manifestRefsCandidate": {"type": "topology_ui_seed_record", "packageIds": []}},
            [],
        )
        expect("36t. flattened_seed_record_used_as_manifest_final_shape fires when manifestRefsCandidate.type == topology_ui_seed_record", "FLATTENED_SEED_RECORD_USED_AS_MANIFEST_FINAL_SHAPE" in [e["ruleId"] for e in bad_manifest_refs_type])

        unsplit_errors = translator_impl.validate_adoption_candidates(
            {"packageAdoptionCandidates": [], "layoutAdoptionCandidates": [], "wiringAdoptionCandidates": [], "tensorAdoptionCandidates": []},
            [{"record": {"recordType": "topology_ui_field", "key": "orphan"}}],
        )
        expect("36u. ui_payload_not_split_to_package_layout_design_wiring_tensor fires when flat_records is non-empty but every candidate bucket is empty", "UI_PAYLOAD_NOT_SPLIT_TO_PACKAGE_LAYOUT_DESIGN_WIRING_TENSOR" in [e["ruleId"] for e in unsplit_errors])

        orphan_dispatch_action = {
            "record": {
                "recordType": "topology_ui_action", "key": "orphan_action", "sourceReactPath": "$.root.x",
                "eventBinding": {"wiringLane": "external_instance_wiring", "targetRef": "instance:db_instance_port:a:b"},
                "runtimeInteractions": [{"actionType": "dispatchInstanceOperation", "sourceActionKey": "orphan_action"}],
            },
        }
        orphan_errors = translator_impl.validate_adoption_candidates({"tensorAdoptionCandidates": []}, [orphan_dispatch_action])
        orphan_rule_ids = [e["ruleId"] for e in orphan_errors]
        expect("36v. runtime_interactions_not_persisted_layout_path fires when a dispatch-lane action has no tensorAdoptionCandidates runtimeInteractions[] entry", "RUNTIME_INTERACTIONS_NOT_PERSISTED_LAYOUT_PATH" in orphan_rule_ids)
        expect("36w. idempotency_carrier_missing_for_runtime_dispatch fires when a dispatchInstanceOperation candidate is missing instanceTargetRef/payloadFrom", "IDEMPOTENCY_CARRIER_MISSING_FOR_RUNTIME_DISPATCH" in orphan_rule_ids)

        stray_id_action = {
            "record": {
                "recordType": "topology_ui_action", "key": "stray_id_action", "sourceReactPath": "$.root.y",
                "eventBinding": {"wiringLane": "internal_instance_wiring"},
                "runtimeInteractions": [{"actionType": "localStateMutation", "runtimeInteractionId": "should-not-be-here"}],
            },
        }
        stray_id_errors = translator_impl.validate_adoption_candidates({"tensorAdoptionCandidates": []}, [stray_id_action])
        expect("36x. idempotency_carrier_missing_for_runtime_dispatch fires when a runtimeInteractions candidate carries runtimeInteractionId (translator must never generate one)", "IDEMPOTENCY_CARRIER_MISSING_FOR_RUNTIME_DISPATCH" in [e["ruleId"] for e in stray_id_errors])

        secret_leak_errors = translator_impl.validate_adoption_candidates(
            {"wiringAdoptionCandidates": [{"wiringKey": "leak", "wiringSchemaJson": {"password_hash": "should-never-appear"}}]},
            [],
        )
        expect("36y. credential_secret_projection_detected fires when an adoption candidate carries a password_hash key", "CREDENTIAL_SECRET_PROJECTION_DETECTED" in [e["ruleId"] for e in secret_leak_errors])

        # forbidden-field guard-list declarations (documentation, not leakage)
        # must NOT false-positive credential_secret_projection_detected.
        guard_list_errors = translator_impl.validate_adoption_candidates(
            {"wiringAdoptionCandidates": [{"wiringKey": "policy", "wiringSchemaJson": {"forbidden_fields": ["secret", "plaintext_secret", "token"]}}]},
            [],
        )
        expect("36z. credential_secret_projection_detected does NOT false-positive on a forbidden_fields guard-list array (values, not a denylisted key)", "CREDENTIAL_SECRET_PROJECTION_DETECTED" not in [e["ruleId"] for e in guard_list_errors])

        # 36z1-36z3: package_authority_target_table_mismatch -- the rule added for
        # the ui_component_package / components_package_design conflation this
        # section exists to prevent from silently reproducing.
        swapped_manifest_refs_errors = translator_impl.validate_adoption_candidates(
            {
                "manifestRefsCandidate": {"type": "ui_projection", "packageIds": ["<seed.component_group_bundle>"]},
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "componentGroupBundleAdoptionCandidates": [{"componentGroupBundleKey": "seed.component_group_bundle"}],
            },
            [],
        )
        expect(
            "36z1. package_authority_target_table_mismatch fires when manifestRefsCandidate.packageIds references a componentGroupBundleAdoptionCandidates key",
            "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH" in [e["ruleId"] for e in swapped_manifest_refs_errors],
        )

        swapped_tensor_ref_errors = translator_impl.validate_adoption_candidates(
            {
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "componentGroupBundleAdoptionCandidates": [{"componentGroupBundleKey": "seed.component_group_bundle"}],
                "tensorAdoptionCandidates": [{"tensorKey": "seed.tensor", "packageIdRef": "<seed.package>", "layoutPatchJson": {"nodes": []}}],
            },
            [],
        )
        expect(
            "36z2. package_authority_target_table_mismatch fires when tensorAdoptionCandidates.packageIdRef references a packageAdoptionCandidates key",
            "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH" in [e["ruleId"] for e in swapped_tensor_ref_errors],
        )

        correctly_wired_errors = translator_impl.validate_adoption_candidates(
            {
                "manifestRefsCandidate": {"type": "ui_projection", "packageIds": ["<seed.package>"]},
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "componentGroupBundleAdoptionCandidates": [{"componentGroupBundleKey": "seed.component_group_bundle"}],
                "tensorAdoptionCandidates": [{"tensorKey": "seed.tensor", "packageIdRef": "<seed.component_group_bundle>", "layoutPatchJson": {"nodes": []}}],
            },
            [],
        )
        expect(
            "36z3. package_authority_target_table_mismatch does NOT false-positive when packageIds/packageIdRef are wired to the correct respective buckets",
            "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH" not in [e["ruleId"] for e in correctly_wired_errors],
        )

        # 36z4-36z6: manifest_refs_candidate_reference_resolution -- reproduces the
        # exact PR580 review finding (an earlier translator version emitted one
        # wiringAdoptionCandidates entry per Action/Step but a single
        # aggregate-style wiringId that matched none of them) as a unit-level
        # negative-path proof, plus a clean-resolution positive-path proof so the
        # rule cannot false-positive on correctly-wired candidates.
        unresolved_wiring_errors = translator_impl.validate_adoption_candidates(
            {
                "manifestRefsCandidate": {
                    "type": "ui_projection",
                    "packageIds": ["<seed.package>"],
                    "layoutId": "<seed.layout>",
                    "wiringId": "<seed.wiring>",
                    "tensorId": "<seed.tensor>",
                },
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "layoutAdoptionCandidates": [{"layoutKey": "seed.layout"}],
                # Old, incorrect per-Action shape: no candidate actually carries the
                # aggregate key "seed.wiring" the manifestRefsCandidate references.
                "wiringAdoptionCandidates": [{"wiringKey": "seed.validate.wiring"}, {"wiringKey": "seed.approve.wiring"}],
                "tensorAdoptionCandidates": [{"tensorKey": "seed.tensor"}],
            },
            [],
        )
        expect(
            "36z4. manifest_refs_candidate_reference_resolution fires when manifestRefsCandidate.wiringId does not match any emitted wiringAdoptionCandidates[].wiringKey (the exact PR580 review finding)",
            "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED" in [e["ruleId"] for e in unresolved_wiring_errors],
        )

        unresolved_layout_tensor_errors = translator_impl.validate_adoption_candidates(
            {
                "manifestRefsCandidate": {
                    "type": "ui_projection",
                    "packageIds": ["<seed.package>"],
                    "layoutId": "<seed.wrong_layout>",
                    "wiringId": "<seed.wiring>",
                    "tensorId": "<seed.wrong_tensor>",
                },
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "layoutAdoptionCandidates": [{"layoutKey": "seed.layout"}],
                "wiringAdoptionCandidates": [{"wiringKey": "seed.wiring"}],
                "tensorAdoptionCandidates": [{"tensorKey": "seed.tensor"}],
            },
            [],
        )
        expect(
            "36z5. manifest_refs_candidate_reference_resolution fires independently for an unresolved layoutId and an unresolved tensorId in the same candidates bundle (collects every violation, not just the first)",
            {"$.adoptionCandidates.manifestRefsCandidate.layoutId", "$.adoptionCandidates.manifestRefsCandidate.tensorId"}.issubset(
                {e["path"] for e in unresolved_layout_tensor_errors if e["ruleId"] == "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED"}
            ),
        )

        resolved_refs_errors = translator_impl.validate_adoption_candidates(
            {
                "manifestRefsCandidate": {
                    "type": "ui_projection",
                    "packageIds": ["<seed.package>"],
                    "layoutId": "<seed.layout>",
                    "wiringId": "<seed.wiring>",
                    "tensorId": "<seed.tensor>",
                },
                "packageAdoptionCandidates": [{"packageKey": "seed.package", "layout": []}],
                "layoutAdoptionCandidates": [{"layoutKey": "seed.layout"}],
                "wiringAdoptionCandidates": [{"wiringKey": "seed.wiring", "wiringSchemaJson": {"actions": [{"wiringKey": "seed.validate.wiring"}, {"wiringKey": "seed.approve.wiring"}]}}],
                "tensorAdoptionCandidates": [{"tensorKey": "seed.tensor"}],
            },
            [],
        )
        expect(
            "36z6. manifest_refs_candidate_reference_resolution does NOT false-positive when every ref (packageIds/layoutId/wiringId/tensorId) resolves to its matching bucket's actually-emitted key, including the aggregate wiringId shape",
            "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED" not in [e["ruleId"] for e in resolved_refs_errors],
        )

        # 36z6a-36z6b: parent_scoped_identity_reconstruction (docs/design/
        # runtime-orchestration-ssot.yaml ui_projection_render_reachability_contract.
        # layout_schema_structural_render_contract) -- split_flat_records_into_adoption_candidates
        # keys each Action/Step's own tensorAdoptionCandidates node by ITS OWN disambiguated
        # (parentKey-namespaced when duplicated) resolved identity (UIBuilder-lineage closure
        # round: never an owning-Form-keyed redirect -- see that branch's own doc comment in
        # react_schema_topology_seed_translator.py for why a real live-DB layout_patch:validate
        # round trip ruled that shape out), so two different Form instances that happen to share
        # an authored key never merge their actions' runtimeInteractions into the same tensor node.
        duplicate_form_key_flat_records = [
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "cat_a", "record": {
                "recordType": "topology_ui_form", "key": "shared_form", "label": "Form A",
                "target": "t", "mode": "edit", "authorityMarker": "validation_only",
                "fieldKeys": [], "actionKeys": ["act_a"],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "shared_form", "record": {
                "recordType": "topology_ui_action", "key": "act_a", "label": "Act A",
                "actionRef": "instance:a", "authorityMarker": "validation_only",
                "eventBinding": {"trigger": "click", "wiringLane": "external_instance_wiring", "targetRef": "instance:a"},
                "runtimeInteractions": [{"trigger": "click", "actionType": "dispatchInstanceOperation", "sourceActionKey": "act_a", "instanceTargetRef": "A"}],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "cat_b", "record": {
                "recordType": "topology_ui_form", "key": "shared_form", "label": "Form B",
                "target": "t", "mode": "edit", "authorityMarker": "validation_only",
                "fieldKeys": [], "actionKeys": ["act_b"],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "shared_form", "record": {
                "recordType": "topology_ui_action", "key": "act_b", "label": "Act B",
                "actionRef": "instance:b", "authorityMarker": "validation_only",
                "eventBinding": {"trigger": "click", "wiringLane": "external_instance_wiring", "targetRef": "instance:b"},
                "runtimeInteractions": [{"trigger": "click", "actionType": "dispatchInstanceOperation", "sourceActionKey": "act_b", "instanceTargetRef": "B"}],
            }},
        ]
        duplicate_form_key_adoption = translator_impl.split_flat_records_into_adoption_candidates(
            duplicate_form_key_flat_records, "seed",
        )
        duplicate_form_key_tensor_nodes = dig(
            duplicate_form_key_adoption["tensorAdoptionCandidates"][0], "layoutPatchJson", "nodes",
        ) or []
        expect(
            "36z6a. two Form instances sharing an authored key produce TWO separate leaf-keyed tensorAdoptionCandidates nodes (one per Action, at its OWN resolved identity), not one merged node",
            len(duplicate_form_key_tensor_nodes) == 2,
        )
        node_a = next((n for n in duplicate_form_key_tensor_nodes if n["nodeId"] == "act_a"), None)
        node_b = next((n for n in duplicate_form_key_tensor_nodes if n["nodeId"] == "act_b"), None)
        expect(
            "36z6b. each Action's own tensor node carries ONLY its own runtimeInteractions (no cross-contamination) and a resolved componentKey (button.primitive)",
            node_a is not None and node_b is not None
            and node_a["componentKey"] == "button.primitive" and node_b["componentKey"] == "button.primitive"
            and len(node_a["runtimeInteractions"]) == 1 and node_a["runtimeInteractions"][0]["sourceActionKey"] == "act_a"
            and len(node_b["runtimeInteractions"]) == 1 and node_b["runtimeInteractions"][0]["sourceActionKey"] == "act_b",
        )

        # 36z6c: parent_scoped_identity_reconstruction, COMBINED case -- both the owning Form's
        # key AND the Action's own key are duplicated across branches at the same time (the case
        # neither 36z6a/36z6b alone covers, since there the Action keys act_a/act_b are distinct).
        # resolve_and_track_identity must namespace the duplicated Action key by the Form's
        # RESOLVED identity, never the raw parentKey string alone -- a raw-parentKey namespace
        # would collide to the SAME "{sharedFormKey}::{sharedActionKey}" string in both branches.
        duplicate_parent_and_child_flat_records = [
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "cat_a", "record": {
                "recordType": "topology_ui_form", "key": "shared_form", "label": "Form A",
                "target": "t", "mode": "edit", "authorityMarker": "validation_only",
                "fieldKeys": [], "actionKeys": ["shared_action"],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "shared_form", "record": {
                "recordType": "topology_ui_action", "key": "shared_action", "label": "Act A",
                "actionRef": "instance:a", "authorityMarker": "validation_only",
                "eventBinding": {"trigger": "click", "wiringLane": "external_instance_wiring", "targetRef": "instance:a"},
                "runtimeInteractions": [{"trigger": "click", "actionType": "dispatchInstanceOperation", "sourceActionKey": "shared_action", "instanceTargetRef": "A"}],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "cat_b", "record": {
                "recordType": "topology_ui_form", "key": "shared_form", "label": "Form B",
                "target": "t", "mode": "edit", "authorityMarker": "validation_only",
                "fieldKeys": [], "actionKeys": ["shared_action"],
            }},
            {"type": "topology_ui_seed_record", "seedKey": "seed", "parentKey": "shared_form", "record": {
                "recordType": "topology_ui_action", "key": "shared_action", "label": "Act B",
                "actionRef": "instance:b", "authorityMarker": "validation_only",
                "eventBinding": {"trigger": "click", "wiringLane": "external_instance_wiring", "targetRef": "instance:b"},
                "runtimeInteractions": [{"trigger": "click", "actionType": "dispatchInstanceOperation", "sourceActionKey": "shared_action", "instanceTargetRef": "B"}],
            }},
        ]
        duplicate_parent_and_child_adoption = translator_impl.split_flat_records_into_adoption_candidates(
            duplicate_parent_and_child_flat_records, "seed",
        )
        duplicate_parent_and_child_tensor_nodes = dig(
            duplicate_parent_and_child_adoption["tensorAdoptionCandidates"][0], "layoutPatchJson", "nodes",
        ) or []
        expect(
            "36z6c. duplicate parent (Form) key AND duplicate child (Action) key simultaneously still produce TWO separate leaf-keyed tensorAdoptionCandidates nodes, not one merged node",
            len(duplicate_parent_and_child_tensor_nodes) == 2,
        )
        combined_node_a = next((n for n in duplicate_parent_and_child_tensor_nodes if n["nodeId"] == "cat_a::shared_form::shared_action"), None)
        combined_node_b = next((n for n in duplicate_parent_and_child_tensor_nodes if n["nodeId"] == "cat_b::shared_form::shared_action"), None)
        expect(
            "36z6d. each duplicate-keyed Action's OWN tensor node is namespaced by ITS OWN resolved Form parent (never colliding with the other instance) and carries only its own runtimeInteractions, even when the action key is also duplicated",
            combined_node_a is not None and combined_node_b is not None
            and len(combined_node_a["runtimeInteractions"]) == 1 and combined_node_a["runtimeInteractions"][0]["instanceTargetRef"] == "A"
            and len(combined_node_b["runtimeInteractions"]) == 1 and combined_node_b["runtimeInteractions"][0]["instanceTargetRef"] == "B",
        )

        # 36z7: grep guard against the retired "one row per Action record" wiring
        # cardinality wording re-appearing in docs after the Blocking 2 fix above
        # (PR580 review follow-up) -- the correct wording is one aggregate row
        # per Projection with wiringSchemaJson.actions[]/wiring_schema_json.actions[].
        wiring_cardinality_doc_targets = [
            REPO_ROOT / "docs" / "projection_design" / "credential-management-projection-design.md",
            REPO_ROOT / "docs" / "design" / "react-schema-topology-seed-translator-ssot.yaml",
        ]
        wiring_cardinality_doc_text = "\n".join(p.read_text(encoding="utf-8") for p in wiring_cardinality_doc_targets)
        expect(
            "36z7. no doc reintroduces the retired \"one row per Action record\" wiring cardinality wording (must read one aggregate row per Projection instead)",
            "one row per Action record" not in wiring_cardinality_doc_text
            and "one candidate per Action/Step record carrying eventBinding" not in wiring_cardinality_doc_text,
        )

        # 36f-36h (structural-subtree-conditional-visibility-implementation round
        # correction): a large single-field candidate whose record lands in
        # layoutAdoptionCandidates (components_layout_design.layout_schema_json, a
        # scalar JSONB column with no GIN index at all -- see
        # storage_adoption_contract.target_storage.applies_only_to_note) must NOT be
        # blocked by SEED_RECORD_EXCEEDS_STORAGE_BUDGET, since that record is never
        # proposed for manifest.topology adoption. This is the corrected behavior:
        # an earlier version of this budget check applied the manifest.topology /
        # idx_manifest_topology GIN per-element ceiling to every flattened record
        # regardless of its real adoption target, which produced false-positive
        # blocking failures for legitimately large, correctly-routed
        # layoutAdoptionCandidates-bound records (the exact gap that stalled
        # credential-management manifest 092's own source-fixture regeneration).
        oversize_schema = {
            "schema": "topolactor.react_schema.v1",
            "presetKey": "auth.external.credential_management.projection",
            "surface": "auth.external.credential_management.projection",
            "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
            "root": {
                "kind": "Projection", "key": "p", "label": "p", "sourceYamlRefs": ["a"],
                "children": [{
                    "kind": "Category", "key": "c1", "label": "c1", "sourceYamlRefs": ["a"],
                    "children": [{
                        "kind": "Section", "key": "s1", "label": "s1", "sourceYamlRefs": ["a"], "sectionKind": "readonly_boundary",
                        "children": [{
                            "kind": "Field", "key": "f1", "label": "x" * 3000, "sourceYamlRefs": ["a"],
                            "control": "form_input/form_field", "required": False,
                        }],
                    }],
                }],
            },
        }
        tmp36 = write_topology_seed_tmp_fixture(json.dumps(oversize_schema), tmpdir=tmpdir)
        oversize_fragment_path = Path(tmpdir) / "oversize-fragment.sql"
        proc36, doc36 = run_generate_topology_seed(tmp36, extra_args=["--seed-sql-fragment", str(oversize_fragment_path)])
        expect(
            "36f. a large layoutAdoptionCandidates-bound flattened field does NOT block generation "
            "with SEED_RECORD_EXCEEDS_STORAGE_BUDGET (it is never adopted into manifest.topology, so "
            "the GIN index budget does not apply to it)",
            proc36.returncode == 0 and "SEED_RECORD_EXCEEDS_STORAGE_BUDGET" not in rule_ids(doc36),
        )
        expect("36g. topologyUiSeedFlatRecords is still populated for review", bool((doc36 or {}).get("topologyUiSeedFlatRecords")))
        expect("36h. --seed-sql-fragment IS written for this large-but-correctly-routed record (nothing here is manifest.topology-bound or over its budget)", oversize_fragment_path.exists())

        # 36i-36j (unit-level, corrected target): the budget must be measured in
        # UTF-8 *bytes*, not Python characters, but ONLY applies to
        # manifestRefsCandidate now -- exercised directly against
        # validate_flat_seed_records since the CLI's own manifestRefsCandidate
        # (packageIds/layoutId/wiringId/tensorId only) cannot organically grow
        # large enough to hit 2712 bytes from ordinary React-like Schema input.
        # 900 "あ" (3 bytes each in UTF-8) keeps a synthetic manifestRefsCandidate
        # at ~1.2k characters (would pass a naive len(str) check) but ~2.8k bytes
        # (must fail the real byte-length check).
        oversized_manifest_refs_multibyte = {
            "type": "ui_projection",
            "packageIds": ["あ" * 900],
            "layoutId": "<x.layout>",
            "wiringId": "<x.wiring>",
            "tensorId": "<x.tensor>",
        }
        multibyte_over_errors = translator_impl.validate_flat_seed_records(oversized_manifest_refs_multibyte)
        expect(
            "36i. a multi-byte manifestRefsCandidate under 2712 *characters* but over 2712 UTF-8 "
            "*bytes* is still caught as blocking SEED_RECORD_EXCEEDS_STORAGE_BUDGET",
            "SEED_RECORD_EXCEEDS_STORAGE_BUDGET" in [e["ruleId"] for e in multibyte_over_errors]
            and len(json.dumps(oversized_manifest_refs_multibyte, separators=(",", ":"), ensure_ascii=False)) <= 2712
            and record_byte_size(oversized_manifest_refs_multibyte) > 2712,
        )

        oversized_manifest_refs_multibyte_ok = {
            "type": "ui_projection",
            "packageIds": ["あ" * 600],
            "layoutId": "<x.layout>",
            "wiringId": "<x.wiring>",
            "tensorId": "<x.tensor>",
        }
        multibyte_ok_errors = translator_impl.validate_flat_seed_records(oversized_manifest_refs_multibyte_ok)
        expect(
            "36j. a smaller multi-byte manifestRefsCandidate that stays under the byte budget is not "
            "flagged (byte check is not overly conservative)",
            "SEED_RECORD_EXCEEDS_STORAGE_BUDGET" not in [e["ruleId"] for e in multibyte_ok_errors],
        )

        # 36j1: wiring proof that validate_flat_seed_records is actually called
        # against adoptionCandidates["manifestRefsCandidate"] (post-separation),
        # never against the pre-separation topologyUiSeedFlatRecords list -- a
        # real manifestRefsCandidate can never organically grow past 2712 bytes
        # from ordinary React-like Schema input (packageIds always holds exactly
        # one entry -- one packageAdoptionCandidates row per Projection root,
        # never one per Category -- and every field is a short
        # "<seedKey.suffix>" template string), so this call-site wiring is
        # checked structurally rather than forcing an artificial end-to-end
        # overflow through the CLI.
        translator_src_for_budget_wiring = (REPO_ROOT / ".agent" / "scripts" / "react_schema_topology_seed_translator.py").read_text(encoding="utf-8")
        expect(
            "36j1. validate_flat_seed_records is wired against adoptionCandidates.manifestRefsCandidate, not the pre-separation flat_records list",
            'validate_flat_seed_records(adoption_candidates.get("manifestRefsCandidate"))' in translator_src_for_budget_wiring,
        )

        # 37. generate-react-schema envelope rejected by generate-topology-seed (mode mismatch)
        tmp37 = write_tmp_fixture("[projection key=p label=x sourceYamlRefs=a]\n[/projection]\n", tmpdir=tmpdir)
        _, doc37 = run_generate_topology_seed(tmp37)
        expect("37. generate-topology-seed + envelope.mode=generate_react_schema becomes blocking MODE_MISMATCH", "MODE_MISMATCH" in rule_ids(doc37))

        # 38. non-JSON inputText becomes blocking validationError, not a crash
        tmp38 = write_topology_seed_tmp_fixture("this is not json", tmpdir=tmpdir)
        proc38, doc38 = run_generate_topology_seed(tmp38)
        expect("38. non-JSON inputText becomes blocking INPUT_TEXT_NOT_VALID_JSON (no crash)", doc38 is not None and "INPUT_TEXT_NOT_VALID_JSON" in rule_ids(doc38))

        # 39. a react schema candidate with an unmapped node kind produces a loss entry and a blocking error
        broken_schema = json.loads(json.loads(TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"))["inputText"])
        broken_schema["root"]["kind"] = "NotARealKind"
        tmp39 = write_topology_seed_tmp_fixture(json.dumps(broken_schema), tmpdir=tmpdir)
        _, doc39 = run_generate_topology_seed(tmp39)
        expect("39. unmapped react schema node kind becomes blocking REACT_NODE_KIND_UNMAPPED and a lossEntries entry", "REACT_NODE_KIND_UNMAPPED" in rule_ids(doc39) and bool(dig(doc39 or {}, "exchangeReport", "lossEntries")))

        # 40. generate-topology-seed re-validates the supplied schema (doesn't trust it blindly):
        # an Action injected directly under a Section (not Form/Workflow) must still be rejected.
        tampered_schema = json.loads(json.loads(TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"))["inputText"])
        rogue_action = {
            "kind": "Action", "key": "rogue_action", "label": "rogue", "sourceYamlRefs": ["a"],
            "authorityMarker": "validation_only",
            "eventBinding": {"trigger": "click", "wiringLane": "external_instance_wiring", "targetRef": "instance:db_instance_port:x:y", "authority": "validation_only"},
        }
        tampered_schema["root"]["children"][0]["children"][0]["children"].append(rogue_action)
        tmp40 = write_topology_seed_tmp_fixture(json.dumps(tampered_schema), tmpdir=tmpdir)
        _, doc40 = run_generate_topology_seed(tmp40)
        expect("40. generate-topology-seed re-validates the supplied schema (rogue Action under Section is still rejected)", "ACTION_NOT_OWNED_BY_FORM_OR_WORKFLOW" in rule_ids(doc40))

        # 41. every emitted seed record in the golden run has a non-null, non-empty label
        # (regression pin: Action/Step/etc. used to fall through with label=null and
        # validationErrors=[] since LABEL_REQUIRED_UNITS didn't cover them)
        def find_bad_labels(rec, acc):
            if rec.get("label") in (None, ""):
                acc.append((rec.get("recordType"), rec.get("key")))
            for list_field in ("categories", "sections", "children", "fields", "actions", "steps", "columns"):
                for c in rec.get(list_field) or []:
                    if isinstance(c, dict):
                        find_bad_labels(c, acc)

        bad_labels = []
        if tuc.get("projections"):
            find_bad_labels(tuc["projections"][0], bad_labels)
        expect("41. no emitted seed record (including Action/Step) has a null or empty label", not bad_labels)

        # 42. post-conversion validator catches a null label deep in a supplied schema
        # even when the markup parser never ran (mode=generate_topology_ui_seed skips
        # parse_markup entirely, so this must be a standalone recursive check)
        schema_with_bad_label = json.loads(json.loads(TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"))["inputText"])
        schema_with_bad_label["root"]["children"][2]["children"][0]["children"][0]["children"][1]["label"] = None
        tmp42 = write_topology_seed_tmp_fixture(json.dumps(schema_with_bad_label), tmpdir=tmpdir)
        _, doc42 = run_generate_topology_seed(tmp42)
        expect("42. a supplied schema with a null label deep in the tree becomes blocking SEED_RECORD_MISSING_REQUIRED_FIELD", "SEED_RECORD_MISSING_REQUIRED_FIELD" in rule_ids(doc42))

        # --- round 17: admin_runtime_dispatch_override_wiring lane (6th wiring_lane_contract
        # lane) -- an Action authoring a dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger
        # override must reach tensorAdoptionCandidates via generation, not only hand-authored
        # seed SQL (translator/fixture sync gap flagged by the round 17 review). ---
        admin_runtime_override_schema = {
            "schema": "topolactor.react_schema.v1",
            "presetKey": "admin.enum.management.write.create_group",
            "surface": "admin.enum.management.write.create_group",
            "sourceYamlRefs": ["docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml#admin_runtime_target_ref_override_contract"],
            "root": {
                "kind": "Projection", "key": "p", "label": "p", "sourceYamlRefs": ["a"],
                "children": [{
                    "kind": "Category", "key": "c1", "label": "c1", "sourceYamlRefs": ["a"],
                    "children": [{
                        "kind": "Section", "key": "s1", "label": "s1", "sourceYamlRefs": ["a"], "sectionKind": "readonly_boundary",
                        "children": [{
                            "kind": "Form", "key": "create_group_form", "label": "Create group", "sourceYamlRefs": ["a"],
                            "target": "enum.groups", "mode": "create",
                            "authorityMarker": "draft_apply_not_execution_authority",
                            "fields": ["group_name_field"], "actions": ["confirm_button"],
                            "children": [
                                {
                                    "kind": "Field", "key": "group_name_field", "label": "Group name", "sourceYamlRefs": ["a"],
                                    "control": "form_input/form_field", "required": True,
                                },
                                {
                                    "kind": "Action", "key": "confirm_button", "label": "Confirm & write", "sourceYamlRefs": ["a"],
                                    "authorityMarker": "draft_apply_not_execution_authority",
                                    "actionRef": "ui-local:confirm_button.write",
                                    "eventBinding": {
                                        "trigger": "click",
                                        "wiringLane": "admin_runtime_dispatch_override_wiring",
                                        "targetRef": "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
                                        "authority": "draft_apply_not_execution_authority",
                                        "payloadFrom": {"groupName": "node:group_name_field.value"},
                                    },
                                },
                            ],
                        }],
                    }],
                }],
            },
        }
        tmp_aro = write_topology_seed_tmp_fixture(json.dumps(admin_runtime_override_schema), tmpdir=tmpdir)
        proc_aro, doc_aro = run_generate_topology_seed(tmp_aro)
        expect(
            "42a. admin_runtime_dispatch_override_wiring Action translates with zero validationErrors",
            proc_aro.returncode == 0 and not (doc_aro or {}).get("validationErrors"),
        )
        aro_adoption = (doc_aro or {}).get("adoptionCandidates") or {}
        aro_tensor_nodes = []
        for tensor in aro_adoption.get("tensorAdoptionCandidates") or []:
            aro_tensor_nodes.extend(dig(tensor, "layoutPatchJson", "nodes") or [])
        aro_node = next((n for n in aro_tensor_nodes if n.get("dispatchTargetRefByTrigger")), None)
        expect(
            "42b. the Action's override reaches tensorAdoptionCandidates[].layoutPatchJson.nodes[].dispatchTargetRefByTrigger keyed by trigger",
            aro_node is not None
            and aro_node["dispatchTargetRefByTrigger"].get("click")
            == "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
        )
        expect(
            "42c. the same node's dispatchPayloadFromByTrigger carries the eventBinding's payloadFrom for that trigger",
            aro_node is not None
            and aro_node.get("dispatchPayloadFromByTrigger", {}).get("click") == {"groupName": "node:group_name_field.value"},
        )
        expect(
            "42d. the override is NEVER folded into runtimeInteractions[] (action-authority-vs-effect-data separation, round 6/15/16 design)",
            aro_node is not None and aro_node.get("runtimeInteractions") == [],
        )
        expect(
            "42f. (round 19) the override's tensor node is keyed by the ACTION LEAF's own resolved "
            "key ('confirm_button'), NOT its owning Form's key ('create_group_form') -- "
            "backend/repository/LayoutSchemaTensorComposer.cs's Compose merges "
            "dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger via a plain NodeId match "
            "against a catalog leaf (isCatalogLeaf-gated), never via "
            "BuildInteractionsBySourceActionKey's sourceActionKey scoping that only applies to "
            "runtimeInteractionsJson -- a Form record is structural (StructuralRecordTypes), so "
            "keying this override by the owning form's nodeId silently drops it from every real "
            "merge (caught by a live-DB round trip, Topolactor.Integration.Tests "
            "AdminEnumHubRelationUiProjectionLiveDbTests."
            "DispatchAsync_AdminEnumManagementManifest_CreateGroupFormNode_..., before this fix).",
            aro_node is not None and aro_node.get("nodeId") == "confirm_button",
        )

        # 42g (round 21/22 audit): the Python NODE_VALUE_RE grammar for payloadFrom's
        # node:<nodeId>.value(.<path>)* pattern must accept/reject the IDENTICAL set of raw
        # source strings the frontend's payloadFromResolver.ts NODE_VALUE_RE does. Round 22 fix:
        # both suites now read the SAME shared, machine-readable corpus file --
        # .agent/tests/fixtures/payload-from-node-value-grammar-corpus.json -- rather than each
        # hand-retyping its own copy of the accept/reject lists (round 21's own version had done
        # exactly that duplication, which this round's own audit flagged as an NG-axis violation
        # to leave standing). Editing a case means editing that ONE file; both suites pick it up
        # automatically, and neither can silently drift from the other.
        sys.path.insert(0, str(REPO_ROOT / ".agent" / "scripts"))
        import react_schema_topology_seed_translator as translator_module  # noqa: E402
        node_value_re = translator_module.NODE_VALUE_RE
        grammar_corpus_path = REPO_ROOT / ".agent" / "tests" / "fixtures" / "payload-from-node-value-grammar-corpus.json"
        grammar_corpus = json.loads(grammar_corpus_path.read_text())
        grammar_parity_accept = grammar_corpus["accept"]
        grammar_parity_reject = grammar_corpus["reject"]
        expect(
            "42g. Python NODE_VALUE_RE accepts every string the frontend grammar accepts "
            "(node:<id>.value and node:<id>.value.<path> forms, hyphenated nodeIds included)",
            all(node_value_re.match(s) for s in grammar_parity_accept),
        )
        expect(
            "42h. Python NODE_VALUE_RE rejects every string the frontend grammar rejects "
            "(missing .value, trailing dot with no segment, empty nodeId, near-miss suffix, "
            "a different pattern kind, and literal: prefix precedence)",
            not any(node_value_re.match(s) for s in grammar_parity_reject),
        )

        # Negative: an Action declaring this lane but never reaching a tensor node (simulated by
        # validating adoption candidates directly with an empty tensorAdoptionCandidates bucket)
        # must fail closed, mirroring the pre-existing RUNTIME_INTERACTIONS_NOT_PERSISTED_LAYOUT_PATH
        # completeness check for the other dispatch lanes.
        orphan_admin_runtime_override_action = {
            "record": {
                "recordType": "topology_ui_action",
                "key": "orphan_override_action",
                "sourceReactPath": "$.root.children[0]",
                "eventBinding": {
                    "trigger": "click",
                    "wiringLane": "admin_runtime_dispatch_override_wiring",
                    "targetRef": "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
                    "authority": "draft_apply_not_execution_authority",
                },
            },
        }
        orphan_aro_errors = translator_impl.validate_adoption_candidates(
            {"tensorAdoptionCandidates": []}, [orphan_admin_runtime_override_action],
        )
        orphan_aro_rule_ids = [e.get("ruleId") for e in orphan_aro_errors]
        expect(
            "42e. ADMIN_RUNTIME_DISPATCH_OVERRIDE_NOT_PERSISTED_LAYOUT_PATH fires when an admin_runtime_dispatch_override_wiring Action has no tensorAdoptionCandidates dispatchTargetRefByTrigger entry",
            "ADMIN_RUNTIME_DISPATCH_OVERRIDE_NOT_PERSISTED_LAYOUT_PATH" in orphan_aro_rule_ids,
        )

        # --- physical_search_crud_aggregate.v1 canonical SPA CRUD schema fixture ---
        # Independent, standalone fixture (not just JSON embedded in the topology-seed
        # envelope's inputText) plus a sync check that the envelope's inputText really
        # is that same fixture's content, not a drifted copy.

        crud_schema = json.loads(CRUD_SCHEMA_FIXTURE.read_text(encoding="utf-8"))
        crud_envelope = json.loads(CRUD_TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"))

        expect("43. physical-search-crud-aggregate.react-schema.json is a topolactor.react_schema.v1 candidate for physical_search_crud_aggregate.v1", crud_schema.get("schema") == "topolactor.react_schema.v1" and crud_schema.get("surface") == "physical_search_crud_aggregate.v1")
        expect("44. canonical CRUD schema fixture has a non-empty root.sourceYamlRefs and a Projection root with children", bool(crud_schema.get("sourceYamlRefs")) and dig(crud_schema, "root", "kind") == "Projection" and bool(dig(crud_schema, "root", "children")))

        expect("45. topology-seed envelope for physical_search_crud_aggregate.v1 has mode=generate_topology_ui_seed and matching targetSurface", crud_envelope.get("mode") == "generate_topology_ui_seed" and crud_envelope.get("targetSurface") == "physical_search_crud_aggregate.v1" == crud_schema.get("surface"))

        try:
            embedded_schema = json.loads(crud_envelope.get("inputText", ""))
        except json.JSONDecodeError:
            embedded_schema = None
        expect("46. topology-seed envelope inputText, parsed as JSON, is byte-for-byte identical to the canonical schema fixture (no drifted embedded copy)", embedded_schema == crud_schema)

        crud_root = crud_schema.get("root") or {}
        crud_section_kinds = set()

        def collect_section_kinds(node, acc):
            if node.get("kind") == "Section" and node.get("sectionKind"):
                acc.add(node["sectionKind"])
            for c in node.get("children") or []:
                collect_section_kinds(c, acc)

        collect_section_kinds(crud_root, crud_section_kinds)
        expect("47. canonical CRUD schema expresses search/result/modal-create structure via existing Section.sectionKind values (no new node kind)", {"search_control", "result_projection", "modal_create_control"}.issubset(crud_section_kinds))

        def collect_tables(node, acc):
            if node.get("kind") == "Table":
                acc.add(node.get("display"))
            for c in node.get("children") or []:
                collect_tables(c, acc)

        crud_table_displays = set()
        collect_tables(crud_root, crud_table_displays)
        expect("48. canonical CRUD schema expresses card-list result projection via Table.display (no new node kind)", crud_table_displays == {"card_list"})

        crud_action_keys = set(collect_keys_by_kind(crud_root, "Action"))
        expect("49. canonical CRUD schema contains search/add/submit/cancel actions matching the real preset seed's node keys", crud_action_keys == {"crud_search_button", "crud_add_button", "crud_submit_button", "crud_cancel_button"})

        def find_action_parent_kinds(node, acc, parent_kind=None):
            if node.get("kind") == "Action":
                acc.add(parent_kind)
            for c in node.get("children") or []:
                find_action_parent_kinds(c, acc, parent_kind=node.get("kind"))

        crud_action_parent_kinds = set()
        find_action_parent_kinds(crud_root, crud_action_parent_kinds)
        expect("50. every Action in the canonical CRUD schema is a direct child of a Form (no Action under Section/Table)", crud_action_parent_kinds == {"Form"})

        # generate-topology-seed against the canonical CRUD fixture
        out_path_crud = Path(tmpdir) / "translated-crud-seed.json"
        proc_crud, doc_crud = run_generate_topology_seed(CRUD_TOPOLOGY_SEED_FIXTURE, extra_args=[
            "--output", str(out_path_crud),
            "--scenario-uuid", SCENARIO_UUID,
        ])
        expect("51. generate-topology-seed translates the canonical CRUD fixture with zero validationErrors", out_path_crud.is_file() and doc_crud is not None and not rule_ids(doc_crud))

        crud_tuc = (doc_crud or {}).get("topologyUiSeedCandidate") or {}
        expect("52. CRUD topologyUiSeedCandidate.schema/role match the contract", crud_tuc.get("schema") == "topolactor.topology_ui_seed.v1" and crud_tuc.get("role") == "draft_intake_artifact_not_active_topology")
        expect("53. CRUD topologyUiSeedCandidate.projections is populated", bool(crud_tuc.get("projections")))
        expect("54. CRUD exchangeReport.outputSeedSchemaId == topolactor.topology_ui_seed.v1", dig(doc_crud or {}, "exchangeReport", "outputSeedSchemaId") == "topolactor.topology_ui_seed.v1")
        expect("55. CRUD run has no unexplained lossEntries (loss only where a knownGapRef backs it)", all(e.get("knownGapRef") for e in dig(doc_crud or {}, "exchangeReport", "lossEntries") or []))
        crud_expected_gap_refs = {
            "enum_status_select_options_from_content_bundle_list_states",
            "ssot_ambiguity_gap:crud_add_button_wiring_candidate_not_present_in_seed_compile_snapshot",
            "runtime_dispatch_or_projection_gap:table_item_click_wiring_not_yet_expressible_in_react_schema_contract",
            "form_field_values_to_create_entity_draft_payload",
        }
        expect("56. envelope-level knownGapRefs (all four gaps surfaced by connecting to the real preset seed) propagate into unresolvedGaps", crud_expected_gap_refs.issubset(set((doc_crud or {}).get("unresolvedGaps") or [])))

        crud_node_gap_map = {
            "crud_status_filter": "enum_status_select_options_from_content_bundle_list_states",
            "crud_add_button": "ssot_ambiguity_gap:crud_add_button_wiring_candidate_not_present_in_seed_compile_snapshot",
            "crud_result_list": "runtime_dispatch_or_projection_gap:table_item_click_wiring_not_yet_expressible_in_react_schema_contract",
            "crud_submit_button": "form_field_values_to_create_entity_draft_payload",
        }

        def find_node_gap_refs(node, key, acc):
            if node.get("key") == key:
                acc.extend(node.get("knownGapRefs") or [])
            for c in node.get("children") or []:
                find_node_gap_refs(c, key, acc)

        crud_node_gaps_match = True
        for node_key, expected_gap in crud_node_gap_map.items():
            found = []
            find_node_gap_refs(crud_root, node_key, found)
            if expected_gap not in found:
                crud_node_gaps_match = False
        expect("57. each node-level knownGapRef sits on the exact node it was discovered on while connecting to the real preset seed (crud_status_filter/crud_add_button/crud_result_list/crud_submit_button)", crud_node_gaps_match)
        expect("58. CRUD run carries no active-topology/execution-authority claim", "activeTopology" not in json.dumps(crud_tuc) and "runtimeExecute" not in json.dumps(crud_tuc))

        crud_record_types_seen = set()

        def collect_record_types(rec, acc):
            acc.add(rec.get("recordType"))
            for list_field in ("categories", "sections", "children", "fields", "actions", "steps", "columns"):
                for c in rec.get(list_field) or []:
                    if isinstance(c, dict):
                        collect_record_types(c, acc)

        if crud_tuc.get("projections"):
            collect_record_types(crud_tuc["projections"][0], crud_record_types_seen)
        expect("59. CRUD seed emits topology_ui_form, topology_ui_table, and topology_ui_action records (search toolbar/result list/create modal all round-trip)", {"topology_ui_form", "topology_ui_table", "topology_ui_action"}.issubset(crud_record_types_seen))

        crud_bad_labels = []
        if crud_tuc.get("projections"):
            find_bad_labels(crud_tuc["projections"][0], crud_bad_labels)
        expect("60. no emitted CRUD seed record has a null or empty label", not crud_bad_labels)

        # existing credential-management-0092 fixtures must still be unaffected
        _, doc_regression = run_generate(FIXTURE)
        expect("61. credential-management-0092 generate-react-schema fixture still passes with zero validationErrors (no regression)", doc_regression is not None and not rule_ids(doc_regression))
        _, doc_regression_seed = run_generate_topology_seed(TOPOLOGY_SEED_FIXTURE)
        expect("62. credential-management-0092 generate-topology-seed fixture still passes with zero validationErrors (no regression)", doc_regression_seed is not None and not rule_ids(doc_regression_seed))

        # ── Seed-first connection: the fixture must trace to the real, already-registered
        # db/physical_search_crud_aggregate_preset_seed.sql compile snapshot, not just
        # invent thematically similar content. Test/proof-layer-only db/*.sql reading,
        # mirroring frontend/tests/presetSeedLineContract.test.ts's extractCompileSnapshot()
        # contracts -- the translator body never reads db/*.sql.

        crud_seed_sql = CRUD_PRESET_SEED_SQL_PATH.read_text(encoding="utf-8")
        snapshot = extract_compile_snapshot(crud_seed_sql, "db/physical_search_crud_aggregate_preset_seed.sql")
        expect("63. db/physical_search_crud_aggregate_preset_seed.sql's compile snapshot INSERT resolves 5 ordered jsonb blocks", snapshot is not None)

        layout_nodes = dig(snapshot, "layoutPatchJson", "nodes") or []
        layout_nodes_valid = bool(layout_nodes) and all(all(f in n for f in REQUIRED_NODE_FIELDS) for n in layout_nodes)
        expect("64. real seed layout_patch_json.nodes is non-empty and every node carries REQUIRED_NODE_FIELDS", layout_nodes_valid)

        pkg = snapshot.get("packageMembershipCandidateJson") or {}
        expect("65. real seed package_membership_candidate_json.activeTopologyWrite is false", pkg.get("activeTopologyWrite") is False)

        wiring_candidates = snapshot.get("wiringCandidateJson") or []
        valid_statuses = {"pending", "confirmed", "rejected"}
        wiring_valid = bool(wiring_candidates) and all(
            wc.get("status") in valid_statuses and str(wc.get("targetRef", "")).startswith("content_bundle:")
            for wc in wiring_candidates
        )
        expect("66. real seed wiring_candidate_json is non-empty with valid statuses and content_bundle: target refs", wiring_valid)

        unresolved_entries = snapshot.get("unresolvedJson") or []
        unresolved_valid = bool(unresolved_entries) and all(
            e.get("nodeId") and e.get("reason") and e.get("knownGapRef") for e in unresolved_entries
        )
        expect("67. real seed unresolved_json entries each carry nodeId/reason/knownGapRef (gaps stay explicit, never silently dropped)", unresolved_valid)

        seed_wiring_by_node = {wc.get("nodeId"): wc.get("targetRef") for wc in wiring_candidates}
        expect(
            "68. fixture's crud_search_button/crud_submit_button actionRef matches the real seed's wiring_candidate_json targetRef for those same nodeIds",
            seed_wiring_by_node.get("crud_search_button") == "content_bundle:search"
            and seed_wiring_by_node.get("crud_submit_button") == "content_bundle:create_entity_draft",
        )
        expect(
            "69. crud_add_button genuinely has no wiring_candidate_json entry in the real seed (the fixture's knownGapRef on it is honestly discovered, not invented)",
            "crud_add_button" not in seed_wiring_by_node,
        )

        seed_unresolved_gap_by_node = {e.get("nodeId"): e.get("knownGapRef") for e in unresolved_entries}
        expect(
            "70. real seed unresolved_json knownGapRefs for crud_status_filter/crud_submit_button match the fixture's node-level knownGapRefs exactly",
            seed_unresolved_gap_by_node.get("crud_status_filter") == "enum_status_select_options_from_content_bundle_list_states"
            and seed_unresolved_gap_by_node.get("crud_submit_button") == "form_field_values_to_create_entity_draft_payload",
        )

        expect(
            "71. real seed's crud_result_list item.click wiring (content_bundle:get_entity) is exactly why the fixture's Table node carries the table-item-click knownGapRef (Table has no eventBinding field yet)",
            seed_wiring_by_node.get("crud_result_list") == "content_bundle:get_entity",
        )

        # --- schema<->seed translator entry gate: verify the shared gate core
        # (.agent/scripts/agent_tools/schema_seed_translator_entry_gate.py) is
        # actually wired into this translator's entry, not a disconnected
        # parallel preflight. Full shape-detection/fail-close-condition
        # coverage lives in the dedicated
        # check_schema_seed_translator_entry_gate.py proof; these checks only
        # confirm the connection is real from this translator proof's own
        # golden/negative fixtures.

        gate_dir = REPO_ROOT / ".agent" / "scripts" / "agent_tools"
        sys.path.insert(0, str(gate_dir))
        import schema_seed_translator_entry_gate as gate

        expect(
            "72. schema_seed_translator_entry_gate core never reads db/*.sql and declares itself read-only/no-db/no-api/no-write",
            gate.GATE_BOUNDARY.get("read_only") is True
            and gate.GATE_BOUNDARY.get("db_connection") is False
            and gate.GATE_BOUNDARY.get("external_api_connection") is False
            and gate.GATE_BOUNDARY.get("writes_repo_files") is False
            and gate.GATE_BOUNDARY.get("runs_translator_conversion") is False,
        )

        expect("73. golden credential-management-0092 generate-react-schema run reports gateStatus == pass (translator entry actually called the gate core)", doc.get("gateStatus") == gate.GATE_STATUS_PASS)
        expect("74. golden credential-management-0092 generate-topology-seed run reports gateStatus == pass", doc_ts.get("gateStatus") == gate.GATE_STATUS_PASS)
        expect("75. golden physical_search_crud_aggregate.v1 generate-topology-seed run reports gateStatus == pass", doc_crud.get("gateStatus") == gate.GATE_STATUS_PASS)

        expect(
            "76. a deliberately-blocking generate-react-schema entry (missing wiringLane) reports gateStatus == blocking (gate connection is real, not translator-only self-reporting)",
            doc16 is not None and doc16.get("gateStatus") == gate.GATE_STATUS_BLOCKING,
        )
        expect(
            "77. a deliberately-blocking generate-topology-seed entry (non-JSON inputText) reports gateStatus == blocking",
            doc38 is not None and doc38.get("gateStatus") == gate.GATE_STATUS_BLOCKING,
        )

        # gate core called directly (bypassing the translator CLI entirely)
        # must agree with the translator-entry-observed gateStatus above --
        # same core, same verdict, from either caller.
        direct_pass = gate.validate_translator_entry(fixture_envelope, expected_mode="generate_react_schema")
        expect("78. gate core invoked directly on the same golden fixture independently reports gateStatus == pass", direct_pass["gateStatus"] == gate.GATE_STATUS_PASS)

        direct_blocking = gate.validate_translator_entry("not valid json", expected_mode="generate_react_schema")
        expect("79. gate core invoked directly on invalid JSON reports gateStatus == unsupported_input_shape (fail-closed, not silently accepted)", direct_blocking["gateStatus"] == gate.GATE_STATUS_UNSUPPORTED)

        # --- generate.log regeneration-trace evidence -------------------------
        #
        # *seed.sql / SSOT docs remain the production storage authority;
        # generated JSON is a local/tmp projection under .agent/tools/generated/
        # (gitignored, never tracked evidence). .agent/tools/logs/generate.log
        # is the tracked JSON Lines regeneration index instead. This is trace
        # evidence only -- never seed adoption authority, never proof completion
        # by itself -- so these checks verify shape/regeneration-hash-consistency,
        # not semantic correctness of any one record's content.

        generate_log_path = REPO_ROOT / ".agent" / "tools" / "logs" / "generate.log"
        expect("80. .agent/tools/logs/generate.log exists as tracked trace evidence", generate_log_path.is_file())

        log_lines = [ln for ln in generate_log_path.read_text(encoding="utf-8").splitlines() if ln.strip()]
        expect("81. generate.log is non-empty", bool(log_lines))

        log_records = []
        for ln in log_lines:
            try:
                log_records.append(json.loads(ln))
            except json.JSONDecodeError:
                log_records.append(None)
        expect("82. every generate.log line parses as valid JSON (JSON Lines, one record per line)", log_records and all(r is not None for r in log_records))

        required_record_fields = [
            "datetime", "nametag", "mode", "source", "sourceSeedSql", "seedKey", "manifestId",
            "command", "outputKind", "outputSchemaId", "embeddedCandidateKind", "outputPath",
            "sha256", "gateStatus", "validationErrorCount", "unresolvedGapCount", "taskRef", "prRef",
        ]
        expect(
            "83. every generate.log record carries all required fields (nullable where declared, never absent)",
            all(r is not None and all(f in r for f in required_record_fields) for r in log_records),
        )
        expect(
            "84. every generate.log record's gateStatus/mode/embeddedCandidateKind reflect an actual gate-connected translator run",
            all(r.get("gateStatus") == "pass" and r.get("mode") in ("generate_react_schema", "generate_topology_ui_seed") and r.get("embeddedCandidateKind") for r in log_records),
        )
        expect(
            "84a. generate.log's outputKind/outputSchemaId describe the actual hashed artifact (the full topolactor.translator_output.v1 document --output writes), not just the candidate embedded inside it",
            all(
                r.get("outputKind") == "translator_output_document" and r.get("outputSchemaId") == "topolactor.translator_output.v1"
                for r in log_records
            ),
        )

        # 85. .agent/tools/generated/* is regeneration-only local output, never
        # a tracked-required path: none of the log's outputPath values are
        # tracked in git (a clean clone must not assume they already exist).
        tracked_files = set(
            subprocess.run(["git", "-C", str(REPO_ROOT), "ls-files"], capture_output=True, text=True, timeout=30).stdout.splitlines()
        )
        expect(
            "85. generate.log outputPath values are not assumed to exist in a clean clone (regenerate-on-demand, not tracked)",
            all(r.get("outputPath") not in tracked_files for r in log_records if r.get("outputPath")),
        )

        # 86. regeneration index actually regenerates: re-running the first
        # record's source/mode with a fresh --output reproduces the same
        # sha256 (the translator output document carries no timestamps of its
        # own -- only the generate.log record does -- so re-running the same
        # --input deterministically reproduces byte-identical output).
        first_record = log_records[0] if log_records else None
        if first_record is not None:
            subcommand = "generate-react-schema" if first_record.get("mode") == "generate_react_schema" else "generate-topology-seed"
            regen_out = Path(tmpdir) / "regenerated-from-generate-log.json"
            proc_regen = run_tool([subcommand, "--input", first_record["source"], "--output", str(regen_out)])
            regen_sha256 = None
            regen_doc = None
            if regen_out.is_file():
                regen_bytes = regen_out.read_bytes()
                h = hashlib.sha256()
                h.update(regen_bytes)
                regen_sha256 = h.hexdigest()
                try:
                    regen_doc = json.loads(regen_bytes.decode("utf-8"))
                except json.JSONDecodeError:
                    regen_doc = None
            expect(
                "86. re-running generate.log's first record (same source/mode) reproduces the exact recorded sha256 (regeneration index actually regenerates)",
                regen_sha256 is not None and regen_sha256 == first_record.get("sha256"),
            )
            expect(
                "86a. the regenerated (hashed) artifact really is a topolactor.translator_output.v1 document embedding the record's claimed candidate kind (outputKind/embeddedCandidateKind are not mislabeled)",
                regen_doc is not None
                and regen_doc.get("schemaId") == first_record.get("outputSchemaId") == "topolactor.translator_output.v1"
                and regen_doc.get(
                    "topologyUiSeedCandidate" if first_record.get("embeddedCandidateKind") == "topology_ui_seed_candidate" else "reactSchemaCandidate"
                ) is not None,
            )
        else:
            fail("86. no generate.log record available to regenerate")
            fail("86a. no generate.log record available to regenerate")


        runtime_id_pattern = re.compile(r'"runtimeInteractionId"\s*:')
        seed_runtime_id_surfaces = {
            "credential-management input fixture": FIXTURE.read_text(encoding="utf-8"),
            "credential-management topology-seed fixture": TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"),
            "db/seed_empty.sql": SEED_EMPTY_PATH.read_text(encoding="utf-8"),
        }
        expect(
            "87. credential-management fixtures and db/seed_empty.sql do not hardcode runtimeInteractionId (backend persistence boundary owns final assignment)",
            all(runtime_id_pattern.search(text) is None for text in seed_runtime_id_surfaces.values()),
        )

        generated_seed_docs = [doc_ts, doc_crud]
        expect(
            "88. generate-topology-seed outputs remain draft/intake candidates and do not mint runtimeInteractionId authority",
            all(
                d is not None
                and runtime_id_pattern.search(json.dumps(d, separators=(",", ":"), ensure_ascii=False)) is None
                and (dig(d, "topologyUiSeedCandidate", "role") == "draft_intake_artifact_not_active_topology"
                     or dig(d, "topologyUiSeedCandidate", "schema") == "topolactor.topology_ui_seed.v1")
                for d in generated_seed_docs
            ),
        )

        repo_text_targets = [
            REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "agent_ui_initial_contract.py",
            REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "agent_ui_local_test.py",
            REPO_ROOT / ".agent" / "tools" / "README.md",
            REPO_ROOT / "docs" / "governance" / "reference" / "agent-ui-tool-output-reference.yaml",
            REPO_ROOT / ".agent" / "prompt" / "implementation-change.md",
        ]
        repo_text = "\n".join(path.read_text(encoding="utf-8") for path in repo_text_targets)
        expect(
            "89. Agent UI route wording exposes prompt_content full text plus normalized protocol_obligations[] and does not retain protocol-excerpts/manual-protocol/retired protocol_trigger_hints as the tool-first route",
            "protocol excerpts" not in repo_text.lower()
            and "triggered protocol excerpts" not in repo_text.lower()
            and "protocol_trigger_hints" not in repo_text
            and "protocol_obligations" in repo_text
            and "fallback_protocol_ref" in repo_text
            and "full text" in repo_text.lower(),
        )


        # PR578 scope-authority correction: the todo Bundle body (not the
        # shortened initial prompt) requires evidence across the listed backend,
        # frontend forwarding, translator, entry-gate, fixture, seed, and Agent
        # UI governance surfaces. These static checks deliberately read those
        # target surfaces so the proof cannot shrink to only the files touched
        # by the previous PR diff.
        backend_repo_src = (REPO_ROOT / "backend" / "repository" / "NpgsqlUiTopologyRepository.cs").read_text(encoding="utf-8")
        apply_idx = backend_repo_src.find("public override async Task<LayoutPatchResult> ApplyConfirmedLayoutPatchAsync")
        assign_call_idx = backend_repo_src.find("AssignRuntimeInteractionIds(valid.TensorPatchJson)", apply_idx)
        persist_idx = backend_repo_src.find("UPDATE topology.ui_topology_tensor SET layout_patch_json=@patch::jsonb", apply_idx)
        expect(
            "90. backend persistence wiring calls AssignRuntimeInteractionIds inside ApplyConfirmedLayoutPatchAsync before layout_patch_json persistence",
            apply_idx >= 0 and assign_call_idx > apply_idx and persist_idx > assign_call_idx,
        )
        expect(
            "91. backend assignment boundary includes HasValidRuntimeInteractionId and invalid-id replacement before persisted layout_patch_json",
            "private static bool HasValidRuntimeInteractionId" in backend_repo_src
            and "Guid.TryParse" in backend_repo_src
            and 'writer.WriteString("runtimeInteractionId", Guid.NewGuid().ToString())' in backend_repo_src,
        )

        seed_template_persist_bypass = re.search(
            r"INSERT\s+INTO\s+topology\.ui_topology_tensor\s*\([^)]*layout_patch_json|UPDATE\s+topology\.ui_topology_tensor\s+SET\s+layout_patch_json",
            backend_repo_src,
            re.IGNORECASE | re.DOTALL,
        )
        expect(
            "92. NpgsqlUiTopologyRepository has no alternate active layout_patch_json persist path before the assignment boundary check (bypass would be caught here)",
            seed_template_persist_bypass is not None and seed_template_persist_bypass.start() == persist_idx,
        )

        render_src = (REPO_ROOT / "frontend" / "runtime" / "renderEmission.ts").read_text(encoding="utf-8")
        event_builder_idx = render_src.find("function buildExternalPortEventBinding")
        event_runtime_id_idx = render_src.find("runtimeInteractionId", event_builder_idx)
        event_forward_idx = render_src.find("runtimeInteractionId,", event_runtime_id_idx)
        effect_src = (REPO_ROOT / "frontend" / "runtime" / "uiEventEffectRunner.ts").read_text(encoding="utf-8")
        lifecycle_idx = effect_src.find("const emitLifecycle")
        lifecycle_forward_idx = effect_src.find("runtimeInteractionId: w.runtimeInteractionId", lifecycle_idx)
        visual_src = (REPO_ROOT / "frontend" / "runtime" / "visualLayoutUtils.ts").read_text(encoding="utf-8")
        clone_idx = visual_src.find("export function cloneVisualNode")
        strip_idx = visual_src.find("runtimeInteractionId: _runtimeInteractionId", clone_idx)
        shell_src = (REPO_ROOT / "frontend" / "islands" / "ProjectionShell.tsx").read_text(encoding="utf-8")
        expect(
            "93. ProjectionShell/renderEmission/uiEventEffectRunner forward persisted runtimeInteractionId through event and lifecycle dispatch lanes",
            "renderEmission(" in shell_src
            and "emitLifecycle" in shell_src
            and event_builder_idx >= 0
            and event_runtime_id_idx > event_builder_idx
            and event_forward_idx > event_runtime_id_idx
            and lifecycle_idx >= 0
            and lifecycle_forward_idx > lifecycle_idx,
        )
        expect(
            "94. visualLayoutUtils cloneVisualNode strips runtimeInteractionId so duplicated authored interactions receive fresh backend ids",
            clone_idx >= 0 and strip_idx > clone_idx,
        )

        translator_src = (REPO_ROOT / ".agent" / "scripts" / "react_schema_topology_seed_translator.py").read_text(encoding="utf-8")
        entry_gate_src = (REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "schema_seed_translator_entry_gate.py").read_text(encoding="utf-8")
        expect(
            "95. translator/entry-gate target functions exist and remain disconnected from db/*.sql/persisted runtimeInteractionId authority",
            all(name in translator_src for name in (
                "def convert_node_to_seed_record",
                "def build_topology_ui_seed_candidate",
                "def flatten_topology_ui_seed_tree",
                "def validate_flat_seed_records",
            ))
            and "def validate_translator_entry" in entry_gate_src
            and "Guid.NewGuid" not in translator_src
            and "uuid4" not in translator_src
            and "Guid.NewGuid" not in entry_gate_src
            and "uuid4" not in entry_gate_src,
        )

        agent_ui_targets = {
            "initial": REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "agent_ui_initial_contract.py",
            "local": REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "agent_ui_local_test.py",
            "common": REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "agent_ui_common.py",
        }
        agent_ui_src = {k: v.read_text(encoding="utf-8") for k, v in agent_ui_targets.items()}
        expect(
            "96. Agent UI target functions/common helpers preserve tool-first route and authority boundary wording",
            all(name in agent_ui_src["initial"] for name in ("def _cmd_start", "def _read_full", "def _cmd_resolve_ssot", "def _cmd_sections", "def _cmd_end", "def build_parser"))
            and all(name in agent_ui_src["local"] for name in ("def _cmd_run_worktype_tests", "def _cmd_read_senario_tmp", "def _cmd_checklist", "def _cmd_checks", "def _cmd_summary", "def _run_check", "def _checklist_items"))
            and all(name in agent_ui_src["common"] for name in ("def worktypes", "def reject_output_flag", "def parse_senario_tmp"))
            and "protocol_obligations_note" in agent_ui_src["initial"] and "PROTOCOL_OBLIGATION_NOTE" in agent_ui_src["initial"]
            and "protocol_trigger_hints" not in agent_ui_src["initial"]
            and "def _build_protocol_obligation" in agent_ui_src["initial"]
            and "not SSOT authority" in agent_ui_src["local"]
            and "proof\ncompletion" in agent_ui_src["local"],
        )


        idempotency_schema = {
            "schema": "topolactor.react_schema.v1",
            "presetKey": "auth.external.credential_management.projection",
            "surface": "auth.external.credential_management.projection",
            "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"],
            "root": {
                "kind": "Projection",
                "key": "idempotency_route_projection",
                "label": "Idempotency route projection",
                "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"],
                "children": [{
                    "kind": "Category",
                    "key": "idempotency_category",
                    "label": "Idempotency category",
                    "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"],
                    "children": [{
                        "kind": "Section",
                        "key": "idempotency_section",
                        "label": "Idempotency section",
                        "sectionKind": "fixed_form_projection",
                        "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"],
                        "children": [{
                            "kind": "Form",
                            "key": "idempotency_form",
                            "label": "Idempotency form",
                            "target": "db_instance_port",
                            "mode": "edit",
                            "fields": ["instance_authority_key"],
                            "authorityMarker": "validation_only",
                            "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"],
                            "children": [{
                                "kind": "Field",
                                "key": "instance_authority_key",
                                "label": "Instance authority key",
                                "control": "form_input/form_field",
                                "required": True,
                                "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"]
                            }, {
                                "kind": "Action",
                                "key": "validate_with_idempotency",
                                "label": "Validate with idempotency",
                                "authorityMarker": "validation_only",
                                "actionRef": "instance:db_instance_port:instance_authority_key:operation_binding_key",
                                "eventBinding": {
                                    "trigger": "initial_mount",
                                    "wiringLane": "external_instance_wiring",
                                    "targetRef": "instance:db_instance_port:instance_authority_key:operation_binding_key",
                                    "authority": "validation_only",
                                    "payloadFrom": {"instance_authority_key": "node:instance_authority_key.value"}
                                },
                                "idempotencyPolicy": "once_per_mount",
                                "lifecycleDispatchConfirmed": True,
                                "debounceMs": 250,
                                "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#runtime_interactions_candidate_contract"]
                            }]
                        }]
                    }]
                }]
            }
        }
        idempotency_fixture = write_topology_seed_tmp_fixture(json.dumps(idempotency_schema, separators=(",", ":")), tmpdir=tmpdir)
        proc_idem, doc_idem = run_generate_topology_seed(idempotency_fixture)
        idem_records = (doc_idem or {}).get("topologyUiSeedFlatRecords") or []
        idem_action_record = None
        for wrapper in idem_records:
            record = wrapper.get("record") if isinstance(wrapper, dict) else None
            if isinstance(record, dict) and record.get("key") == "validate_with_idempotency":
                idem_action_record = record
                break
        idem_runtime = (idem_action_record or {}).get("runtimeInteractions") or []
        idem_first = idem_runtime[0] if idem_runtime else {}
        expect(
            "97. generate-topology-seed preserves eventBinding-derived runtimeInteractions[] candidate with idempotency route fields",
            proc_idem.returncode == 0
            and doc_idem is not None
            and not rule_ids(doc_idem)
            and idem_action_record is not None
            and idem_action_record.get("eventBinding", {}).get("targetRef") == "instance:db_instance_port:instance_authority_key:operation_binding_key"
            and idem_first.get("actionType") == "dispatchInstanceOperation"
            and idem_first.get("instanceTargetRef") == "instance-port:db_instance_port:instance_authority_key:operation_binding_key"
            and idem_first.get("idempotencyPolicy") == "once_per_mount"
            and idem_first.get("lifecycleDispatchConfirmed") is True
            and idem_first.get("debounceMs") == 250,
        )
        expect(
            "98. runtimeInteractions[] candidate remains draft/template material and never carries runtimeInteractionId before backend assignment",
            idem_first
            and "runtimeInteractionId" not in idem_first
            and "runtimeInteractionId" not in json.dumps(doc_idem.get("topologyUiSeedCandidate"), separators=(",", ":"), ensure_ascii=False),
        )
        layout_patch_from_candidate = layout_patch_from_seed_runtime_interaction(idem_action_record or {})
        approved_instance_refs = {"instance-port:db_instance_port:instance_authority_key:operation_binding_key"}
        boundary_error = validate_runtime_interactions_boundary_equivalent(layout_patch_from_candidate, approved_instance_refs)
        expect(
            "99. seed/template runtimeInteractions[] candidate reaches backend ValidateRuntimeInteractions-equivalent targetRef vocabulary before assignment",
            boundary_error is None
            and idem_first.get("instanceTargetRef", "").startswith("instance-port:"),
        )
        invalid_layout_patch = json.loads(json.dumps(layout_patch_from_candidate))
        invalid_layout_patch["nodes"][0]["runtimeInteractions"][0]["instanceTargetRef"] = "instance:db_instance_port:instance_authority_key:operation_binding_key"
        invalid_boundary_error = validate_runtime_interactions_boundary_equivalent(invalid_layout_patch, approved_instance_refs)
        expect(
            "100. cross-boundary proof fails closed on eventBinding instance: vocabulary when used as runtimeInteractions[].instanceTargetRef",
            invalid_boundary_error == "RUNTIME_INTERACTION_INSTANCE_TARGET_REF_INVALID:instance:db_instance_port:instance_authority_key:operation_binding_key",
        )

        agent_tools_dir = REPO_ROOT / ".agent" / "scripts" / "agent_tools"
        if str(agent_tools_dir) not in sys.path:
            sys.path.insert(0, str(agent_tools_dir))
        import agent_ui_initial_contract as initial_contract_impl

        impl_change_obligation, impl_change_truncated = initial_contract_impl._build_protocol_obligation(
            ".agent/protocols/implementation-change.md", "required", "always",
        )
        expect(
            "101. protocol_obligations[] entry for a required protocol is a normalized extraction (not full text), routes/applies are set from the caller, and unmapped fields are honestly null rather than fabricated",
            impl_change_obligation is not None
            and impl_change_truncated is False
            and impl_change_obligation["protocol_path"] == ".agent/protocols/implementation-change.md"
            and impl_change_obligation["route_mode"] == "required"
            and impl_change_obligation["applies"] == "always"
            and impl_change_obligation["fallback_protocol_ref"] == ".agent/protocols/implementation-change.md"
            and impl_change_obligation["trigger_condition"] is not None
            and "Runtime/code changes under existing SSOT." in "\n".join(impl_change_obligation["trigger_condition"])
            and impl_change_obligation["blocking_conditions"] is not None
            and any("db-schema.yaml" in line for line in impl_change_obligation["blocking_conditions"])
            and impl_change_obligation["classification_vocab"] is None,
        )

        ssot_impact_obligation, _ = initial_contract_impl._build_protocol_obligation(
            ".agent/protocols/ssot-change-impact.md", "triggered:ssot_change", "agent_judgment_required",
        )
        expect(
            "102. protocol_obligations[] heading-alias matching normalizes heterogeneous real heading spellings (## Trigger / ## Required / ## Prohibited / ## Output Expectation) into the same canonical fields other protocols reach via ## trigger_condition / ## blocking_conditions / ## pass_conditions",
            ssot_impact_obligation is not None
            and ssot_impact_obligation["route_mode"] == "triggered:ssot_change"
            and ssot_impact_obligation["applies"] == "agent_judgment_required"
            and ssot_impact_obligation["trigger_condition"] is not None
            and any("SSOT files" in line for line in ssot_impact_obligation["trigger_condition"])
            and ssot_impact_obligation["required_fields"] is not None
            and any("impact checks" in line for line in ssot_impact_obligation["required_fields"])
            and ssot_impact_obligation["blocking_conditions"] is not None
            and any("stale expectations" in line for line in ssot_impact_obligation["blocking_conditions"])
            and ssot_impact_obligation["output_boundary"] is not None
            and any("lightweight" in line for line in ssot_impact_obligation["output_boundary"]),
        )

        missing_obligation, _ = initial_contract_impl._build_protocol_obligation(
            ".agent/protocols/__does_not_exist__.md", "required", "always",
        )
        expect(
            "103. protocol_obligations[] extraction fails closed (returns None, not a fabricated empty entry) for a routed path that does not exist on disk, matching _read_full's missing-file contract",
            missing_obligation is None,
        )

        # 104-107: validate_disclosure_targets negative-boundary proof (round 25). These
        # exercise the cross-tree disclosure-family validator directly with hand-built
        # nodes -- a well-formed CLI input from a real screen cannot organically produce
        # an unsupported actionType or a dangling targetNodeId, so this is the only place
        # that proves the restricted DISCLOSURE_ACTION_TYPES set (openModal/closeModal/
        # toggleModal only, since round 25 removed the unvalidated Drawer/Dialog/setState
        # entries) and the targetNodeId/target-kind checks actually fail closed rather than
        # silently passing through.
        unsupported_action_node = {
            "_path": "$.test.open_drawer_action",
            "key": "open_drawer_action",
            "eventBinding": {
                "wiringLane": "disclosure_state_wiring",
                "disclosureActionType": "openDrawer",
                "disclosureTargetNodeId": "some_drawer",
            },
        }
        unsupported_action_errors = []
        translator_impl.validate_disclosure_targets(unsupported_action_node, {}, unsupported_action_errors)
        expect(
            "104. validate_disclosure_targets rejects an unsupported disclosure-family actionType (openDrawer) with DISCLOSURE_ACTION_TYPE_UNSUPPORTED rather than treating it as recognized vocabulary",
            "DISCLOSURE_ACTION_TYPE_UNSUPPORTED" in [e["ruleId"] for e in unsupported_action_errors],
        )

        missing_target_node = {
            "_path": "$.test.open_modal_no_target",
            "key": "open_modal_no_target",
            "eventBinding": {
                "wiringLane": "disclosure_state_wiring",
                "disclosureActionType": "openModal",
                "disclosureTargetNodeId": None,
            },
        }
        missing_target_errors = []
        translator_impl.validate_disclosure_targets(missing_target_node, {}, missing_target_errors)
        expect(
            "105. validate_disclosure_targets rejects a supported disclosureActionType (openModal) with a missing/empty targetNodeId via DISCLOSURE_TARGET_NODE_REQUIRED",
            "DISCLOSURE_TARGET_NODE_REQUIRED" in [e["ruleId"] for e in missing_target_errors],
        )

        wrong_kind_target_node = {
            "_path": "$.test.open_modal_wrong_target",
            "key": "open_modal_wrong_target",
            "eventBinding": {
                "wiringLane": "disclosure_state_wiring",
                "disclosureActionType": "openModal",
                "disclosureTargetNodeId": "not_a_modal",
            },
        }
        wrong_kind_target_errors = []
        translator_impl.validate_disclosure_targets(
            wrong_kind_target_node, {"not_a_modal": "Section"}, wrong_kind_target_errors,
        )
        expect(
            "106. validate_disclosure_targets rejects a disclosureTargetNodeId that resolves to a real node of the wrong kind (Section, not Modal) via DISCLOSURE_TARGET_KIND_MISMATCH",
            "DISCLOSURE_TARGET_KIND_MISMATCH" in [e["ruleId"] for e in wrong_kind_target_errors],
        )

        valid_target_node = {
            "_path": "$.test.open_modal_valid",
            "key": "open_modal_valid",
            "eventBinding": {
                "wiringLane": "disclosure_state_wiring",
                "disclosureActionType": "openModal",
                "disclosureTargetNodeId": "real_modal",
            },
        }
        valid_target_errors = []
        translator_impl.validate_disclosure_targets(
            valid_target_node, {"real_modal": "Modal"}, valid_target_errors,
        )
        expect(
            "107. validate_disclosure_targets positive control: a supported actionType (openModal) with a targetNodeId resolving to an actual Modal node produces zero errors, proving 104-106 fail for the right reason and not because every input fails",
            valid_target_errors == [],
        )

        # 108-116: validate_admin_runtime_preview_action_pairing negative-boundary proof
        # (preview-gap round). This proves the FULL safety shape of a Section-owned
        # admin_runtime_dispatch_override_wiring Action is checked, not merely that the lane
        # name is a member of SECTION_OWNABLE_ACTION_LANES -- each case below isolates exactly
        # one violated invariant against an otherwise-valid preview/confirm pairing built from
        # hand-crafted nodes (a well-formed CLI input cannot organically produce most of these
        # defects), so a passing test proves the check actually distinguishes the specific
        # violation, not that every input is rejected.
        def build_preview_confirm_pair(
            preview_payload_from=None,
            confirm_payload_from=None,
            secondary=("openModal", "confirm_modal"),
            confirm_target_ref="manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
            confirm_count=1,
        ):
            preview_target_ref = "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group"
            if preview_payload_from is None:
                preview_payload_from = {"groupName": "node:name_input.value", "dryRun": "literal:true"}
            if confirm_payload_from is None:
                confirm_payload_from = {"groupName": "node:name_input.value", "confirmed": "literal:true"}
            confirm_actions = [
                {
                    "kind": "Action",
                    "key": f"confirm_btn_{i}",
                    "eventBinding": {
                        "wiringLane": "admin_runtime_dispatch_override_wiring",
                        "targetRef": confirm_target_ref,
                        "payloadFrom": confirm_payload_from,
                    },
                }
                for i in range(confirm_count)
            ]
            modal_node = {"kind": "Modal", "key": "confirm_modal", "children": confirm_actions}
            preview_action = {
                "kind": "Action",
                "key": "open_btn",
                "_path": "$.test.open_btn",
                "eventBinding": {
                    "wiringLane": "admin_runtime_dispatch_override_wiring",
                    "targetRef": preview_target_ref,
                    "payloadFrom": preview_payload_from,
                },
            }
            if secondary is not None:
                action_type, target_node_id = secondary
                preview_action["secondaryDisclosureAction"] = {
                    "actionType": action_type, "targetNodeId": target_node_id,
                }
            nodes_by_key = {"confirm_modal": modal_node}
            for c in confirm_actions:
                nodes_by_key[c["key"]] = c
            section_parent = {"kind": "Section", "key": "section1"}
            return preview_action, nodes_by_key, section_parent

        def run_pairing_check(preview_action, nodes_by_key, section_parent):
            errs = []
            translator_impl.validate_admin_runtime_preview_action_pairing(
                preview_action, nodes_by_key, errs, parent=section_parent,
            )
            return [e["ruleId"] for e in errs]

        missing_dryrun_action, missing_dryrun_nodes, missing_dryrun_parent = build_preview_confirm_pair(
            preview_payload_from={"groupName": "node:name_input.value"},
        )
        expect(
            "108. validate_admin_runtime_preview_action_pairing rejects a Section-owned preview Action whose payloadFrom omits dryRun via ADMIN_RUNTIME_PREVIEW_ACTION_DRYRUN_REQUIRED",
            "ADMIN_RUNTIME_PREVIEW_ACTION_DRYRUN_REQUIRED"
            in run_pairing_check(missing_dryrun_action, missing_dryrun_nodes, missing_dryrun_parent),
        )

        confirmed_present_action, confirmed_present_nodes, confirmed_present_parent = build_preview_confirm_pair(
            preview_payload_from={
                "groupName": "node:name_input.value", "dryRun": "literal:true", "confirmed": "literal:true",
            },
        )
        expect(
            "109. validate_admin_runtime_preview_action_pairing rejects a Section-owned preview Action whose payloadFrom ALSO declares confirmed via ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRMED_NOT_ALLOWED",
            "ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRMED_NOT_ALLOWED"
            in run_pairing_check(confirmed_present_action, confirmed_present_nodes, confirmed_present_parent),
        )

        missing_secondary_action, missing_secondary_nodes, missing_secondary_parent = build_preview_confirm_pair(
            secondary=None,
        )
        expect(
            "110. validate_admin_runtime_preview_action_pairing rejects a Section-owned admin_runtime Action with NO secondaryDisclosureAction at all via ADMIN_RUNTIME_PREVIEW_ACTION_SECONDARY_OPEN_MODAL_REQUIRED (never a silently-accepted bare mutation)",
            "ADMIN_RUNTIME_PREVIEW_ACTION_SECONDARY_OPEN_MODAL_REQUIRED"
            in run_pairing_check(missing_secondary_action, missing_secondary_nodes, missing_secondary_parent),
        )

        wrong_secondary_action, wrong_secondary_nodes, wrong_secondary_parent = build_preview_confirm_pair(
            secondary=("closeModal", "confirm_modal"),
        )
        expect(
            "111. validate_admin_runtime_preview_action_pairing rejects a Section-owned admin_runtime Action whose secondaryDisclosureActionType is closeModal (not openModal) via ADMIN_RUNTIME_PREVIEW_ACTION_SECONDARY_OPEN_MODAL_REQUIRED",
            "ADMIN_RUNTIME_PREVIEW_ACTION_SECONDARY_OPEN_MODAL_REQUIRED"
            in run_pairing_check(wrong_secondary_action, wrong_secondary_nodes, wrong_secondary_parent),
        )

        # secondaryDisclosureAction target existence/kind is validate_disclosure_targets' own
        # authority (104-107 above), never duplicated in validate_admin_runtime_preview_action_pairing
        # -- proven here specifically THROUGH the secondaryDisclosureAction path (104-107 only
        # exercised the PRIMARY disclosureActionType path), since _disclosure_target_checks_for_node
        # yields from both independently.
        missing_target_secondary_errors = []
        translator_impl.validate_disclosure_targets(
            {
                "_path": "$.test.open_btn_missing_target", "key": "open_btn_missing_target",
                "secondaryDisclosureAction": {"actionType": "openModal", "targetNodeId": None},
            },
            {}, missing_target_secondary_errors,
        )
        expect(
            "112. validate_disclosure_targets rejects a secondaryDisclosureAction (the preview button's own Modal-opening secondary) with a missing targetNodeId via DISCLOSURE_TARGET_NODE_REQUIRED, same authority as the primary disclosureActionType path",
            "DISCLOSURE_TARGET_NODE_REQUIRED" in [e["ruleId"] for e in missing_target_secondary_errors],
        )

        wrong_kind_secondary_errors = []
        translator_impl.validate_disclosure_targets(
            {
                "_path": "$.test.open_btn_wrong_kind_target", "key": "open_btn_wrong_kind_target",
                "secondaryDisclosureAction": {"actionType": "openModal", "targetNodeId": "not_a_modal"},
            },
            {"not_a_modal": "Section"}, wrong_kind_secondary_errors,
        )
        expect(
            "113. validate_disclosure_targets rejects a secondaryDisclosureAction targeting a real node of the wrong kind (Section, not Modal) via DISCLOSURE_TARGET_KIND_MISMATCH, same authority as the primary disclosureActionType path",
            "DISCLOSURE_TARGET_KIND_MISMATCH" in [e["ruleId"] for e in wrong_kind_secondary_errors],
        )

        target_ref_mismatch_action, target_ref_mismatch_nodes, target_ref_mismatch_parent = build_preview_confirm_pair(
            confirm_target_ref="manifest:00000000-0000-0000-0000-0000000ae999:enum_dictionary:some_other_action",
        )
        expect(
            "114. validate_admin_runtime_preview_action_pairing rejects a preview Action whose target_ref differs from its Modal's own Confirm button's target_ref via ADMIN_RUNTIME_PREVIEW_ACTION_TARGET_REF_MISMATCH (preview and confirm resolving different manifests/actions is a real authoring defect, not cosmetic)",
            "ADMIN_RUNTIME_PREVIEW_ACTION_TARGET_REF_MISMATCH"
            in run_pairing_check(target_ref_mismatch_action, target_ref_mismatch_nodes, target_ref_mismatch_parent),
        )

        payload_mismatch_action, payload_mismatch_nodes, payload_mismatch_parent = build_preview_confirm_pair(
            confirm_payload_from={"groupName": "node:a_DIFFERENT_input.value", "confirmed": "literal:true"},
        )
        expect(
            "115. validate_admin_runtime_preview_action_pairing rejects a preview Action whose business-field payloadFrom differs from its Modal's own Confirm button's business-field payloadFrom via ADMIN_RUNTIME_PREVIEW_ACTION_PAYLOAD_FIELDS_MISMATCH",
            "ADMIN_RUNTIME_PREVIEW_ACTION_PAYLOAD_FIELDS_MISMATCH"
            in run_pairing_check(payload_mismatch_action, payload_mismatch_nodes, payload_mismatch_parent),
        )

        ambiguous_confirm_action, ambiguous_confirm_nodes, ambiguous_confirm_parent = build_preview_confirm_pair(
            confirm_count=2,
        )
        expect(
            "116. validate_admin_runtime_preview_action_pairing rejects a preview Action whose target Modal contains TWO admin_runtime_dispatch_override_wiring children via ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRM_TARGET_AMBIGUOUS (never silently pairing against the first match)",
            "ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRM_TARGET_AMBIGUOUS"
            in run_pairing_check(ambiguous_confirm_action, ambiguous_confirm_nodes, ambiguous_confirm_parent),
        )

        no_confirm_action, no_confirm_nodes, no_confirm_parent = build_preview_confirm_pair(confirm_count=0)
        expect(
            "117. validate_admin_runtime_preview_action_pairing rejects a preview Action whose target Modal contains ZERO admin_runtime_dispatch_override_wiring children via ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRM_TARGET_AMBIGUOUS",
            "ADMIN_RUNTIME_PREVIEW_ACTION_CONFIRM_TARGET_AMBIGUOUS"
            in run_pairing_check(no_confirm_action, no_confirm_nodes, no_confirm_parent),
        )

        valid_pair_action, valid_pair_nodes, valid_pair_parent = build_preview_confirm_pair()
        expect(
            "118. validate_admin_runtime_preview_action_pairing positive control: a fully-matching dryRun preview + openModal secondary + single same-target_ref/same-fields Confirm produces zero errors, proving 108-117 fail for the right reason and not because every input fails",
            run_pairing_check(valid_pair_action, valid_pair_nodes, valid_pair_parent) == [],
        )

        not_section_owned_action, not_section_owned_nodes, _ = build_preview_confirm_pair(
            preview_payload_from={"groupName": "node:name_input.value"},
        )
        expect(
            "119. validate_admin_runtime_preview_action_pairing does not apply this rule at all to a Modal-owned (not Section-owned) admin_runtime_dispatch_override_wiring Action -- the real Confirm button itself, which legitimately has no dryRun/openModal pairing of its own",
            run_pairing_check(not_section_owned_action, not_section_owned_nodes, {"kind": "Modal", "key": "some_modal"}) == [],
        )

        # 119a-119b (round 3, preview-gap audit): the RESOLVED Confirm button's own payloadFrom
        # authority, not merely lane membership plus Modal position, is now checked.
        confirm_missing_confirmed_action, confirm_missing_confirmed_nodes, confirm_missing_confirmed_parent = (
            build_preview_confirm_pair(
                confirm_payload_from={"groupName": "node:name_input.value"},
            )
        )
        expect(
            "119a. validate_admin_runtime_preview_action_pairing rejects a resolved Confirm button whose own payloadFrom omits confirmed=literal:true via ADMIN_RUNTIME_CONFIRM_ACTION_CONFIRMED_LITERAL_TRUE_REQUIRED (lane membership plus Modal position alone is not write-confirmation authority proof)",
            "ADMIN_RUNTIME_CONFIRM_ACTION_CONFIRMED_LITERAL_TRUE_REQUIRED"
            in run_pairing_check(
                confirm_missing_confirmed_action, confirm_missing_confirmed_nodes, confirm_missing_confirmed_parent,
            ),
        )

        confirm_leftover_dryrun_action, confirm_leftover_dryrun_nodes, confirm_leftover_dryrun_parent = (
            build_preview_confirm_pair(
                confirm_payload_from={
                    "groupName": "node:name_input.value", "confirmed": "literal:true", "dryRun": "literal:true",
                },
            )
        )
        expect(
            "119b. validate_admin_runtime_preview_action_pairing rejects a resolved Confirm button whose own payloadFrom carries a leftover dryRun flag via ADMIN_RUNTIME_CONFIRM_ACTION_EXTRA_AUTHORITY_FLAG_NOT_ALLOWED (a Confirm button must never carry any authority flag besides confirmed)",
            "ADMIN_RUNTIME_CONFIRM_ACTION_EXTRA_AUTHORITY_FLAG_NOT_ALLOWED"
            in run_pairing_check(
                confirm_leftover_dryrun_action, confirm_leftover_dryrun_nodes, confirm_leftover_dryrun_parent,
            ),
        )

        # 119c-119f (round 3, preview-gap audit): validate_table_display_columns_and_rows_source --
        # displayColumns/rowsSource are now an SSOT-defined, independently-validated data carrier
        # rather than a translator-hardcoded literal (see the split_flat_records_into_adoption_
        # candidates topology_ui_table branch, which now reads record["rowsSource"] instead of
        # unconditionally emitting "emission.data").
        def run_table_check(node):
            errs = []
            translator_impl.validate_table_display_columns_and_rows_source(node, errs)
            return [e["ruleId"] for e in errs]

        malformed_columns_table = {
            "kind": "Table", "key": "malformed_table", "_path": "$.test.malformed_table",
            "displayColumns": [{"key": "groupName", "header": "Group name"}],
            "_rawDisplayColumns": "groupName:Group name,badsegment_no_colon",
            "rowsSource": "emission.data",
        }
        expect(
            "119c. validate_table_display_columns_and_rows_source rejects a displayColumns segment missing the ':' key:header separator via TABLE_DISPLAY_COLUMNS_MALFORMED (never silently dropped even though parse_columns' own lenient parsing omits it from the returned column list)",
            "TABLE_DISPLAY_COLUMNS_MALFORMED" in run_table_check(malformed_columns_table),
        )

        missing_rows_source_table = {
            "kind": "Table", "key": "missing_rows_source_table", "_path": "$.test.missing_rows_source_table",
            "displayColumns": [{"key": "groupName", "header": "Group name"}],
            "_rawDisplayColumns": "groupName:Group name",
            "rowsSource": "",
        }
        expect(
            "119d. validate_table_display_columns_and_rows_source rejects a Table with displayColumns authored but no rowsSource via TABLE_ROWS_SOURCE_REQUIRED_WITH_DISPLAY_COLUMNS (a data-bound table needs both a column list and a rows source)",
            "TABLE_ROWS_SOURCE_REQUIRED_WITH_DISPLAY_COLUMNS" in run_table_check(missing_rows_source_table),
        )

        invalid_rows_source_table = {
            "kind": "Table", "key": "invalid_rows_source_table", "_path": "$.test.invalid_rows_source_table",
            "displayColumns": [{"key": "groupName", "header": "Group name"}],
            "_rawDisplayColumns": "groupName:Group name",
            "rowsSource": "enum.groups",
        }
        expect(
            "119e. validate_table_display_columns_and_rows_source rejects a rowsSource equal to a domain/table identifier (e.g. the Table's own `source` attribute value) rather than an emission.data JSON traversal path via TABLE_ROWS_SOURCE_INVALID",
            "TABLE_ROWS_SOURCE_INVALID" in run_table_check(invalid_rows_source_table),
        )

        valid_table = {
            "kind": "Table", "key": "valid_table", "_path": "$.test.valid_table",
            "displayColumns": [{"key": "groupName", "header": "Group name"}],
            "_rawDisplayColumns": "groupName:Group name",
            "rowsSource": "emission.data",
        }
        expect(
            "119f. validate_table_display_columns_and_rows_source positive control: well-formed displayColumns paired with a valid emission.data rowsSource produces zero errors, proving 119c-119e fail for the right reason and not because every input fails",
            run_table_check(valid_table) == [],
        )

        # 120-123: canonical-generation regression guard for the REAL admin-enum-ae200 fixture
        # (preview-gap round) -- proves generate-topology-seed alone (no manual extraction/merge
        # from a previously-hand-patched db/seed_empty.sql) reproduces enum_table's own
        # propsJson/propBindings and every *_confirm_modal's own propsJson, and that the new
        # Section-owned preview Action pairing check (108-119 above) passes cleanly against the
        # real fixture, not just hand-built unit nodes.
        proc_ae200, doc_ae200 = run_generate_topology_seed(ADMIN_ENUM_AE200_TOPOLOGY_SEED_FIXTURE)
        expect(
            "120. real admin-enum-ae200 fixture's generate-topology-seed run reports gateStatus == pass",
            doc_ae200 is not None and doc_ae200.get("gateStatus") == "pass",
        )
        ae200_tensor_nodes = (
            dig(doc_ae200, "adoptionCandidates", "tensorAdoptionCandidates")[0]["layoutPatchJson"]["nodes"]
            if doc_ae200 and dig(doc_ae200, "adoptionCandidates", "tensorAdoptionCandidates")
            else []
        )
        ae200_nodes_by_id = {n["nodeId"]: n for n in ae200_tensor_nodes}
        expect(
            "121. real admin-enum-ae200 fixture's generate-topology-seed run alone (no post-generation manual patch) produces enum_table's own propsJson (columns) and propBindings (rows: emission.data.groups) -- the content round 30 could only reproduce by hand-extracting it from the previous seed; round 37 moved rowsSource from emission.data to emission.data.groups (list_groups' response envelope became {groups, groupOptions} so enum_group_filter's own options no longer self-shrink to the same narrowed array enum_table's rows read from)",
            "enum_table" in ae200_nodes_by_id
            and ae200_nodes_by_id["enum_table"].get("propsJson") is not None
            and "groupName" in ae200_nodes_by_id["enum_table"]["propsJson"]
            and ae200_nodes_by_id["enum_table"].get("propBindings") == {"rows": {"source": "emission.data.groups"}},
        )
        ae200_modal_ids = [nid for nid in ae200_nodes_by_id if nid.endswith("_confirm_modal")]
        expect(
            "122. real admin-enum-ae200 fixture's generate-topology-seed run alone produces propsJson (open/title/body) for all 7 *_confirm_modal nodes -- the content round 30 could only reproduce by hand-extracting it from the previous seed",
            len(ae200_modal_ids) == 7
            and all(ae200_nodes_by_id[nid].get("propsJson") is not None for nid in ae200_modal_ids)
            and all('"open": false' in ae200_nodes_by_id[nid]["propsJson"] for nid in ae200_modal_ids),
        )
        expect(
            "123. real admin-enum-ae200 fixture's generate-topology-seed run reports zero validationErrors, proving the new Section-owned preview-pairing check (108-119) passes cleanly against production content, not only hand-built unit-test nodes",
            doc_ae200 is not None and doc_ae200.get("validationErrors") == [],
        )

        # 123a (admin-enum subBundle closure round): the 7 write-flow typed fields must resolve
        # control=form_input/input (a real <Input> with onChange, frontend/runtime/
        # runtimeComponentFactory.ts inputFactory), never form_input/form_field
        # (formFieldFactory renders FormField.tsx with a hardcoded empty span child -- no <input>
        # element, no onChange, the live node value tracker never gets a value for that node, and
        # every one of the 7 write actions' typed-value payloadFrom sourced from it would fail
        # closed with PAYLOAD_FROM_NODE_NOT_FOUND in real production). This regression guard reads
        # the REAL generated layoutAdoptionCandidates records (not a hand-built node), so it can
        # only pass if the actual shipped fixture is correct.
        ae200_layout_records = (
            dig(doc_ae200, "adoptionCandidates", "layoutAdoptionCandidates")[0]["layoutSchemaJson"]["records"]
            if doc_ae200 and dig(doc_ae200, "adoptionCandidates", "layoutAdoptionCandidates")
            else []
        )
        ae200_field_control_by_key = {
            r["record"]["key"]: r["record"].get("control")
            for r in ae200_layout_records
            if r["record"].get("recordType") == "topology_ui_field"
        }
        ae200_write_flow_typed_field_keys = [
            "enum_create_group_name_input",
            "enum_update_group_name_input",
            "enum_create_item_name_input",
            "enum_update_item_index_input",
            "enum_update_item_name_input",
            "enum_delete_item_index_input",
            "enum_set_group_items_input",
        ]
        expect(
            "123a. all 7 admin-enum write-flow typed fields resolve control=form_input/input (a real interactive <Input>), never form_input/form_field (a non-interactive label-only wrapper with no <input> element) -- proven against the REAL generated fixture output, not a hand-built substitute",
            all(
                ae200_field_control_by_key.get(k) == "form_input/input"
                for k in ae200_write_flow_typed_field_keys
            ),
        )

        # 124-125 (round 4, preview-gap audit round 4): split_flat_records_into_adoption_candidates
        # no longer substitutes "emission.data" as a default when a topology_ui_table record's own
        # rowsSource is missing (the round-3 fallback this round removed) -- these two tests prove
        # BOTH directions of that removal against the REAL production react schema candidate (a
        # mutated copy of the actual ae200 fixture, not a hand-built minimal node), so the proof
        # reflects what actually ships: (124) omitting rowsSource fails the WHOLE run closed rather
        # than silently defaulting, (125) an authored NON-default rowsSource is what actually
        # reaches propBindings.rows.source -- proving true data-driven generation rather than a
        # same-value coincidence with the (now-removed) hardcoded default.
        ae200_envelope_raw = json.loads(ADMIN_ENUM_AE200_TOPOLOGY_SEED_FIXTURE.read_text(encoding="utf-8"))
        ae200_candidate = json.loads(ae200_envelope_raw["inputText"])

        def write_ae200_variant_fixture(mutated_candidate):
            envelope = dict(ae200_envelope_raw)
            envelope["inputText"] = json.dumps(mutated_candidate)
            path = Path(tmpdir) / _next_tmp_name("ae200_variant_fixture")
            path.write_text(json.dumps(envelope), encoding="utf-8")
            return path

        missing_rows_source_candidate = json.loads(json.dumps(ae200_candidate))
        missing_rows_source_table_node = find_node(missing_rows_source_candidate["root"], "enum_table")
        if missing_rows_source_table_node is None:
            raise AssertionError("124/125 setup: enum_table node must exist in the real ae200 fixture")
        missing_rows_source_table_node["rowsSource"] = ""
        tmp124 = write_ae200_variant_fixture(missing_rows_source_candidate)
        proc124, doc124 = run_generate_topology_seed(tmp124)
        expect(
            "124. split_flat_records_into_adoption_candidates fails the WHOLE generate-topology-seed run closed (non-zero exit, no JSON document) when a topology_ui_table record reaches tensor generation with displayColumns authored but rowsSource missing -- never silently substitutes a translator-hardcoded default",
            proc124.returncode != 0 and doc124 is None,
        )

        NON_DEFAULT_ROWS_SOURCE = "emission.data.groups"
        non_default_candidate = json.loads(json.dumps(ae200_candidate))
        non_default_table_node = find_node(non_default_candidate["root"], "enum_table")
        if non_default_table_node is None:
            raise AssertionError("124/125 setup: enum_table node must exist in the real ae200 fixture")
        non_default_table_node["rowsSource"] = NON_DEFAULT_ROWS_SOURCE
        tmp125 = write_ae200_variant_fixture(non_default_candidate)
        proc125, doc125 = run_generate_topology_seed(tmp125)
        non_default_tensor_nodes = (
            dig(doc125, "adoptionCandidates", "tensorAdoptionCandidates")[0]["layoutPatchJson"]["nodes"]
            if doc125 and dig(doc125, "adoptionCandidates", "tensorAdoptionCandidates")
            else []
        )
        non_default_nodes_by_id = {n["nodeId"]: n for n in non_default_tensor_nodes}
        expect(
            "125. an authored NON-default rowsSource (emission.data.groups, distinct from the removed hardcoded default emission.data) reaches the generated tensor's propBindings.rows.source EXACTLY -- proving data-driven generation, not a same-value coincidence with the old hardcode",
            doc125 is not None
            and doc125.get("validationErrors") == []
            and non_default_nodes_by_id.get("enum_table", {}).get("propBindings")
            == {"rows": {"source": NON_DEFAULT_ROWS_SOURCE}},
        )

        # 126-131 (round 36, admin-enum subBundle closure): validate_field_admin_runtime_dispatch_
        # wiring -- a Field's own use of admin_runtime_dispatch_override_wiring is NEVER subject to
        # validate_admin_runtime_preview_action_pairing (Action-only, tests 108-119 above) or
        # validate_structural_node's ownership check (a Field is never Action-owned in the first
        # place); this is the SEPARATE, generic fail-close restricting a Field's own participation
        # in this lane to read/filter use only, closing the gap where a Field could otherwise carry
        # mutation authority (confirmed/dryRun), drive Modal state, or target a mutation action
        # entirely outside the Action-only Section-ownership/dryRun-preview-pairing rules.
        def run_field_dispatch_check(node):
            errs = []
            translator_impl.validate_field_admin_runtime_dispatch_wiring(node, errs)
            return [e["ruleId"] for e in errs]

        _UNSET = object()

        def build_field_dispatch_node(
            payload_from=None,
            secondary=None,
            target_ref="manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
            authority_marker="draft_or_projection_only",
            event_binding_authority="draft_or_projection_only",
            control=None,
            debounce_ms=_UNSET,
        ):
            # round 37: defaults are the CORRECT authorityMarker/authority pair (rule 4's required
            # value) so every negative test below isolates the ONE violation its own name/comment
            # describes, rather than incidentally also tripping FIELD_ADMIN_RUNTIME_DISPATCH_
            # AUTHORITY_MARKER_REQUIRED/MISMATCH alongside it. Tests that specifically target rule 4
            # override authority_marker/event_binding_authority explicitly. control defaults to None
            # (absent) -- rule 5 (debounceMs) only fires for control=="form_input/search_input", so
            # every pre-existing test above (126-130) that never passes control is unaffected by
            # rule 5's addition, same as before it existed.
            node = {
                "kind": "Field", "key": "test_dispatch_field", "_path": "$.test.test_dispatch_field",
                "authorityMarker": authority_marker,
                "eventBinding": {
                    "trigger": "change",
                    "wiringLane": "admin_runtime_dispatch_override_wiring",
                    "targetRef": target_ref,
                    "authority": event_binding_authority,
                    "payloadFrom": payload_from or {},
                },
            }
            if control is not None:
                node["control"] = control
            if debounce_ms is not _UNSET:
                node["debounceMs"] = debounce_ms
            if secondary is not None:
                node["secondaryDisclosureAction"] = secondary
            return node

        field_confirmed_flag_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value", "confirmed": "literal:true"},
        )
        expect(
            "126. validate_field_admin_runtime_dispatch_wiring rejects a Field's payloadFrom declaring confirmed=literal:true via FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_FLAG_NOT_ALLOWED -- a Field can never carry write-confirmation authority",
            "FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_FLAG_NOT_ALLOWED" in run_field_dispatch_check(field_confirmed_flag_node),
        )

        field_dryrun_flag_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value", "dryRun": "literal:true"},
        )
        expect(
            "127. validate_field_admin_runtime_dispatch_wiring rejects a Field's payloadFrom declaring dryRun=literal:true too via FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_FLAG_NOT_ALLOWED -- unlike an Action's Section-owned preview use, even dryRun is disallowed for a Field, since a Field can never pair into the Modal-Confirm shape dryRun's own safety meaning depends on",
            "FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_FLAG_NOT_ALLOWED" in run_field_dispatch_check(field_dryrun_flag_node),
        )

        field_modal_mutation_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            secondary={"trigger": "change", "actionType": "openModal", "targetNodeId": "some_modal"},
        )
        expect(
            "128. validate_field_admin_runtime_dispatch_wiring rejects a Field paired with a secondaryDisclosureAction via FIELD_ADMIN_RUNTIME_DISPATCH_MODAL_MUTATION_NOT_ALLOWED -- Modal open/close authority belongs to Action/Step's own disclosure_state_wiring lane, never a Field's own change event",
            "FIELD_ADMIN_RUNTIME_DISPATCH_MODAL_MUTATION_NOT_ALLOWED" in run_field_dispatch_check(field_modal_mutation_node),
        )

        field_mutation_target_node = build_field_dispatch_node(
            payload_from={"groupName": "node:test_dispatch_field.value"},
            target_ref="manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group",
        )
        expect(
            "129. validate_field_admin_runtime_dispatch_wiring rejects a Field whose targetRef resolves to a mutation action (create_group) via FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED -- round 37 positive allowlist (ADMIN_RUNTIME_READ_ACTIONS), not the removed verb-prefix denylist",
            "FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED" in run_field_dispatch_check(field_mutation_target_node),
        )

        for _mutation_suffix, _mutation_action in enumerate(("update_group", "delete_group", "set_group_items", "create_item", "update_item", "delete_item")):
            _mutation_node = build_field_dispatch_node(
                payload_from={"groupName": "node:test_dispatch_field.value"},
                target_ref=f"manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:{_mutation_action}",
            )
            expect(
                f"129{chr(97 + _mutation_suffix)}. validate_field_admin_runtime_dispatch_wiring also rejects a Field targeting '{_mutation_action}' via FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED -- proves the verb-prefix check covers every enum_dictionary write action, not just create_group",
                "FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED" in run_field_dispatch_check(_mutation_node),
            )

        # auth_users domain uses the SAME create/update/delete verb convention (docs/design/
        # admin-master-roster-management-ssot.yaml admin_runtime_actions) -- proves the check is a
        # shared, generic naming convention across BOTH admin_runtime action namespaces, never a
        # hardcoded enum_dictionary allowlist.
        auth_users_mutation_node = build_field_dispatch_node(
            payload_from={"status": "node:test_dispatch_field.value"},
            target_ref="manifest:00000000-0000-0000-0000-000000000a01:auth_users:update",
        )
        expect(
            "129g. validate_field_admin_runtime_dispatch_wiring also rejects a Field targeting the UNRELATED auth_users domain's 'update' action via FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED -- the verb-prefix convention is shared across admin_runtime action namespaces, not an enum_dictionary-only allowlist",
            "FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED" in run_field_dispatch_check(auth_users_mutation_node),
        )

        # 129h-129l (round 37): negative proof that the POSITIVE allowlist (ADMIN_RUNTIME_READ_
        # ACTIONS) rejects mutation actions whose leading verb was NEVER a member of the removed
        # MUTATION_ACTION_VERB_PREFIXES denylist ({"create", "update", "delete", "set"}) -- exactly
        # the class of action a verb-DENYLIST would have silently let through. A positive allowlist
        # rejects these by construction (absent from admin_runtime_actions' "_read" groups), not by
        # anyone having thought to add "apply"/"promote"/"import"/"deprecate" to a verb list.
        for _non_denylist_suffix, (_ndm_manifest, _ndm_action) in enumerate((
            ("00000000-0000-0000-0000-000000000m01", "manifest:promote"),
            ("00000000-0000-0000-0000-000000000m01", "manifest:deprecate"),
            ("00000000-0000-0000-0000-000000000m02", "layout_patch:apply"),
            ("00000000-0000-0000-0000-000000000m03", "seed_runtime:import"),
            ("00000000-0000-0000-0000-000000000m04", "package_generator:promote_package"),
        )):
            _ndm_node = build_field_dispatch_node(
                payload_from={"x": "node:test_dispatch_field.value"},
                target_ref=f"manifest:{_ndm_manifest}:{_ndm_action}",
            )
            expect(
                f"129{chr(104 + _non_denylist_suffix)}. validate_field_admin_runtime_dispatch_wiring rejects a Field targeting '{_ndm_action}' via FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED even though its leading verb was never a MUTATION_ACTION_VERB_PREFIXES member -- proves the round-37 positive allowlist catches what the removed verb-denylist would have silently passed",
                "FIELD_ADMIN_RUNTIME_DISPATCH_MUTATION_TARGET_NOT_ALLOWED" in run_field_dispatch_check(_ndm_node),
            )

        # 129m-129n (round 37): FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_REQUIRED /
        # _MISMATCH -- a Field participating in this lane must declare authorityMarker ==
        # "draft_or_projection_only" (the exchange_mapping.authority_mapping frontend_intent value),
        # independent of and in addition to rule 3's targetRef-resolved classification.
        field_authority_marker_missing_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            authority_marker=None,
            event_binding_authority=None,
        )
        expect(
            "129m. validate_field_admin_runtime_dispatch_wiring rejects a Field with no authorityMarker at all via FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_REQUIRED, even though its targetRef legitimately resolves to a read action",
            "FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_REQUIRED" in run_field_dispatch_check(field_authority_marker_missing_node),
        )

        field_authority_marker_wrong_value_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            authority_marker="validation_only",
            event_binding_authority="validation_only",
        )
        expect(
            "129n. validate_field_admin_runtime_dispatch_wiring rejects a Field whose authorityMarker is a DIFFERENT legal-for-the-lane value (validation_only) rather than the Field-specific required draft_or_projection_only, via FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_REQUIRED",
            "FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_REQUIRED" in run_field_dispatch_check(field_authority_marker_wrong_value_node),
        )

        field_authority_marker_disagreement_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            authority_marker="draft_or_projection_only",
            event_binding_authority="preview_only",
        )
        expect(
            "129o. validate_field_admin_runtime_dispatch_wiring rejects a Field whose authorityMarker is correct (draft_or_projection_only) but whose OWN eventBinding.authority disagrees, via FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_MISMATCH",
            "FIELD_ADMIN_RUNTIME_DISPATCH_AUTHORITY_MARKER_MISMATCH" in run_field_dispatch_check(field_authority_marker_disagreement_node),
        )

        field_valid_read_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
        )
        expect(
            "130. validate_field_admin_runtime_dispatch_wiring positive control: a Field dispatching to a read action (list_groups) with no authority flag, no secondaryDisclosureAction, and the correct authorityMarker produces zero errors, proving 126-129o fail for the right reason and not because every Field-owned use of this lane fails",
            run_field_dispatch_check(field_valid_read_node) == [],
        )

        # 130a-130d (round 37): FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS -- a
        # continuous-typing search Field (control="form_input/search_input") on this lane must
        # declare a valid positive-integer debounceMs; a discrete-choice control (e.g. select) is
        # never required to.
        field_search_no_debounce_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            control="form_input/search_input",
        )
        expect(
            "130a. validate_field_admin_runtime_dispatch_wiring rejects a search_input Field on this lane with NO debounceMs at all via FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS -- a continuous-typing search field must never dispatch uncontrolled on every keystroke",
            "FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS" in run_field_dispatch_check(field_search_no_debounce_node),
        )

        field_search_zero_debounce_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            control="form_input/search_input",
            debounce_ms=0,
        )
        expect(
            "130b. validate_field_admin_runtime_dispatch_wiring rejects a search_input Field whose debounceMs is 0 (non-positive) via FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS -- zero is not a valid debounce window, never silently treated as 'no delay'",
            "FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS" in run_field_dispatch_check(field_search_zero_debounce_node),
        )

        field_search_negative_debounce_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            control="form_input/search_input",
            debounce_ms=-100,
        )
        expect(
            "130c. validate_field_admin_runtime_dispatch_wiring rejects a search_input Field whose debounceMs is negative via FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS",
            "FIELD_ADMIN_RUNTIME_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS" in run_field_dispatch_check(field_search_negative_debounce_node),
        )

        field_search_valid_debounce_node = build_field_dispatch_node(
            payload_from={"search": "node:test_dispatch_field.value"},
            control="form_input/search_input",
            debounce_ms=300,
        )
        expect(
            "130d. validate_field_admin_runtime_dispatch_wiring positive control: a search_input Field with a valid positive-integer debounceMs (300) produces zero errors, proving 130a-130c fail for the right reason",
            run_field_dispatch_check(field_search_valid_debounce_node) == [],
        )

        field_select_no_debounce_node = build_field_dispatch_node(
            payload_from={"groupNameFilter": "node:test_dispatch_field.value"},
            control="form_input/select",
        )
        expect(
            "130e. validate_field_admin_runtime_dispatch_wiring does NOT require debounceMs for a discrete-choice control (form_input/select) on this lane -- rule 5 is scoped to search_input specifically, since a select fires once per selection, never once per keystroke",
            run_field_dispatch_check(field_select_no_debounce_node) == [],
        )

        # 131: the REAL admin-enum-ae200 fixture's own enum_search Field (the actual production
        # shape, not a hand-built substitute) passes this new check cleanly -- proves the fail-close
        # does not regress the legitimate generic read/filter use case it exists to still permit.
        enum_search_react_node = find_node(ae200_candidate["root"], "enum_search")
        expect(
            "131. the real admin-enum-ae200 fixture's own enum_search Field (control=form_input/search_input, targeting enum_dictionary:list_groups) exists, carries the admin_runtime_dispatch_override_wiring eventBinding, and produces zero FIELD_ADMIN_RUNTIME_DISPATCH_* errors -- proving the new fail-close was actually exercised against production content and passed for the right (permitted read/filter) reason",
            enum_search_react_node is not None
            and (enum_search_react_node.get("eventBinding") or {}).get("wiringLane") == "admin_runtime_dispatch_override_wiring"
            and not any(
                rid.startswith("FIELD_ADMIN_RUNTIME_DISPATCH_")
                for rid in run_field_dispatch_check(enum_search_react_node)
            )
            and doc_ae200 is not None
            and not any(
                e.get("ruleId", "").startswith("FIELD_ADMIN_RUNTIME_DISPATCH_")
                for e in (doc_ae200.get("validationErrors") or [])
            ),
        )

    print()
    if FAILURES:
        print(f"=== {len(FAILURES)} react-schema-topology-seed-translator check(s) failed ===", file=sys.stderr)
        return 1
    print(f"PASS check-react-schema-topology-seed-translator.py assertions={PASS_COUNT}")
    return 0


def dig(obj, *keys):
    cur = obj
    for k in keys:
        if not isinstance(cur, dict) or k not in cur:
            return None
        cur = cur[k]
    return cur


if __name__ == "__main__":
    sys.exit(main())
