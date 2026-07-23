#!/usr/bin/env python3
"""schema_seed_translator_entry_gate.py -- schema<->seed translator entry gate core.

Independent, importable, read-only validation core for
docs/design/react-schema-topology-seed-translator-ssot.yaml's exchange
contract. Three callers share this single core instead of re-implementing
their own copy of the same validation:

  - .agent/tools/topology-seed-discussion (translator-entry-gate subcommand,
    dispatched from .agent/scripts/agent_tools/readonly_observation.py)
  - .agent/scripts/react_schema_topology_seed_translator.py (translator
    entry: cmd_generate_react_schema / cmd_generate_topology_seed call this
    core before running their own conversion pipeline)
  - .agent/scripts/check_schema_seed_translator_entry_gate.py (dedicated
    proof script) and .agent/scripts/check_react_schema_topology_seed_translator.py
    (existing translator proof script, which additionally checks this gate's
    pass/blocking/unsupported behavior)

This module never writes files, never connects to a DB/external API/AI API,
and never runs the translator's own conversion (generate-react-schema /
generate-topology-seed output assembly). It only classifies an input JSON
document's shape and re-uses the translator's own validation primitives
(imported, not duplicated) to decide entry-gate pass/blocking/unsupported.

Boundary: this core is translator-entry-gate authority only. It is not SSOT
authority, not proof completion, not seed adoption authority, and not
implemented/partial/not_started status evidence.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

_THIS_DIR = Path(__file__).resolve().parent           # .agent/scripts/agent_tools
_SCRIPTS_ROOT = _THIS_DIR.parent                        # .agent/scripts
if str(_SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_ROOT))
import react_schema_topology_seed_translator as translator  # noqa: E402

GATE_SCHEMA_ID = "topolactor.schema_seed_translator_entry_gate.v1"

GATE_STATUS_PASS = "pass"
GATE_STATUS_BLOCKING = "blocking"
GATE_STATUS_UNSUPPORTED = "unsupported_input_shape"

GATE_BOUNDARY = {
    "read_only": True,
    "writes_repo_files": False,
    "db_connection": False,
    "external_api_connection": False,
    "ai_api_call": False,
    "runs_translator_conversion": False,
    "runs_translator_output_assembly": False,
    "authority_boundary": (
        "translator_entry_gate_core_only_not_ssot_authority_not_proof_completion_"
        "not_seed_adoption_authority_not_implemented_status_evidence"
    ),
    "prohibited_judgments": [
        "implemented", "partial", "not_started", "proof_passed", "semantic_completion", "seed_adoption",
    ],
}

VALID_MODES = translator.VALID_MODES

REACT_SCHEMA_ROOT_REQUIRED_FIELDS = ["schema", "presetKey", "surface", "sourceYamlRefs", "root"]
TOPOLOGY_SEED_ROOT_REQUIRED_FIELDS = [
    "schema", "seedKey", "surface", "sourceReactSchema", "sourceYamlRefs", "projections", "exchangeReport",
]
TRANSLATOR_OUTPUT_REQUIRED_FIELDS = [
    "schemaId", "normalizedInputElements", "reactSchemaCandidate", "topologyUiSeedCandidate",
    "exchangeReport", "validationErrors", "unresolvedGaps", "reverseTranslationBlockers",
]

MAPPING_RULE_IDS = {
    "REACT_NODE_KIND_UNMAPPED",
    "SEED_RECORD_MISSING_REQUIRED_FIELD",
    "SEED_RECORD_EMPTY_SOURCE_YAML_REFS",
}

# Structured pointer to the non-SSOT seed authoring reference
# (docs/reference/seed-data-authoring-guide.md). This is the single
# definition every caller of validate_translator_entry /
# validate_translator_entry_from_path shares -- _build_result attaches it to
# every gateStatus (pass / blocking / unsupported_input_shape) so no caller
# path can silently drop it. It is deliberately data, not authority: the
# guide's own Document Status header declares "Authority: non-SSOT", and this
# entry repeats that boundary so a caller reading only the gate result JSON
# (never opening the guide itself) still sees the same authority_boundary
# text. Applicable SSOT (react-schema-topology-seed-translator-ssot.yaml and
# whatever surface-specific SSOT governs the seed being authored) remains the
# design authority; this reference is authoring/comparison guidance only.
AUTHORING_REFERENCES = [
    {
        "path": "docs/reference/seed-data-authoring-guide.md",
        "classification": "non_ssot_authoring_reference",
        "authority_boundary": (
            "non_ssot_authoring_and_comparison_guidance_only_applicable_ssot_remains_"
            "design_authority_on_conflict_ssot_wins"
        ),
        "purpose": [
            "canonical_carrier_comparison",
            "translator_adoption_boundary_explanation",
            "canonical_conformance_and_runtime_reachability_axis_separation",
        ],
    },
]

SEED_RECORD_CHILD_LIST_FIELDS = ("categories", "sections", "children", "fields", "actions", "steps", "columns")


# ---------------------------------------------------------------------------
# shape detection
# ---------------------------------------------------------------------------

def _detect_shape(doc):
    if not isinstance(doc, dict):
        return None
    if doc.get("schemaId") == "topolactor.translator_input.v1":
        return "translator_input_envelope"
    if doc.get("schema") == "topolactor.react_schema.v1":
        return "react_schema_candidate"
    if doc.get("schema") == "topolactor.topology_ui_seed.v1":
        return "topology_ui_seed_candidate"
    if doc.get("schemaId") == "topolactor.translator_output.v1":
        return "translator_output"
    return None


# ---------------------------------------------------------------------------
# observation walker (descriptive only; validate_wiring_node/validate_ui_catalog_node
# in react_schema_topology_seed_translator.py own the pass/fail decision -- this
# just summarizes what was seen, reusing the same regex/lookup helpers)
# ---------------------------------------------------------------------------

def _new_observations():
    return {
        "componentKinds_seen": [],
        "componentKinds_unknown": [],
        "styleRefs_seen": [],
        "styleRefs_unknown": [],
        "wiringLanes_seen": [],
        "wiringLanes_unknown": [],
        "payloadFrom_sources_seen": [],
        "payloadFrom_sources_invalid": [],
        "authorityMarkers_seen": [],
        "authorityMarkers_mismatched": [],
        "reactNodeKinds_seen": [],
        "seedRecordTypes_seen": [],
    }


def _observe_react_tree(node, lanes_def, declared_surface, obs=None):
    if obs is None:
        obs = _new_observations()
    if not isinstance(node, dict):
        return obs
    kind = node.get("kind")
    if kind and kind not in obs["reactNodeKinds_seen"]:
        obs["reactNodeKinds_seen"].append(kind)

    if kind == "Field":
        control = node.get("control")
        if control and control not in obs["componentKinds_seen"]:
            obs["componentKinds_seen"].append(control)
        allowed = set()
        if declared_surface:
            for k in (translator.dig(declared_surface, "component_catalog_refs", "componentKinds") or []):
                allowed.add(k.split(" ")[0])
        if control and allowed and control not in allowed and not node.get("knownGapRefs") and control not in obs["componentKinds_unknown"]:
            obs["componentKinds_unknown"].append(control)

    if kind == "StyleRef":
        token = node.get("tokenRef", "")
        if token and token not in obs["styleRefs_seen"]:
            obs["styleRefs_seen"].append(token)
        if token and not translator.TOKEN_LIKE_RE.match(token) and not node.get("knownGapRefs") and token not in obs["styleRefs_unknown"]:
            obs["styleRefs_unknown"].append(token)

    if kind in ("Action", "Step"):
        eb = node.get("eventBinding") or {}
        lane = eb.get("wiringLane")
        if lane and lane not in obs["wiringLanes_seen"]:
            obs["wiringLanes_seen"].append(lane)
        if lane and lane not in (lanes_def or {}) and lane not in obs["wiringLanes_unknown"]:
            obs["wiringLanes_unknown"].append(lane)
        authority = eb.get("authority")
        if authority and authority not in obs["authorityMarkers_seen"]:
            obs["authorityMarkers_seen"].append(authority)
        if authority != node.get("authorityMarker"):
            obs["authorityMarkers_mismatched"].append({
                "key": node.get("key"),
                "eventBindingAuthority": authority,
                "nodeAuthorityMarker": node.get("authorityMarker"),
            })
        payload_from = eb.get("payloadFrom") or {}
        for field, source in payload_from.items():
            src_kind = translator.classify_source_pattern(source)
            if src_kind and src_kind not in obs["payloadFrom_sources_seen"]:
                obs["payloadFrom_sources_seen"].append(src_kind)
            if src_kind is None and source not in obs["payloadFrom_sources_invalid"]:
                obs["payloadFrom_sources_invalid"].append(source)

    for child in node.get("children") or []:
        _observe_react_tree(child, lanes_def, declared_surface, obs)
    return obs


def _observe_seed_record_types(record, obs, acc_gap_refs):
    if not isinstance(record, dict):
        return
    record_type = record.get("recordType")
    if record_type and record_type not in obs["seedRecordTypes_seen"]:
        obs["seedRecordTypes_seen"].append(record_type)
    for gap in record.get("knownGapRefs") or []:
        if gap not in acc_gap_refs:
            acc_gap_refs.append(gap)
    for list_field in SEED_RECORD_CHILD_LIST_FIELDS:
        for child in record.get(list_field) or []:
            if isinstance(child, dict):
                _observe_seed_record_types(child, obs, acc_gap_refs)


def _lookup_declared_surface(ssot_root, target_surface):
    declared_surfaces = translator.dig(ssot_root, "declared_seed_surface_catalog", "known_declared_surfaces") or []
    return next((s for s in declared_surfaces if s.get("seed_surface_key") == target_surface), None)


# ---------------------------------------------------------------------------
# per-shape validation (reuses react_schema_topology_seed_translator.py
# functions directly -- never re-implements parsing/validation logic)
# ---------------------------------------------------------------------------

def _validate_envelope_shape(envelope, ssot_root, expected_mode):
    vocabulary = translator.protected_vocabulary(ssot_root)
    val_errors, mode, input_text, target_surface = translator.validate_input_envelope(envelope, ssot_root, vocabulary)
    errors = list(val_errors)

    if expected_mode and mode is not None and mode != expected_mode:
        errors.append(translator.err(
            "MODE_MISMATCH", "$.mode", "blocking",
            f"expected envelope mode '{expected_mode}', got '{mode}'",
        ))

    effective_mode = expected_mode or mode
    fatal = {"INPUT_TEXT_EMPTY", "SOURCE_YAML_REFS_EMPTY", "INPUT_MODE_INVALID", "MODE_MISMATCH"}
    observations = _new_observations()
    normalized = []
    known_gap_refs = list(envelope.get("knownGapRefs") or [])
    loss_entries = []

    if not any(e.get("ruleId") in fatal for e in errors):
        if effective_mode == "generate_react_schema":
            normalized, root_node, parse_errors = translator.parse_markup(input_text)
            errors.extend(parse_errors)
            if root_node is not None:
                translator.finalize_node(root_node)
                lanes_def = translator.dig(ssot_root, "wiring_lane_contract", "lanes") or {}
                declared_surface = _lookup_declared_surface(ssot_root, target_surface)
                tree_errors = []
                translator.walk_and_validate(root_node, lanes_def, declared_surface, tree_errors)
                errors.extend(tree_errors)
                observations = _observe_react_tree(root_node, lanes_def, declared_surface)
                for gap in translator.collect_known_gap_refs(root_node):
                    if gap not in known_gap_refs:
                        known_gap_refs.append(gap)

        elif effective_mode == "generate_topology_ui_seed":
            try:
                supplied_schema = json.loads(input_text)
            except json.JSONDecodeError as exc:
                errors.append(translator.err(
                    "INPUT_TEXT_NOT_VALID_JSON", "$.inputText", "blocking",
                    f"inputText must be a JSON string of a react schema candidate: {exc}",
                ))
                supplied_schema = None

            if supplied_schema is not None:
                if not isinstance(supplied_schema, dict) or supplied_schema.get("schema") != "topolactor.react_schema.v1":
                    errors.append(translator.err(
                        "REACT_SCHEMA_CANDIDATE_SCHEMA_MISMATCH", "$.inputText", "blocking",
                        "parsed inputText must be a topolactor.react_schema.v1 object",
                    ))
                else:
                    root_node = supplied_schema.get("root")
                    if not isinstance(root_node, dict):
                        errors.append(translator.err(
                            "REACT_SCHEMA_CANDIDATE_ROOT_MISSING", "$.inputText.root", "blocking",
                            "react schema candidate is missing a root node",
                        ))
                    else:
                        translator.assign_paths(root_node)
                        lanes_def = translator.dig(ssot_root, "wiring_lane_contract", "lanes") or {}
                        declared_surface = _lookup_declared_surface(ssot_root, target_surface)
                        tree_errors = []
                        translator.walk_and_validate(root_node, lanes_def, declared_surface, tree_errors)
                        errors.extend(tree_errors)
                        observations = _observe_react_tree(root_node, lanes_def, declared_surface)
                        for gap in translator.collect_known_gap_refs(root_node):
                            if gap not in known_gap_refs:
                                known_gap_refs.append(gap)

                        schema_to_seed_map = translator.dig(ssot_root, "exchange_mapping", "schema_to_seed_record_mapping") or {}
                        root_record = translator.convert_node_to_seed_record(root_node, schema_to_seed_map, target_surface, loss_entries)
                        for entry in loss_entries:
                            if entry.get("severity") == "blocking":
                                errors.append(translator.err(
                                    "REACT_NODE_KIND_UNMAPPED", entry["sourceReactPath"], "blocking", entry["reason"],
                                ))
                            gap_ref = entry.get("knownGapRef")
                            if gap_ref and gap_ref not in known_gap_refs:
                                known_gap_refs.append(gap_ref)
                        if root_record is not None:
                            record_types_def = translator.dig(ssot_root, "topology_ui_seed_contract", "record_types") or {}
                            translator.validate_seed_record_tree(root_record, record_types_def, errors)
                            _observe_seed_record_types(root_record, observations, known_gap_refs)

        _, evidence_errors = translator.passthrough_seed_evidence(envelope)
        errors.extend(evidence_errors)

    source_yaml_refs = list(envelope.get("sourceYamlRefs") or [])
    return errors, mode, target_surface, known_gap_refs, observations, normalized, source_yaml_refs, loss_entries


def _validate_react_schema_candidate_shape(doc, ssot_root):
    errors = []
    for field in REACT_SCHEMA_ROOT_REQUIRED_FIELDS:
        if not doc.get(field):
            errors.append(translator.err(
                "REACT_SCHEMA_ROOT_FIELD_MISSING", f"$.{field}", "blocking",
                f"react schema candidate missing required root field '{field}'",
            ))

    target_surface = doc.get("surface")
    root_node = doc.get("root")
    observations = _new_observations()
    known_gap_refs = []
    if isinstance(root_node, dict):
        translator.assign_paths(root_node)
        lanes_def = translator.dig(ssot_root, "wiring_lane_contract", "lanes") or {}
        declared_surface = _lookup_declared_surface(ssot_root, target_surface)
        translator.walk_and_validate(root_node, lanes_def, declared_surface, errors)
        observations = _observe_react_tree(root_node, lanes_def, declared_surface)
        known_gap_refs = translator.collect_known_gap_refs(root_node)

    source_yaml_refs = list(doc.get("sourceYamlRefs") or [])
    return errors, target_surface, known_gap_refs, observations, source_yaml_refs


def _validate_topology_ui_seed_candidate_shape(doc, ssot_root):
    errors = []
    for field in TOPOLOGY_SEED_ROOT_REQUIRED_FIELDS:
        if not doc.get(field) and doc.get(field) != []:
            errors.append(translator.err(
                "TOPOLOGY_SEED_ROOT_FIELD_MISSING", f"$.{field}", "blocking",
                f"topology ui seed candidate missing required root field '{field}'",
            ))

    record_types_def = translator.dig(ssot_root, "topology_ui_seed_contract", "record_types") or {}
    projections = doc.get("projections") or []
    observations = _new_observations()
    known_gap_refs = []
    for projection in projections:
        if isinstance(projection, dict):
            translator.validate_seed_record_tree(projection, record_types_def, errors)
            _observe_seed_record_types(projection, observations, known_gap_refs)

    source_yaml_refs = list(doc.get("sourceYamlRefs") or [])
    return errors, doc.get("surface"), known_gap_refs, observations, source_yaml_refs


def _validate_translator_output_shape(doc):
    errors = []
    for field in TRANSLATOR_OUTPUT_REQUIRED_FIELDS:
        if field not in doc:
            errors.append(translator.err(
                "TRANSLATOR_OUTPUT_FIELD_MISSING", f"$.{field}", "blocking",
                f"translator output missing required field '{field}'",
            ))

    existing_errors = doc.get("validationErrors") or []
    blocking_existing = [e for e in existing_errors if isinstance(e, dict) and e.get("severity") == "blocking"]
    if blocking_existing:
        errors.append(translator.err(
            "TRANSLATOR_OUTPUT_CARRIES_BLOCKING_ERRORS", "$.validationErrors", "blocking",
            f"supplied translator output already carries {len(blocking_existing)} blocking validationError(s)",
        ))

    known_gap_refs = list(doc.get("unresolvedGaps") or [])
    source_yaml_refs = list(translator.dig(doc, "exchangeReport", "sourceYamlRefs") or [])
    return errors, known_gap_refs, source_yaml_refs, existing_errors


# ---------------------------------------------------------------------------
# result assembly
# ---------------------------------------------------------------------------

def _mapping_observations(errors, observations):
    return {
        "mapping_rule_violations": [e for e in errors if e.get("ruleId") in MAPPING_RULE_IDS],
        "reactNodeKinds_seen": observations.get("reactNodeKinds_seen", []),
        "seedRecordTypes_seen": observations.get("seedRecordTypes_seen", []),
    }


def _build_result(*, source_ref, gate_status, detected_shape, errors, mode, target_surface,
                   known_gap_refs, observations, source_yaml_refs, normalized, expected_mode, loss_entries=None):
    blocking = [e for e in errors if e.get("severity") == "blocking"]
    warnings = [e for e in errors if e.get("severity") != "blocking"]
    return {
        "schemaId": GATE_SCHEMA_ID,
        "gateStatus": gate_status,
        "boundary": GATE_BOUNDARY,
        # Present on every gateStatus (pass / blocking / unsupported_input_shape) --
        # see AUTHORING_REFERENCES' own comment for why this is data, not authority.
        "authoringReferences": AUTHORING_REFERENCES,
        "inputDetection": {
            "sourceRef": source_ref,
            "detectedShape": detected_shape,
            "mode": mode,
            "expectedMode": expected_mode,
            "targetSurface": target_surface,
        },
        "entryValidation": {
            "validationErrors": errors,
            "blockingCount": len(blocking),
            "warningCount": len(warnings),
        },
        "schemaSeedMappingObservations": _mapping_observations(errors, observations),
        "blockingGaps": blocking,
        "unresolvedGaps": known_gap_refs,
        "sourceYamlRefs": source_yaml_refs,
        "knownGapRefs": known_gap_refs,
        "componentObservations": {
            "componentKinds_seen": observations.get("componentKinds_seen", []),
            "componentKinds_unknown": observations.get("componentKinds_unknown", []),
            "styleRefs_seen": observations.get("styleRefs_seen", []),
            "styleRefs_unknown": observations.get("styleRefs_unknown", []),
        },
        "wiringObservations": {
            "wiringLanes_seen": observations.get("wiringLanes_seen", []),
            "wiringLanes_unknown": observations.get("wiringLanes_unknown", []),
        },
        "payloadObservations": {
            "payloadFrom_sources_seen": observations.get("payloadFrom_sources_seen", []),
            "payloadFrom_sources_invalid": observations.get("payloadFrom_sources_invalid", []),
        },
        "authorityObservations": {
            "authorityMarkers_seen": observations.get("authorityMarkers_seen", []),
            "authorityMarkers_mismatched": observations.get("authorityMarkers_mismatched", []),
        },
        "translatorEntry": {
            "canProceedToConversion": gate_status == GATE_STATUS_PASS,
            "validationErrors": errors,
            "normalizedInputElements": normalized or [],
            "lossEntries": loss_entries or [],
        },
        "lossEntries": loss_entries or [],
        "proofSurface": {
            "executableProof": ".agent/scripts/check_react_schema_topology_seed_translator.py",
            "gateProof": ".agent/scripts/check_schema_seed_translator_entry_gate.py",
            "gateStatus": gate_status,
            "blockingRuleIds": [e.get("ruleId") for e in blocking],
        },
        # top-level convenience alias so callers/tests can read validationErrors
        # without descending into entryValidation/translatorEntry.
        "validationErrors": errors,
    }


def validate_translator_entry(input_json, ssot_root=None, expected_mode=None, source_ref="<in-memory>"):
    """Independent, importable schema<->seed translator entry gate.

    input_json: a dict (already-parsed JSON document) or a JSON string/bytes.
    ssot_root: an already-loaded react_schema_topology_seed_translator_ssot
        root dict (callers that already loaded the SSOT may pass it through
        to avoid re-parsing the YAML); loaded internally when omitted.
    expected_mode: optional translator subcommand context
        (generate_react_schema / generate_topology_ui_seed / round_trip_check)
        used to additionally check envelope.mode consistency.

    Returns a dict per topolactor.schema_seed_translator_entry_gate.v1 (see
    module docstring / .agent/tools/README.md for the field contract). Never
    raises for malformed input -- always returns a gateStatus.
    """
    if isinstance(input_json, (str, bytes)):
        try:
            doc = json.loads(input_json)
        except (json.JSONDecodeError, TypeError) as exc:
            return _build_result(
                source_ref=source_ref, gate_status=GATE_STATUS_UNSUPPORTED, detected_shape=None,
                errors=[translator.err("GATE_INPUT_INVALID_JSON", "$", "blocking", str(exc))],
                mode=None, target_surface=None, known_gap_refs=[], observations=_new_observations(),
                source_yaml_refs=[], normalized=[], expected_mode=expected_mode,
            )
    else:
        doc = input_json

    shape = _detect_shape(doc)
    if shape is None:
        return _build_result(
            source_ref=source_ref, gate_status=GATE_STATUS_UNSUPPORTED, detected_shape=None,
            errors=[translator.err(
                "GATE_UNSUPPORTED_ROOT_SHAPE", "$", "blocking",
                "input JSON does not match translator_input_envelope / react_schema_candidate / "
                "topology_ui_seed_candidate / translator_output shape",
            )],
            mode=None, target_surface=None, known_gap_refs=[], observations=_new_observations(),
            source_yaml_refs=[], normalized=[], expected_mode=expected_mode,
        )

    if ssot_root is None:
        ssot_root, ssot_errors = translator.load_ssot(translator.REPO_ROOT)
        if ssot_errors:
            return _build_result(
                source_ref=source_ref, gate_status=GATE_STATUS_BLOCKING, detected_shape=shape,
                errors=ssot_errors, mode=None, target_surface=None, known_gap_refs=[],
                observations=_new_observations(), source_yaml_refs=[], normalized=[], expected_mode=expected_mode,
            )

    mode = None
    normalized = []

    loss_entries = []
    if shape == "translator_input_envelope":
        errors, mode, target_surface, known_gap_refs, observations, normalized, source_yaml_refs, loss_entries = _validate_envelope_shape(doc, ssot_root, expected_mode)
    elif shape == "react_schema_candidate":
        errors, target_surface, known_gap_refs, observations, source_yaml_refs = _validate_react_schema_candidate_shape(doc, ssot_root)
    elif shape == "topology_ui_seed_candidate":
        errors, target_surface, known_gap_refs, observations, source_yaml_refs = _validate_topology_ui_seed_candidate_shape(doc, ssot_root)
    else:  # translator_output
        errors, known_gap_refs, source_yaml_refs, _existing = _validate_translator_output_shape(doc)
        target_surface = None
        observations = _new_observations()

    gate_status = GATE_STATUS_BLOCKING if any(e.get("severity") == "blocking" for e in errors) else GATE_STATUS_PASS

    return _build_result(
        source_ref=source_ref, gate_status=gate_status, detected_shape=shape, errors=errors,
        mode=mode, target_surface=target_surface, known_gap_refs=known_gap_refs,
        observations=observations, source_yaml_refs=source_yaml_refs, normalized=normalized,
        expected_mode=expected_mode, loss_entries=loss_entries,
    )


def validate_translator_entry_from_path(path, ssot_root=None, expected_mode=None):
    """File-based convenience wrapper over validate_translator_entry.

    Never raises: unreadable files become a GATE_INPUT_FILE_UNREADABLE
    unsupported_input_shape result, consistent with the gate's fail-close
    contract for the CLI/proof callers that only have a path.
    """
    text_path = Path(path)
    try:
        raw_text = text_path.read_text(encoding="utf-8")
    except OSError as exc:
        return _build_result(
            source_ref=str(path), gate_status=GATE_STATUS_UNSUPPORTED, detected_shape=None,
            errors=[translator.err("GATE_INPUT_FILE_UNREADABLE", "$.input", "blocking", str(exc))],
            mode=None, target_surface=None, known_gap_refs=[], observations=_new_observations(),
            source_yaml_refs=[], normalized=[], expected_mode=expected_mode,
        )
    return validate_translator_entry(raw_text, ssot_root=ssot_root, expected_mode=expected_mode, source_ref=str(path))
