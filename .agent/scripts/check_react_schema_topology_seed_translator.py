#!/usr/bin/env python3
"""check_react_schema_topology_seed_translator.py -- executable proof surface.

SSOT: docs/design/react-schema-topology-seed-translator-ssot.yaml
Tool under test: .agent/tools/react-schema-topology-seed-translator
                 (-> .agent/scripts/react_schema_topology_seed_translator.py)
Fixture: .agent/tests/fixtures/react-schema-topology-seed-translator/credential-management-0092.input.json

Runs the 23 acceptance checks from the credential-management-0092
generate-react-schema implementation task: the golden fixture end to end,
plus negative-path scenarios built as ephemeral tmp fixtures (no extra
committed fixture files needed per scenario). Structured JSON assertions
live here (Python3 stdlib only); the paired check-*.sh is a bash CI
entrypoint/orchestration wrapper only.
"""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL = REPO_ROOT / ".agent" / "tools" / "react-schema-topology-seed-translator"
FIXTURE = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator" / "credential-management-0092.input.json"
AGENT_TMP_DIR = REPO_ROOT / ".agent" / "tmp"

SCENARIO_UUID = "bad04ed9-7d39-4cfc-a22c-db2684d4cb0a"

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


def rule_ids(doc):
    return [e.get("ruleId") for e in (doc or {}).get("validationErrors", [])]


def write_tmp_fixture(input_text, base=None, tmpdir=None):
    payload = dict(base) if base else {
        "schemaId": "topolactor.translator_input.v1",
        "mode": "generate_react_schema",
        "targetSurface": "auth.external.credential_management.projection",
        "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
    }
    payload["inputText"] = input_text
    path = Path(tmpdir) / "tmp_fixture.json"
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

        seed_evidence = (doc or {}).get("seedEvidence") or {}
        expect("4. seedEvidence.screenUuidNamespace == manifest.manifest_id", seed_evidence.get("screenUuidNamespace") == "manifest.manifest_id")
        expect("5. seedEvidence.screenUuid == 00000000-0000-0000-0000-000000000092", seed_evidence.get("screenUuid") == "00000000-0000-0000-0000-000000000092")
        expect("6. seedEvidence.manifestKey == auth.external.credential_management.projection", seed_evidence.get("manifestKey") == "auth.external.credential_management.projection")
        expect("7. seedEvidence.relatedAuthUserBoundaryManifestId == 00000000-0000-0000-0000-000000000091", seed_evidence.get("relatedAuthUserBoundaryManifestId") == "00000000-0000-0000-0000-000000000091")

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

        # 20 / 21. unimplemented modes fail closed
        proc20 = run_tool(["generate-topology-seed", "--input", str(FIXTURE)])
        try:
            doc20 = json.loads(proc20.stdout)
        except json.JSONDecodeError:
            doc20 = None
        expect("20. generate-topology-seed returns not_implemented / out_of_scope", proc20.returncode != 0 and doc20 is not None and doc20.get("status") == "not_implemented_out_of_scope")

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

        # 23. tool does not treat db/*.sql as translator source authority (the
        # implementation only reads db/seed_empty.sql for seedEvidence, and its
        # own docstring/module states this explicitly; assert the boundary text
        # is present and no other db/*.sql path is referenced).
        other_sql_refs = [
            line for line in impl_text.splitlines()
            if ".sql" in line and "seed_empty.sql" not in line and "SEED_EMPTY_REL_PATH" not in line
        ]
        expect(
            "23. tool does not treat db/*.sql as translator source authority (only db/seed_empty.sql read, for seedEvidence only)",
            "translator input authority" in impl_text.lower() and not other_sql_refs,
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
