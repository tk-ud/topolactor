#!/usr/bin/env python3
"""check_schema_seed_translator_entry_gate.py -- executable proof surface.

Core under test:
  .agent/scripts/agent_tools/schema_seed_translator_entry_gate.py
    (validate_translator_entry / validate_translator_entry_from_path)

This is the dedicated proof for the schema<->seed translator entry gate
CORE itself -- shape detection across all five declared input shapes, and
each required fail-close condition from
docs/design/react-schema-topology-seed-translator-ssot.yaml -- plus the two
wiring assertions that the gate is actually connected to translator entry
(not a parallel, disconnected preflight):

  - gate blocking -> react-schema-topology-seed-translator entry fails
    closed before running its conversion pipeline.
  - gate pass -> react-schema-topology-seed-translator entry proceeds to
    its existing (unmodified) conversion pipeline.

check_react_schema_topology_seed_translator.py remains the executable proof
for the translator's own generate-react-schema / generate-topology-seed
conversion contract; this script does not duplicate those assertions, only
the gate-core-specific ones (including the two wiring checks above, using
the translator CLI only as an observation point for wiring, not to
re-verify conversion correctness).
"""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GATE_CORE_DIR = REPO_ROOT / ".agent" / "scripts" / "agent_tools"
sys.path.insert(0, str(GATE_CORE_DIR))
import schema_seed_translator_entry_gate as gate  # noqa: E402

TRANSLATOR_TOOL = REPO_ROOT / ".agent" / "tools" / "react-schema-topology-seed-translator"
FIXTURES_DIR = REPO_ROOT / ".agent" / "tests" / "fixtures" / "react-schema-topology-seed-translator"
ENVELOPE_FIXTURE = FIXTURES_DIR / "credential-management-0092.input.json"
TOPOLOGY_SEED_ENVELOPE_FIXTURE = FIXTURES_DIR / "credential-management-0092.topology-seed.input.json"
REACT_SCHEMA_FIXTURE = FIXTURES_DIR / "physical-search-crud-aggregate.react-schema.json"
GENERATED_OUTPUT_FIXTURE = REPO_ROOT / ".agent" / "tools" / "generate" / "schema" / "translated.json"
AGENT_TMP_DIR = REPO_ROOT / ".agent" / "tmp"

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


def blocking_rule_ids(result):
    return [e.get("ruleId") for e in result.get("blockingGaps", [])]


def base_envelope(**overrides):
    payload = {
        "schemaId": "topolactor.translator_input.v1",
        "mode": "generate_react_schema",
        "targetSurface": "auth.external.credential_management.projection",
        "sourceYamlRefs": ["docs/design/react-schema-topology-seed-translator-ssot.yaml#declared_seed_surface_catalog"],
        "inputText": "[projection key=p label=x sourceYamlRefs=a]\n[/projection]\n",
    }
    payload.update(overrides)
    return payload


def main():
    ssot_root, ssot_errors = gate.translator.load_ssot(REPO_ROOT)
    expect("0. gate core can load the translator SSOT it validates against", ssot_root is not None and not ssot_errors)

    # --- shape detection + valid-input pass cases -------------------------

    envelope_fixture = json.loads(ENVELOPE_FIXTURE.read_text(encoding="utf-8"))
    result = gate.validate_translator_entry(envelope_fixture, expected_mode="generate_react_schema")
    expect("1. valid translator input envelope -> gateStatus pass", result["gateStatus"] == gate.GATE_STATUS_PASS)
    expect("2. valid envelope result declares detectedShape=translator_input_envelope", result["inputDetection"]["detectedShape"] == "translator_input_envelope")

    react_schema_fixture = json.loads(REACT_SCHEMA_FIXTURE.read_text(encoding="utf-8"))
    result = gate.validate_translator_entry(react_schema_fixture)
    expect("3. valid topolactor.react_schema.v1 candidate -> gateStatus pass", result["gateStatus"] == gate.GATE_STATUS_PASS)
    expect("4. react schema candidate result declares detectedShape=react_schema_candidate", result["inputDetection"]["detectedShape"] == "react_schema_candidate")

    topology_seed_envelope = json.loads(TOPOLOGY_SEED_ENVELOPE_FIXTURE.read_text(encoding="utf-8"))
    seed_gen_result = gate.validate_translator_entry(topology_seed_envelope, expected_mode="generate_topology_ui_seed")
    expect("5. valid translator input envelope (generate_topology_ui_seed mode) -> gateStatus pass", seed_gen_result["gateStatus"] == gate.GATE_STATUS_PASS)

    with tempfile.TemporaryDirectory(dir=str(AGENT_TMP_DIR)) as tmpdir:
        # Build a real topology_ui_seed_candidate by running the (already
        # gate-connected) translator once, then feed its topologyUiSeedCandidate
        # straight back into the gate as a standalone bare-shape input --
        # same JSON shape a human/agent would hand the gate for that fixture.
        proc = subprocess.run(
            [str(TRANSLATOR_TOOL), "generate-topology-seed", "--input", str(TOPOLOGY_SEED_ENVELOPE_FIXTURE)],
            cwd=str(REPO_ROOT), capture_output=True, text=True, timeout=30,
        )
        translator_doc = json.loads(proc.stdout) if proc.stdout else None
        topology_seed_candidate = (translator_doc or {}).get("topologyUiSeedCandidate")
        expect("6. golden fixture generate-topology-seed run produced a topologyUiSeedCandidate to gate-check", bool(topology_seed_candidate))

        result = gate.validate_translator_entry(topology_seed_candidate) if topology_seed_candidate else None
        expect("7. valid topolactor.topology_ui_seed.v1 candidate -> gateStatus pass", result is not None and result["gateStatus"] == gate.GATE_STATUS_PASS)
        expect("8. topology ui seed candidate result declares detectedShape=topology_ui_seed_candidate", result is not None and result["inputDetection"]["detectedShape"] == "topology_ui_seed_candidate")

    if GENERATED_OUTPUT_FIXTURE.is_file():
        translator_output_fixture = json.loads(GENERATED_OUTPUT_FIXTURE.read_text(encoding="utf-8"))
        result = gate.validate_translator_entry(translator_output_fixture)
        expect("9. valid translator output JSON -> gateStatus pass", result["gateStatus"] == gate.GATE_STATUS_PASS)
        expect("10. translator output result declares detectedShape=translator_output", result["inputDetection"]["detectedShape"] == "translator_output")
    else:
        fail("9/10. committed translator output fixture missing: .agent/tools/generate/schema/translated.json")

    # --- fail-close conditions ---------------------------------------------

    result = gate.validate_translator_entry("not valid json at all {")
    expect("11. invalid JSON -> unsupported_input_shape", result["gateStatus"] == gate.GATE_STATUS_UNSUPPORTED)

    result = gate.validate_translator_entry({"not": "a recognized shape"})
    expect("12. unsupported root shape -> unsupported_input_shape", result["gateStatus"] == gate.GATE_STATUS_UNSUPPORTED)

    result = gate.validate_translator_entry(base_envelope(mode="not_a_real_mode"))
    expect("13. unknown mode -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "INPUT_MODE_INVALID" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(targetSurface="__unknown_surface__", knownGapRefs=[]))
    expect("14. unresolved targetSurface with no knownGapRef -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "TARGET_SURFACE_UNRESOLVED" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(sourceYamlRefs=[]))
    expect("15. missing/empty sourceYamlRefs -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "SOURCE_YAML_REFS_EMPTY" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(mode="generate_topology_ui_seed", inputText="this is not json"))
    expect("16. generate_topology_ui_seed inputText not valid React schema JSON -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "INPUT_TEXT_NOT_VALID_JSON" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(mode="generate_topology_ui_seed", inputText=json.dumps({"schema": "topolactor.react_schema.v1"})))
    expect("17a. generate_topology_ui_seed inputText valid JSON but missing root -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "REACT_SCHEMA_CANDIDATE_ROOT_MISSING" in blocking_rule_ids(result))

    result = gate.validate_translator_entry({"schema": "topolactor.react_schema.v1", "presetKey": "x", "surface": "x", "sourceYamlRefs": ["a"]})
    expect("17b. bare react_schema_candidate missing root -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "REACT_SCHEMA_ROOT_FIELD_MISSING" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(
        inputText="[projection key=p label=x sourceYamlRefs=a]\n"
                   "[action key=a1 actionRef=content_bundle:search authorityMarker=validation_only sourceYamlRefs=a]\n"
                   "[/projection]\n",
    ))
    expect("18. missing wiringLane on an action -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "ACTION_OR_STEP_MISSING_WIRING_LANE" in blocking_rule_ids(result))

    result = gate.validate_translator_entry(base_envelope(
        inputText="[projection key=p label=x sourceYamlRefs=a]\n"
                   "[form key=f1 label=lbl target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n"
                   "[action key=a1 actionRef=content_bundle:search wiringLane=not_a_real_lane authorityMarker=validation_only sourceYamlRefs=a]\n"
                   "[/form]\n[/projection]\n",
    ))
    expect("19. unknown wiringLane value -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "UNRESOLVED_WIRING_LANE" in blocking_rule_ids(result))
    expect("19a. unknown wiringLane is surfaced in wiringObservations", "not_a_real_lane" in result["wiringObservations"]["wiringLanes_unknown"])

    result = gate.validate_translator_entry(base_envelope(
        inputText="[projection key=p label=x sourceYamlRefs=a]\n"
                   "[field key=f1 label=lbl control=totally/unknown_kind required=false sourceYamlRefs=a]\n"
                   "[/projection]\n",
    ))
    expect("20. unknown componentKind without knownGapRef -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "UNKNOWN_COMPONENT_KIND" in blocking_rule_ids(result))
    expect("20a. unknown componentKind is surfaced in componentObservations", "totally/unknown_kind" in result["componentObservations"]["componentKinds_unknown"])

    result = gate.validate_translator_entry(base_envelope(
        inputText="[projection key=p label=x sourceYamlRefs=a]\n"
                   "[form key=f1 label=lbl target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n"
                   "[action key=a1 actionRef=instance:db_instance_port:x:y wiringLane=external_instance_wiring "
                   "authorityMarker=validation_only payloadFrom=amount:not_a_recognized_source sourceYamlRefs=a]\n"
                   "[/form]\n[/projection]\n",
    ))
    expect("21. invalid payloadFrom source -> blocking", result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "PAYLOAD_FROM_UNRESOLVED_REF" in blocking_rule_ids(result))
    expect("21a. invalid payloadFrom source is surfaced in payloadObservations", "not_a_recognized_source" in result["payloadObservations"]["payloadFrom_sources_invalid"])

    result = gate.validate_translator_entry(base_envelope(
        inputText="[projection key=p label=x sourceYamlRefs=a]\n"
                   "[form key=f1 label=lbl target=t mode=edit authorityMarker=validation_only sourceYamlRefs=a]\n"
                   "[action key=a1 actionRef=ui-local:x.y wiringLane=internal_instance_wiring "
                   "authorityMarker=approved_runtime_execution_only sourceYamlRefs=a]\n"
                   "[/form]\n[/projection]\n",
    ))
    expect(
        "22. authority boundary mismatch (authority not allowed for the owning wiring lane) -> blocking",
        result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "AUTHORITY_NOT_ALLOWED_FOR_LANE" in blocking_rule_ids(result),
    )

    broken_schema = json.loads(json.loads(TOPOLOGY_SEED_ENVELOPE_FIXTURE.read_text(encoding="utf-8"))["inputText"])
    broken_schema["root"]["kind"] = "NotARealKind"
    result = gate.validate_translator_entry(base_envelope(mode="generate_topology_ui_seed", inputText=json.dumps(broken_schema)))
    expect(
        "23. schema<->seed mapping key mismatch (unmapped react schema node kind) -> blocking",
        result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "REACT_NODE_KIND_UNMAPPED" in blocking_rule_ids(result),
    )
    expect("23a. mapping mismatch is surfaced in schemaSeedMappingObservations", any(v.get("ruleId") == "REACT_NODE_KIND_UNMAPPED" for v in result["schemaSeedMappingObservations"]["mapping_rule_violations"]))

    labelless_schema = json.loads(json.loads(TOPOLOGY_SEED_ENVELOPE_FIXTURE.read_text(encoding="utf-8"))["inputText"])
    labelless_schema["root"]["children"][2]["children"][0]["children"][0]["children"][1]["label"] = None
    result = gate.validate_translator_entry(base_envelope(mode="generate_topology_ui_seed", inputText=json.dumps(labelless_schema)))
    expect(
        "24. topology seed record required field missing (null label deep in supplied schema) -> blocking",
        result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "SEED_RECORD_MISSING_REQUIRED_FIELD" in blocking_rule_ids(result),
    )

    incomplete_seed_candidate = {
        "schema": "topolactor.topology_ui_seed.v1", "seedKey": "x", "surface": "x",
        "sourceReactSchema": {}, "sourceYamlRefs": ["a"], "exchangeReport": {"sourceSchemaId": "x"},
        "projections": [{"recordType": "topology_ui_projection", "key": "p", "sourceYamlRefs": ["a"], "sourceReactPath": "$.root", "knownGapRefs": []}],
    }
    result = gate.validate_translator_entry(incomplete_seed_candidate)
    expect(
        "25. bare topology_ui_seed_candidate with a record missing a required common field (label) -> blocking",
        result["gateStatus"] == gate.GATE_STATUS_BLOCKING and "SEED_RECORD_MISSING_REQUIRED_FIELD" in blocking_rule_ids(result),
    )

    # --- gate core boundary: never SSOT/proof/seed-adoption authority, never writes ---

    boundary = result["boundary"]
    expect("26. gate core boundary declares read_only and no db/api/write side effects", boundary.get("read_only") is True and boundary.get("db_connection") is False and boundary.get("external_api_connection") is False and boundary.get("ai_api_call") is False and boundary.get("writes_repo_files") is False)
    expect("27. gate core boundary explicitly disclaims proof/SSOT/seed-adoption authority", "not_ssot_authority" in boundary.get("authority_boundary", "") and "not_proof_completion" in boundary.get("authority_boundary", "") and "not_seed_adoption_authority" in boundary.get("authority_boundary", ""))
    expect("28. gate core never claims to run translator conversion", boundary.get("runs_translator_conversion") is False)

    # --- wiring: translator entry actually calls this same core ------------

    with tempfile.TemporaryDirectory(dir=str(AGENT_TMP_DIR)) as tmpdir:
        bad_envelope_path = Path(tmpdir) / "bad_envelope.json"
        bad_envelope_path.write_text(json.dumps(base_envelope(sourceYamlRefs=[])), encoding="utf-8")
        proc = subprocess.run(
            [str(TRANSLATOR_TOOL), "generate-react-schema", "--input", str(bad_envelope_path)],
            cwd=str(REPO_ROOT), capture_output=True, text=True, timeout=30,
        )
        try:
            doc = json.loads(proc.stdout)
        except json.JSONDecodeError:
            doc = None
        expect(
            "29. gate blocking -> translator entry fails closed before conversion (reactSchemaCandidate stays None, exit non-zero)",
            proc.returncode != 0 and doc is not None and doc.get("gateStatus") == "blocking" and doc.get("reactSchemaCandidate") is None,
        )
        expect("30. gate-blocked translator entry output still carries the underlying blocking ruleId", doc is not None and any(e.get("ruleId") == "SOURCE_YAML_REFS_EMPTY" for e in doc.get("validationErrors", [])))

        proc_pass = subprocess.run(
            [str(TRANSLATOR_TOOL), "generate-react-schema", "--input", str(ENVELOPE_FIXTURE)],
            cwd=str(REPO_ROOT), capture_output=True, text=True, timeout=30,
        )
        try:
            doc_pass = json.loads(proc_pass.stdout)
        except json.JSONDecodeError:
            doc_pass = None
        expect(
            "31. gate pass -> translator entry proceeds to existing conversion (reactSchemaCandidate populated)",
            doc_pass is not None and doc_pass.get("gateStatus") == "pass" and bool(doc_pass.get("reactSchemaCandidate")),
        )

        bad_seed_envelope_path = Path(tmpdir) / "bad_seed_envelope.json"
        bad_seed_payload = json.loads(TOPOLOGY_SEED_ENVELOPE_FIXTURE.read_text(encoding="utf-8"))
        bad_seed_payload["inputText"] = "not valid json"
        bad_seed_envelope_path.write_text(json.dumps(bad_seed_payload), encoding="utf-8")
        proc_seed_blocking = subprocess.run(
            [str(TRANSLATOR_TOOL), "generate-topology-seed", "--input", str(bad_seed_envelope_path)],
            cwd=str(REPO_ROOT), capture_output=True, text=True, timeout=30,
        )
        try:
            doc_seed_blocking = json.loads(proc_seed_blocking.stdout)
        except json.JSONDecodeError:
            doc_seed_blocking = None
        expect(
            "32. gate blocking on generate-topology-seed entry -> fails closed before conversion (topologyUiSeedCandidate stays None)",
            proc_seed_blocking.returncode != 0 and doc_seed_blocking is not None
            and doc_seed_blocking.get("gateStatus") == "blocking" and doc_seed_blocking.get("topologyUiSeedCandidate") is None,
        )

    # --- translator entry never reads db/*.sql; generate-topology-seed / round-trip-check status ---
    #
    # check_react_schema_topology_seed_translator.py's own check 23 already
    # rigorously proves the translator body's executable code (docstrings
    # and `#` comments excluded, since the db/*.sql boundary is explained
    # there) carries zero db/*.sql references; not re-implemented here to
    # avoid a second, differently-shaped copy of that same stripping logic.
    # This gate-core proof only re-checks the gate core module itself, which
    # this test added.

    gate_core_text = (GATE_CORE_DIR / "schema_seed_translator_entry_gate.py").read_text(encoding="utf-8")
    expect("33. gate core module itself never reads db/*.sql", ".sql" not in gate_core_text.lower() and "db/" not in gate_core_text)

    proc_rtc = subprocess.run([str(TRANSLATOR_TOOL), "round-trip-check", "--input", str(ENVELOPE_FIXTURE)], cwd=str(REPO_ROOT), capture_output=True, text=True, timeout=30)
    try:
        doc_rtc = json.loads(proc_rtc.stdout)
    except json.JSONDecodeError:
        doc_rtc = None
    expect("34. round-trip-check remains not_implemented_out_of_scope (gate wiring did not change its status)", proc_rtc.returncode != 0 and doc_rtc is not None and doc_rtc.get("status") == "not_implemented_out_of_scope")

    print()
    if FAILURES:
        print(f"=== {len(FAILURES)} schema-seed-translator-entry-gate check(s) failed ===", file=sys.stderr)
        return 1
    print(f"PASS check_schema_seed_translator_entry_gate.py assertions={PASS_COUNT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
