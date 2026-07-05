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
AGENT_TMP_DIR = REPO_ROOT / ".agent" / "tmp"
SEED_EMPTY_PATH = REPO_ROOT / "db" / "seed_empty.sql"

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

        # 7c. envelope-level knownGapRefs are not silently dropped: they must
        # surface in both unresolvedGaps and exchangeReport.knownGapRefs.
        fixture_envelope = json.loads(FIXTURE.read_text(encoding="utf-8"))
        envelope_gaps = set(fixture_envelope.get("knownGapRefs") or [])
        expect(
            "7c. envelope-level knownGapRefs propagate into unresolvedGaps and exchangeReport.knownGapRefs",
            bool(envelope_gaps)
            and envelope_gaps.issubset(set((doc or {}).get("unresolvedGaps") or []))
            and envelope_gaps.issubset(set(dig(doc or {}, "exchangeReport", "knownGapRefs") or [])),
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

        expect("35. envelope-level knownGapRefs propagate into topology-seed unresolvedGaps and exchangeReport.knownGapRefs", "instance_settings_projection_category_not_yet_represented" in ((doc_ts or {}).get("unresolvedGaps") or []) and "instance_settings_projection_category_not_yet_represented" in (dig(doc_ts or {}, "exchangeReport", "knownGapRefs") or []))

        no_active_topology_write_claim = "activeTopology" not in json.dumps(tuc) and "runtimeExecute" not in json.dumps(tuc)
        expect("36. topologyUiSeedCandidate carries no active-topology/execution-authority claim", no_active_topology_write_claim)

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
