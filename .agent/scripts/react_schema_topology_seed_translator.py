#!/usr/bin/env python3
"""react_schema_topology_seed_translator.py -- generate-react-schema /
generate-topology-seed implementation body.

Implements two modes of docs/design/react-schema-topology-seed-translator-ssot.yaml:

    generate_react_schema:
      inputText -> input_text_markup_grammar_contract parse
                -> text_decomposition_contract normalized elements
                -> react_schema_contract candidate
                -> wiring_lane_contract / ui_catalog_boundary_contract validation
                -> output_format_contract shaped document

    generate_topology_ui_seed:
      inputText (JSON string of a topolactor.react_schema.v1 candidate)
                -> wiring_lane_contract / ui_catalog_boundary_contract validation
                   (re-validated; a supplied schema is never trusted blindly)
                -> exchange_mapping.schema_to_seed_record_mapping conversion
                -> topology_ui_seed_contract candidate (draft/intake only,
                   never active topology; see topology_ui_seed_contract.active_topology_rule)
                -> output_format_contract shaped document

`round-trip-check` is explicitly out of scope for this Bundle and fails
closed with a `not_implemented_out_of_scope` document rather than raising or
silently succeeding.

Translator input authority: this script reads only the SSOT YAML above (via
the repo's stdlib-only minimal_yaml loader) and the caller-supplied input
envelope JSON, per translator_input_authority in the SSOT. It never opens
`db/*.sql`. If the caller's input envelope carries a pre-resolved
`seedEvidence` object, this script passes it through to the output
unchanged -- it does not resolve, verify, or derive seed evidence itself.
Resolving `seedEvidence` from `db/seed_empty.sql` is an evidence/proof
verification concern that lives in
.agent/scripts/check_react_schema_topology_seed_translator.py, not here.

No network, no database connection, no backend/frontend/nginx process is
started or required.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent.parent
sys.path.insert(0, str(SCRIPT_DIR / "lib"))
import minimal_yaml as yaml  # noqa: E402

SSOT_REL_PATH = "docs/design/react-schema-topology-seed-translator-ssot.yaml"

# storage_adoption_contract.constraint.budget_source: db/manifest_tables.sql
# defines idx_manifest_topology as `GIN (topology)` over `manifest.topology
# jsonb[]`. A GIN index over an array type indexes each array element as one
# indexable item, subject to Postgres's ~2712-byte index item size ceiling
# (btree/GIN internal page-size-derived limit). A single oversized jsonb[]
# element fails INSERT with "index row size N exceeds maximum 2712 for index
# idx_manifest_topology" -- this is a real DB constraint discovered the hard
# way in PR #573, not a style preference. See storage_adoption_contract in
# the SSOT for the documented contract this constant implements.
MANIFEST_TOPOLOGY_ARRAY_ELEMENT_BYTE_BUDGET = 2712

# exchange_mapping.schema_to_seed_record_mapping's nested list fields, keyed
# by the seed recordType that owns them, each paired with the flattened
# "shell" key name that replaces the nested list with a list of child keys
# (see flatten_topology_ui_seed_tree). Mirrors the nesting convert_node_to_seed_record
# builds for each react_kind.
SEED_RECORD_NESTED_LIST_KEYS = {
    "topology_ui_projection": [("categories", "categoryKeys")],
    "topology_ui_category": [("sections", "sectionKeys")],
    "topology_ui_section": [("children", "childKeys")],
    "topology_ui_form": [("fields", "fieldKeys"), ("actions", "actionKeys")],
    "topology_ui_table": [("columns", "columnKeys")],
    "topology_ui_workflow": [("steps", "stepKeys")],
    "topology_ui_modal": [("children", "childKeys")],
}

REQUIRED_SSOT_SECTIONS = [
    "input_format_contract",
    "input_text_markup_grammar_contract",
    "text_decomposition_contract",
    "react_schema_contract",
    "output_format_contract",
    "exchange_mapping",
    "wiring_lane_contract",
    "ui_catalog_boundary_contract",
    "declared_seed_surface_catalog",
    "validation_rules",
]

VALID_MODES = ["generate_react_schema", "generate_topology_ui_seed", "round_trip_check"]

DEFAULT_PROTECTED_VOCABULARY = [
    "plaintext_secret",
    "connection_string",
    "endpoint_real_value",
    "credential_plaintext",
    "private_key_material",
    "runtime_only_decrypted_payload",
    "raw_SQL",
    "unapproved_executable_function_or_schema_authority",
]

CONTAINER_UNITS = {"projection", "category", "section", "form", "workflow", "modal"}
LEAF_UNITS = {"field", "table", "step", "action", "validation", "prop_binding", "payload_from", "style_ref"}
ALL_TAGGABLE_UNITS = CONTAINER_UNITS | LEAF_UNITS

# input_text_markup_grammar_contract.common_attributes.label.required_on:
# react_schema_contract.node_common_required_fields requires label on every
# node kind (not just the container/field-like ones), so this covers all
# taggable units -- an Action/Step/Validation/PropBinding/PayloadFrom/StyleRef
# without a label is a MISSING_LABEL error too, never a null seed record label.
LABEL_REQUIRED_UNITS = set(ALL_TAGGABLE_UNITS)

# react_schema_contract prohibited_features.mutation_action_not_owned_by_a_form:
# an Action node's direct parent must always be a Form, a Workflow, or a Modal -- verified
# against db/physical_search_crud_aggregate_preset_seed.sql's crud_submit_button/
# crud_cancel_button, whose parentNodeId is "crud_create_modal" (a Modal, no Form wrapper).
VALID_ACTION_OWNER_NODE_KINDS = {"Form", "Workflow", "Modal"}

# A Section may ALSO directly own an Action, but only when that Action's own wiringLane is
# disclosure_state_wiring (round 24) -- a pure UI-local disclosure trigger with no backend dispatch
# authority at all, verified against the SAME fixture: crud_add_button/crud_search_button (plain
# trigger buttons, not mutations) have parentNodeId "crud_shell", a bare Section, no Form at all.
# Deliberately NOT extended to every lane: check_react_schema_topology_seed_translator.py's own
# check 40 proves a REAL dispatch Action (external_instance_wiring) injected directly under a
# Section must still be rejected -- unconditionally allowing Section as an Action owner would
# silently defeat that protection. Only the narrowest lane this round's own evidence supports is
# added, not a blanket exception.
SECTION_OWNABLE_ACTION_LANES = {"disclosure_state_wiring"}

UNIT_TO_NODE_KIND = {
    "projection": "Projection",
    "category": "Category",
    "section": "Section",
    "form": "Form",
    "field": "Field",
    "table": "Table",
    "workflow": "Workflow",
    "modal": "Modal",
    "step": "Step",
    "action": "Action",
    "validation": "Validation",
    "prop_binding": "PropBinding",
    "payload_from": "PayloadFrom",
    "style_ref": "StyleRef",
}

# wiring_lane_contract.lanes.disclosure_state_wiring actionType vocabulary (round 24) -- the
# SAME actionTypes backend/repository/NpgsqlUiTopologyRepository.cs ValidateRuntimeInteractions
# accepts for an active-topology runtimeInteractions[] entry whose target is a disclosure
# container or disclosure/tabs|accordion (setActiveKey) node. This translator only emits Modal
# containers today, so only the *Modal actionTypes are reachable via a real fixture; the others
# are still recognized vocabulary (not UNKNOWN) so a future round adding Drawer/Dialog/Tabs
# container kinds does not also need a new actionType allowlist.
DISCLOSURE_ACTION_TYPES = {
    "openModal", "closeModal", "toggleModal",
    "openDrawer", "closeDrawer", "toggleDrawer",
    "openDialog", "closeDialog", "toggleDialog",
    "setActiveKey", "setState",
}

# Only the *Modal family has a matching container kind this translator can emit and therefore
# cross-validate today (see validate_disclosure_targets) -- Drawer/Dialog/Tabs/Accordion target
# kind checking is deferred to whichever future round adds those container kinds.
DISCLOSURE_TARGET_KIND_BY_ACTION_TYPE = {
    "openModal": "Modal",
    "closeModal": "Modal",
    "toggleModal": "Modal",
}

TAG_LINE_RE = re.compile(r'^\[(?P<slash>/)?(?P<kind>[A-Za-z_][A-Za-z0-9_]*)(?P<attrs>(?:\s+.+)?)\]$')
ATTR_RE = re.compile(r'([A-Za-z_][A-Za-z0-9_]*)=("(?:[^"\\]|\\.)*"|\S+)')
HTML_TAG_RE = re.compile(r'<\s*[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?\s*>')

# nodeId grammar [A-Za-z0-9_-]+ (round 21 audit fix): MUST match
# frontend/runtime/payloadFromResolver.ts's own NODE_VALUE_RE exactly --
# docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
# .recognized_source_patterns.node_value_path.nodeId_grammar declares this the ONE
# canonical_layout_node_identity vocabulary (no dots -- a dot is exclusively the
# path-suffix separator, never part of a nodeId itself). This Python regex previously
# allowed a dot INSIDE the nodeId segment (a pre-existing, undetected divergence from the
# frontend's hyphen-inclusive/dot-exclusive class, predating round 20's suffix addition) --
# caught by check_react_schema_topology_seed_translator.py's 42g/42h paired grammar-parity
# assertions (round 21), which proved "node:crud-search-input.value.query" (a real,
# hyphenated nodeId shape used elsewhere in this codebase, e.g.
# ui-builder-preset-ecosystem-ssot.yaml's own hub_search_preset layout_tree) was accepted by
# the frontend but rejected by this Python regex.
#
# Trailing (?:\.[A-Za-z0-9_]+)* mirrors frontend/runtime/payloadFromResolver.ts's
# NODE_VALUE_RE dotted-path-drilling extension (round 20 -- owning SSOT:
# docs/design/ui-builder-preset-ecosystem-ssot.yaml
# payloadFrom_resolver_contract.recognized_source_patterns.node_value_path): a
# tracked node value that is an object (e.g. a table's selected row) can have a
# single field extracted, e.g. node:enum_table.value.groupId.
NODE_VALUE_RE = re.compile(r'^node:[A-Za-z0-9_-]+\.value(?:\.[A-Za-z0-9_]+)*$')
EVENT_PATH_RE = re.compile(r'^event(\.[A-Za-z0-9_]+)+$')
LITERAL_RE = re.compile(r'^literal:.*$')

TOKEN_LIKE_RE = re.compile(r'^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$')


# ---------------------------------------------------------------------------
# small helpers
# ---------------------------------------------------------------------------

def dig(obj, *keys):
    cur = obj
    for k in keys:
        if not isinstance(cur, dict) or k not in cur:
            return None
        cur = cur[k]
    return cur


def err(rule_id, path, severity, message):
    return {"ruleId": rule_id, "path": path, "severity": severity, "message": message}


def is_blocking(entry):
    return entry.get("severity") == "blocking"


def split_list(raw):
    if not raw:
        return []
    return [v.strip() for v in raw.split(",") if v.strip()]


def unquote(value):
    if value is None:
        return None
    if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
        return value[1:-1].replace('\\"', '"')
    return value


# ---------------------------------------------------------------------------
# SSOT loading
# ---------------------------------------------------------------------------

def load_ssot(repo_root: Path):
    path = repo_root / SSOT_REL_PATH
    if not path.is_file():
        return None, [err("SSOT_FILE_NOT_FOUND", "$.ssot", "blocking", f"SSOT file not found: {SSOT_REL_PATH}")]
    try:
        data = yaml.load_file(str(path))
    except Exception as exc:  # noqa: BLE001 - fail-closed with explicit error, not a crash
        return None, [err("SSOT_PARSE_ERROR", "$.ssot", "blocking", f"failed to parse SSOT YAML: {exc}")]
    root = data.get("react_schema_topology_seed_translator_ssot") if isinstance(data, dict) else None
    if root is None:
        return None, [err("SSOT_ROOT_KEY_MISSING", "$.ssot", "blocking", "react_schema_topology_seed_translator_ssot root key missing")]
    errors = []
    for section in REQUIRED_SSOT_SECTIONS:
        if section not in root:
            errors.append(err("SSOT_SECTION_MISSING", f"$.ssot.{section}", "blocking", f"required SSOT section missing: {section}"))
    return root, errors


def protected_vocabulary(ssot_root):
    values = dig(ssot_root, "translator_input_authority", "protected_boundary_vocabulary", "values")
    return values if isinstance(values, list) and values else list(DEFAULT_PROTECTED_VOCABULARY)


# ---------------------------------------------------------------------------
# translator entry gate connection
#
# schema_seed_translator_entry_gate.py imports THIS module at its own top
# level (it reuses validate_input_envelope/parse_markup/walk_and_validate/etc.
# instead of duplicating them), so this module must not import that one at
# module-load time -- that would be circular. Loading it lazily, inside the
# function that needs it, is safe: by the time cmd_generate_react_schema /
# cmd_generate_topology_seed actually run, this module is already fully
# initialized in sys.modules, so the gate core's own top-level import of this
# module resolves instantly from cache instead of re-executing it.
# ---------------------------------------------------------------------------

def _gate_core():
    gate_dir = SCRIPT_DIR / "agent_tools"
    if str(gate_dir) not in sys.path:
        sys.path.insert(0, str(gate_dir))
    import schema_seed_translator_entry_gate as gate  # noqa: PLC0415
    return gate


# ---------------------------------------------------------------------------
# seedEvidence passthrough
#
# The translator NEVER opens db/*.sql. Resolving seedEvidence from
# db/seed_empty.sql is an evidence/proof verification concern owned by
# .agent/scripts/check_react_schema_topology_seed_translator.py. If the
# caller's input envelope already carries a `seedEvidence` object (produced
# by that verification step, or by a human/agent reading db/*.sql as
# reference material while authoring the fixture), this translator carries
# it through to the output unchanged -- it does not look anything up itself.
# ---------------------------------------------------------------------------

SEED_EVIDENCE_REQUIRED_FIELDS = ["screenUuidNamespace", "screenUuid", "manifestKey"]


def passthrough_seed_evidence(envelope):
    seed_evidence = envelope.get("seedEvidence")
    if seed_evidence is None:
        return None, []
    if not isinstance(seed_evidence, dict):
        return None, [err("SEED_EVIDENCE_SHAPE_INVALID", "$.seedEvidence", "blocking", "seedEvidence must be an object when supplied")]
    missing = [f for f in SEED_EVIDENCE_REQUIRED_FIELDS if f not in seed_evidence]
    if missing:
        return seed_evidence, [
            err(
                "SEED_EVIDENCE_SHAPE_INVALID",
                "$.seedEvidence",
                "blocking",
                f"supplied seedEvidence missing required field(s): {missing}",
            )
        ]
    return seed_evidence, []


# ---------------------------------------------------------------------------
# input envelope validation
# ---------------------------------------------------------------------------

def validate_input_envelope(envelope, ssot_root, vocabulary):
    errors = []
    if envelope.get("schemaId") != "topolactor.translator_input.v1":
        errors.append(err("INPUT_SCHEMA_ID_MISMATCH", "$.schemaId", "blocking", "schemaId must equal topolactor.translator_input.v1"))

    mode = envelope.get("mode")
    if mode not in VALID_MODES:
        errors.append(err("INPUT_MODE_INVALID", "$.mode", "blocking", f"mode must be one of {VALID_MODES}"))

    input_text = envelope.get("inputText") or ""
    if not input_text.strip():
        errors.append(err("INPUT_TEXT_EMPTY", "$.inputText", "blocking", "inputText must not be empty"))

    target_surface = envelope.get("targetSurface")
    declared_surfaces = dig(ssot_root, "declared_seed_surface_catalog", "known_declared_surfaces") or []
    declared_keys = {s.get("seed_surface_key") for s in declared_surfaces}
    known_gap_refs = envelope.get("knownGapRefs") or []
    if target_surface not in declared_keys and not known_gap_refs:
        errors.append(
            err(
                "TARGET_SURFACE_UNRESOLVED",
                "$.targetSurface",
                "blocking",
                "targetSurface must resolve to a declared_seed_surface_catalog entry, or carry a knownGapRef",
            )
        )

    source_refs = envelope.get("sourceYamlRefs") or []
    if not source_refs:
        errors.append(err("SOURCE_YAML_REFS_EMPTY", "$.sourceYamlRefs", "blocking", "sourceYamlRefs must be non-empty"))

    for term in vocabulary:
        if term and term in input_text:
            errors.append(
                err(
                    "PROTECTED_BOUNDARY_VOCABULARY_PRESENT",
                    "$.inputText",
                    "blocking",
                    f"protected boundary vocabulary term found in inputText: {term}",
                )
            )

    return errors, mode, input_text, target_surface


# ---------------------------------------------------------------------------
# input_text_markup_grammar_contract parser
# ---------------------------------------------------------------------------

def parse_attrs(raw_attrs: str):
    attrs = {}
    for m in ATTR_RE.finditer(raw_attrs or ""):
        key, raw_value = m.group(1), m.group(2)
        attrs[key] = unquote(raw_value)
    return attrs


def parse_payload_from(raw):
    """`field:source,field2:source2` -> {field: source, field2: source2}."""
    result = {}
    if not raw:
        return result
    for pair in raw.split(","):
        pair = pair.strip()
        if not pair:
            continue
        field, sep, source = pair.partition(":")
        if not sep:
            continue
        result[field.strip()] = source.strip()
    return result


def build_node(kind, attrs, source_refs, known_gaps):
    node_kind = UNIT_TO_NODE_KIND[kind]
    key = attrs.get("key")
    node = {
        "kind": node_kind,
        "key": key,
        "sourceYamlRefs": source_refs,
    }
    if kind in LABEL_REQUIRED_UNITS or attrs.get("label"):
        # no fallback-to-key: a missing label on a LABEL_REQUIRED_UNITS kind is
        # a blocking MISSING_LABEL validation error raised by the caller
        # (parse_markup), not a silently-accepted default value here.
        node["label"] = attrs.get("label")
    if known_gaps:
        node["knownGapRefs"] = list(known_gaps)
    authority_marker = attrs.get("authorityMarker")
    if authority_marker:
        node["authorityMarker"] = authority_marker

    if kind in CONTAINER_UNITS:
        node["children"] = []
    if kind == "section":
        node["sectionKind"] = attrs.get("sectionKind", "")
    if kind == "form":
        node["target"] = attrs.get("target", "")
        node["mode"] = attrs.get("mode", "")
        node["fields"] = []
        node["actions"] = []
    if kind == "field":
        node["control"] = attrs.get("control", "")
        node["required"] = str(attrs.get("required", "false")).lower() == "true"
    if kind == "table":
        node["source"] = attrs.get("source", "")
        node["display"] = attrs.get("display", "")
    if kind == "workflow":
        node["steps"] = []
    if kind == "modal":
        # react_schema_contract.allowed_node_kinds.Modal: only disclosure/modal is emitted by
        # this translator today (round 24) -- title/body are optional display props carried
        # through to the tensor node's propsJson by the seed transcription step, never
        # validated here.
        node["componentKind"] = "disclosure/modal"
        node["title"] = attrs.get("title", "")
        node["body"] = attrs.get("body", "")
    if kind in ("step", "action"):
        wiring_lane = attrs.get("wiringLane")
        event_binding = {
            "trigger": attrs.get("trigger", "click" if kind == "action" else "step"),
            "wiringLane": wiring_lane,
            "targetRef": attrs.get("actionRef", ""),
            "authority": authority_marker,
        }
        payload_from_raw = attrs.get("payloadFrom")
        if payload_from_raw:
            event_binding["payloadFrom"] = parse_payload_from(payload_from_raw)
        output_prop = attrs.get("outputProp")
        if output_prop:
            event_binding["outputProp"] = output_prop
        # wiring_lane_contract.lanes.disclosure_state_wiring (round 24): the structured fields
        # build_runtime_interaction_candidate actually reads for a disclosure/setActiveKey/setState
        # runtimeInteractions[] entry -- targetRef above stays in the ui-local:<nodeId>.<stateKey>
        # shape purely so validate_wiring_node's existing targetRef_shape check has something to
        # validate; it is never read for this lane's actual candidate.
        if wiring_lane == "disclosure_state_wiring":
            d_target = attrs.get("disclosureTargetNodeId", "")
            d_path = attrs.get("disclosureStatePath") or "open"
            event_binding["disclosureActionType"] = attrs.get("disclosureActionType")
            event_binding["disclosureTargetNodeId"] = d_target
            event_binding["disclosureStatePath"] = d_path
            if d_target:
                event_binding["targetRef"] = f"ui-local:{d_target}.{d_path}"
        node["eventBinding"] = event_binding
        node["actionRef"] = attrs.get("actionRef", "")
        # secondaryDisclosureAction (round 24): independent of the primary wiringLane above --
        # lets one Action carry BOTH a real dispatch (e.g. admin_runtime_dispatch_override_wiring)
        # AND an additional disclosure state change (e.g. closeModal) on the same trigger, the
        # same way a real admin/uibuilder author can attach more than one runtimeInteraction to
        # one node/trigger. See wiring_lane_contract.lanes.disclosure_state_wiring.secondary_combination.
        secondary_action_type = attrs.get("secondaryDisclosureActionType")
        if secondary_action_type:
            node["secondaryDisclosureAction"] = {
                "trigger": event_binding["trigger"],
                "actionType": secondary_action_type,
                "targetNodeId": attrs.get("secondaryDisclosureTargetNodeId", ""),
                "statePath": attrs.get("secondaryDisclosureStatePath") or "open",
            }
        if attrs.get("idempotencyPolicy"):
            node["idempotencyPolicy"] = attrs.get("idempotencyPolicy")
        if attrs.get("lifecycleDispatchConfirmed") is not None:
            node["lifecycleDispatchConfirmed"] = str(attrs.get("lifecycleDispatchConfirmed")).lower() == "true"
        if attrs.get("debounceMs") is not None:
            try:
                node["debounceMs"] = int(attrs.get("debounceMs"))
            except (TypeError, ValueError):
                node["debounceMs"] = attrs.get("debounceMs")
    if kind == "validation":
        node["rule"] = attrs.get("rule", "")
        node["severity"] = attrs.get("severity", "warning")
        node["appliesTo"] = attrs.get("appliesTo", "")
    if kind == "prop_binding":
        node["targetProp"] = attrs.get("targetProp", "")
        node["source"] = attrs.get("source", "")
    if kind == "payload_from":
        node["targetField"] = attrs.get("targetField", "")
        node["source"] = attrs.get("source", "")
    if kind == "style_ref":
        node["target"] = attrs.get("target", "")
        node["tokenRef"] = attrs.get("tokenRef", "")
    return node


def parse_markup(input_text):
    """Bracket-tag line-based parser per input_text_markup_grammar_contract.

    Returns (normalized_elements, root_node_or_None, parse_errors).
    """
    normalized = []
    errors = []
    stack = []  # open container nodes, deepest last
    unit_stack = []  # parallel unitKind stack for close-tag matching
    key_sets = [set()]  # key-uniqueness-within-parent, parallel to stack (+root sentinel)
    root_node = None

    def attach(node):
        if stack:
            parent = stack[-1]
            parent.setdefault("children", []).append(node)
            node["_path"] = f'{parent["_path"]}.children[{len(parent["children"]) - 1}]'
            return True
        if root_node is not None:
            root_node.setdefault("children", []).append(node)
            node["_path"] = f'{root_node["_path"]}.children[{len(root_node["children"]) - 1}]'
            return True
        return False

    for lineno, raw_line in enumerate(input_text.splitlines(), start=1):
        line = raw_line.strip()
        if not line:
            continue

        if not line.startswith("[") and HTML_TAG_RE.search(line):
            gap_ref = f"component_catalog_gap:raw_html_rejected:line_{lineno}"
            errors.append(err("RAW_HTML_TAG_EMISSION_ATTEMPT", f"$.inputText:line{lineno}", "blocking", f"raw HTML-like tag rejected: {line!r}"))
            normalized.append({
                "unitKind": "unresolved", "key": None, "rawFragment": line, "attributes": {},
                "sourceYamlRefs": [], "knownGapRefs": [gap_ref], "line": lineno, "path": None,
            })
            continue

        m = TAG_LINE_RE.match(line)
        if not m:
            gap_ref = f"ssot_ambiguity_gap:unclassified_input_fragment:line_{lineno}"
            node = {"kind": "Unresolved", "key": f"unresolved_line_{lineno}", "rawFragment": line,
                    "knownGapRefs": [gap_ref], "sourceYamlRefs": []}
            attached = attach(node)
            normalized.append({
                "unitKind": "unresolved", "key": None, "rawFragment": line, "attributes": {},
                "sourceYamlRefs": [], "knownGapRefs": [gap_ref], "line": lineno,
                "path": node.get("_path") if attached else None,
            })
            continue

        is_close = bool(m.group("slash"))
        kind = m.group("kind")
        attrs = parse_attrs(m.group("attrs"))

        if is_close:
            if unit_stack and unit_stack[-1] == kind:
                unit_stack.pop()
                stack.pop()
                key_sets.pop()
            else:
                errors.append(err("MISMATCHED_CLOSE_TAG", f"$.inputText:line{lineno}", "blocking", f"[/{kind}] does not match currently open unit"))
            continue

        if kind not in ALL_TAGGABLE_UNITS:
            gap_ref = f"ssot_ambiguity_gap:unknown_unit_kind:{kind}:line_{lineno}"
            errors.append(err("UNKNOWN_UNIT_KIND", f"$.inputText:line{lineno}", "blocking", f"unknown unitKind '{kind}'"))
            node = {"kind": "Unresolved", "key": attrs.get("key") or f"unresolved_line_{lineno}",
                    "rawFragment": line, "knownGapRefs": [gap_ref], "sourceYamlRefs": split_list(attrs.get("sourceYamlRefs"))}
            attached = attach(node)
            normalized.append({
                "unitKind": "unresolved", "key": attrs.get("key"), "rawFragment": line, "attributes": attrs,
                "sourceYamlRefs": node["sourceYamlRefs"], "knownGapRefs": [gap_ref], "line": lineno,
                "path": node.get("_path") if attached else None,
            })
            continue

        key = attrs.get("key")
        source_refs = split_list(attrs.get("sourceYamlRefs"))
        known_gaps = split_list(attrs.get("knownGapRefs"))
        wiring_lane = attrs.get("wiringLane")
        authority_marker = attrs.get("authorityMarker")

        if not key:
            errors.append(err("TAG_MISSING_KEY", f"$.inputText:line{lineno}", "blocking", f"[{kind}] tag missing required key attribute"))
        elif key in key_sets[-1]:
            errors.append(err("DUPLICATE_KEY_IN_PARENT", f"$.inputText:line{lineno}", "blocking", f"[{kind} key={key}] duplicates a sibling key"))
        else:
            key_sets[-1].add(key)

        if not source_refs:
            errors.append(err("TAG_MISSING_SOURCE_YAML_REFS", f"$.inputText:line{lineno}", "blocking", f"[{kind} key={key}] missing required sourceYamlRefs attribute"))

        if kind in ("action", "step") and not wiring_lane:
            errors.append(err("ACTION_OR_STEP_MISSING_WIRING_LANE", f"$.inputText:line{lineno}", "blocking", f"[{kind} key={key}] missing required wiringLane attribute"))

        if kind in ("form", "action", "step", "workflow") and not authority_marker:
            errors.append(err("MISSING_AUTHORITY_MARKER", f"$.inputText:line{lineno}", "blocking", f"[{kind} key={key}] missing required authorityMarker attribute"))

        if kind in LABEL_REQUIRED_UNITS and not attrs.get("label"):
            errors.append(err("MISSING_LABEL", f"$.inputText:line{lineno}", "blocking", f"[{kind} key={key}] missing required label attribute"))

        node = build_node(kind, attrs, source_refs, known_gaps)

        if kind == "projection" and root_node is None and not stack:
            node["_path"] = "$.root"
            root_node = node
            stack.append(node)
            unit_stack.append(kind)
            key_sets.append(set())
        else:
            attached = attach(node)
            if not attached:
                errors.append(err("CONTENT_BEFORE_ROOT_PROJECTION", f"$.inputText:line{lineno}", "warning", f"[{kind} key={key}] appears before any [projection] root tag"))
            if kind in CONTAINER_UNITS:
                stack.append(node)
                unit_stack.append(kind)
                key_sets.append(set())

        normalized.append({
            "unitKind": kind, "key": key, "rawFragment": line, "attributes": attrs,
            "sourceYamlRefs": source_refs, "knownGapRefs": known_gaps, "line": lineno,
            "path": node.get("_path"),
        })

    if stack:
        for n in stack:
            errors.append(err("UNCLOSED_CONTAINER_TAG", "$.inputText", "blocking", f"[{UNIT_TO_NODE_KIND_REVERSE.get(n['kind'], n['kind'])}] key={n.get('key')} was never closed"))
    if root_node is None:
        errors.append(err("NO_ROOT_PROJECTION_FOUND", "$.inputText", "blocking", "no [projection ...] root tag found in inputText"))

    return normalized, root_node, errors


UNIT_TO_NODE_KIND_REVERSE = {v: k for k, v in UNIT_TO_NODE_KIND.items()}


def strip_internal(node):
    if not isinstance(node, dict):
        return node
    cleaned = {k: v for k, v in node.items() if not k.startswith("_")}
    if "children" in cleaned:
        cleaned["children"] = [strip_internal(c) for c in cleaned["children"]]
    return cleaned


def finalize_node(node):
    children = node.get("children")
    if children is not None:
        for c in children:
            finalize_node(c)
        if node["kind"] == "Form":
            node["fields"] = [c["key"] for c in children if c.get("kind") == "Field"]
            node["actions"] = [c["key"] for c in children if c.get("kind") == "Action"]
        if node["kind"] == "Workflow":
            node["steps"] = [c["key"] for c in children if c.get("kind") == "Step"]
    return node


def collect_known_gap_refs(node, acc=None):
    if acc is None:
        acc = []
    for g in node.get("knownGapRefs") or []:
        if g not in acc:
            acc.append(g)
    for c in node.get("children") or []:
        collect_known_gap_refs(c, acc)
    return acc


def count_records(node):
    total = 1
    for c in node.get("children") or []:
        total += count_records(c)
    return total


# ---------------------------------------------------------------------------
# wiring_lane_contract / ui_catalog_boundary_contract validation
# ---------------------------------------------------------------------------

def shape_to_regex(shape):
    parts = re.split(r'(<[^>]+>)', shape)
    pattern = ""
    for part in parts:
        if part.startswith("<") and part.endswith(">"):
            inner = part[1:-1]
            if "|" in inner:
                # enum placeholder, e.g. <db_instance_port|runtime_instance_port>:
                # restrict to the listed alternatives instead of a generic [^:]+.
                alternatives = [re.escape(a.strip()) for a in inner.split("|") if a.strip()]
                pattern += "(?:" + "|".join(alternatives) + ")"
            else:
                pattern += r'[^:]+'
        else:
            pattern += re.escape(part)
    return re.compile("^" + pattern + "$")


def lane_target_ref_patterns(lane_def):
    shape = lane_def.get("targetRef_shape")
    if isinstance(shape, dict) and "one_of" in shape:
        return [shape_to_regex(s) for s in shape["one_of"]]
    if isinstance(shape, str):
        return [shape_to_regex(shape)]
    return []


def classify_source_pattern(value):
    if not isinstance(value, str):
        return None
    if NODE_VALUE_RE.match(value):
        return "node_value"
    if EVENT_PATH_RE.match(value):
        return "event_path"
    if LITERAL_RE.match(value):
        return "literal"
    return None


def validate_wiring_node(node, lanes_def, errors, path):
    if node.get("kind") not in ("Action", "Step"):
        return
    eb = node.get("eventBinding") or {}
    lane_key = eb.get("wiringLane")
    lane_def = lanes_def.get(lane_key) if lane_key else None
    if not lane_def:
        errors.append(err("UNRESOLVED_WIRING_LANE", path, "blocking", f"wiringLane '{lane_key}' is not a recognized wiring_lane_contract lane"))
        node.setdefault("knownGapRefs", []).append(f"runtime_dispatch_or_projection_gap:unresolved_wiring_lane:{node.get('key')}")
        return

    target_ref = eb.get("targetRef") or ""
    patterns = lane_target_ref_patterns(lane_def)
    if target_ref and patterns and not any(p.match(target_ref) for p in patterns):
        errors.append(err("TARGET_REF_SHAPE_MISMATCH", path, "blocking", f"targetRef '{target_ref}' does not match lane '{lane_key}' targetRef_shape"))

    authority = eb.get("authority")
    allowed_auth = lane_def.get("allowed_authority_mapping_values") or []
    if authority not in allowed_auth:
        errors.append(err("AUTHORITY_NOT_ALLOWED_FOR_LANE", path, "blocking", f"authority '{authority}' is not allowed for lane '{lane_key}'"))
    if authority != node.get("authorityMarker"):
        errors.append(err("EVENT_BINDING_AUTHORITY_MISMATCH", path, "blocking", f"eventBinding.authority '{authority}' must equal owning node authorityMarker '{node.get('authorityMarker')}'"))

    payload_from = eb.get("payloadFrom") or {}
    allowed_sources = set(lane_def.get("allowed_payload_from_sources") or [])
    for field, source in payload_from.items():
        kind = classify_source_pattern(source)
        if kind is None:
            errors.append(err("PAYLOAD_FROM_UNRESOLVED_REF", path, "blocking", f"payloadFrom '{field}' source '{source}' does not match a recognized pattern"))
        elif kind not in allowed_sources:
            errors.append(err("PAYLOAD_FROM_SOURCE_NOT_ALLOWED_FOR_LANE", path, "blocking", f"payloadFrom '{field}' source kind '{kind}' is not allowed for lane '{lane_key}'"))



def runtime_action_type_for_event_binding(event_binding):
    """Map a seed eventBinding lane into the layout_patch_json runtimeInteractions[]
    actionType vocabulary without creating a runtimeInteractionId. The backend
    persistence boundary remains the only assignment authority for that id."""
    if not isinstance(event_binding, dict):
        return None
    lane = event_binding.get("wiringLane")
    target_ref = event_binding.get("targetRef") or ""
    if lane == "external_instance_wiring" or target_ref.startswith("instance:"):
        return "dispatchInstanceOperation"
    if lane == "external_integration_wiring" or target_ref.startswith("external-port:"):
        return "dispatchExternalPort"
    if lane == "internal_instance_wiring":
        return "localStateMutation"
    if lane == "route_navigation_wiring":
        return "routeNavigation"
    if lane == "contents_api_wiring":
        return "contentsApiDispatch"
    if lane == "disclosure_state_wiring":
        return event_binding.get("disclosureActionType")
    return None


def runtime_target_ref_for_event_binding(event_binding, action_type):
    """Map eventBinding targetRef into backend runtimeInteractions targetRef vocabulary.

    topology_ui_seed eventBinding keeps the seed/import lane vocabulary. The
    layout_patch_json runtimeInteractions boundary validates dispatchInstanceOperation
    against instance-port:<portKind>:<instancePortId>:<operationBindingKey>, so
    seed/template candidates must cross-map before backend validation/assignment.
    """
    target_ref = event_binding.get("targetRef") if isinstance(event_binding, dict) else None
    target_ref = target_ref or ""
    if action_type == "dispatchInstanceOperation" and target_ref.startswith("instance:"):
        return "instance-port:" + target_ref[len("instance:"):]
    return target_ref


def build_runtime_interaction_candidate(node):
    """Build a draft runtimeInteractions[] entry from an Action/Step eventBinding.

    The candidate intentionally omits runtimeInteractionId. It carries the
    idempotency route fields needed by layout_patch_json consumers so a later
    preview/validate/apply path can persist it through ApplyConfirmedLayoutPatchAsync,
    where AssignRuntimeInteractionIds owns final id assignment.
    """
    event_binding = node.get("eventBinding") or {}
    action_type = runtime_action_type_for_event_binding(event_binding)
    if not action_type:
        return None
    # disclosure_state_wiring (round 24): this actionType family's runtimeInteractions[] entry
    # carries targetNodeId/statePath, never targetRef/payloadFrom -- matches
    # NpgsqlUiTopologyRepository.cs ValidateRuntimeInteractions' own shape for these actionTypes.
    if action_type in DISCLOSURE_ACTION_TYPES:
        return {
            "trigger": event_binding.get("trigger"),
            "actionType": action_type,
            "targetNodeId": event_binding.get("disclosureTargetNodeId", ""),
            "statePath": event_binding.get("disclosureStatePath") or "open",
            "sourceActionKey": node.get("key"),
        }
    target_ref = runtime_target_ref_for_event_binding(event_binding, action_type) or node.get("actionRef") or ""
    candidate = {
        "trigger": event_binding.get("trigger"),
        "actionType": action_type,
        "payloadFrom": event_binding.get("payloadFrom") or {},
        "sourceActionKey": node.get("key"),
    }
    if action_type == "dispatchInstanceOperation":
        candidate["instanceTargetRef"] = target_ref
    elif action_type == "dispatchExternalPort":
        candidate["portTargetRef"] = target_ref
    else:
        candidate["targetRef"] = target_ref
    if event_binding.get("outputProp"):
        candidate["outputProp"] = event_binding.get("outputProp")
    for field in ("idempotencyPolicy", "lifecycleDispatchConfirmed", "debounceMs"):
        if field in node:
            candidate[field] = node[field]
    return candidate

def build_admin_runtime_dispatch_override_candidate(node):
    """Build a dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger candidate entry from an
    Action/Step eventBinding whose wiringLane is admin_runtime_dispatch_override_wiring.

    Round 17 (2026-07-29): synced to backend/repository/NpgsqlUiTopologyRepository.cs
    ValidateDispatchTargetRefByTrigger / frontend/runtime/renderEmission.ts
    buildAdminRuntimeTargetRefOverrideByTrigger -- targetRef must already be in
    "manifest:<uuid>:<layer>:<action>" shape (validated separately by validate_wiring_node's
    lane-shape check, same as every other lane). Distinct from build_runtime_interaction_candidate:
    this lane overrides a node's OWN admin_runtime dispatch target/payload for one trigger,
    independent of runtimeInteractions[] entirely (round 6/15/16 design: action-authority-vs-
    effect-data separation) -- never folded into a runtimeInteractions[] entry.
    """
    event_binding = node.get("eventBinding") or {}
    if not isinstance(event_binding, dict):
        return None
    if event_binding.get("wiringLane") != "admin_runtime_dispatch_override_wiring":
        return None
    return {
        "trigger": event_binding.get("trigger"),
        "targetRef": event_binding.get("targetRef") or "",
        "payloadFrom": event_binding.get("payloadFrom") or {},
        "sourceActionKey": node.get("key"),
    }


def build_secondary_disclosure_runtime_interaction_candidate(node):
    """Build an ADDITIONAL runtimeInteractions[] entry from an Action/Step's
    secondaryDisclosureAction (round 24) -- independent of whatever the node's primary
    eventBinding/wiringLane already produced (a dispatch override candidate, a different
    runtimeInteractions candidate, or nothing). See
    wiring_lane_contract.lanes.disclosure_state_wiring.secondary_combination."""
    secondary = node.get("secondaryDisclosureAction")
    if not secondary:
        return None
    return {
        "trigger": secondary.get("trigger"),
        "actionType": secondary.get("actionType"),
        "targetNodeId": secondary.get("targetNodeId", ""),
        "statePath": secondary.get("statePath") or "open",
        "sourceActionKey": node.get("key"),
    }


def validate_ui_catalog_node(node, declared_surface, errors, path):
    allowed_kinds = set()
    if declared_surface:
        for k in dig(declared_surface, "component_catalog_refs", "componentKinds") or []:
            allowed_kinds.add(k.split(" ")[0])

    if node.get("kind") == "Field":
        control = node.get("control")
        if control and allowed_kinds and control not in allowed_kinds and not node.get("knownGapRefs"):
            errors.append(err("UNKNOWN_COMPONENT_KIND", path, "blocking", f"field control '{control}' is not a registered componentKind for this surface and carries no knownGapRef"))

    if node.get("kind") == "Modal":
        component_kind = node.get("componentKind")
        if component_kind and allowed_kinds and component_kind not in allowed_kinds and not node.get("knownGapRefs"):
            errors.append(err("UNKNOWN_COMPONENT_KIND", path, "blocking", f"modal componentKind '{component_kind}' is not a registered componentKind for this surface and carries no knownGapRef"))

    if node.get("kind") == "StyleRef":
        token = node.get("tokenRef", "")
        if token and not TOKEN_LIKE_RE.match(token) and not node.get("knownGapRefs"):
            errors.append(err("STYLE_REF_NOT_TOKEN", path, "blocking", f"styleRef tokenRef '{token}' is not a css-dictionary/topology-layout-class token and carries no knownGapRef"))


def validate_structural_node(node, parent, errors, path):
    """react_schema_contract.prohibited_features: form_without_fields,
    mutation_action_not_owned_by_a_form."""
    if node.get("kind") == "Form" and not (node.get("fields") or []):
        errors.append(err("EMPTY_FORM", path, "blocking", f"Form '{node.get('key')}' has no Field children"))

    if node.get("kind") == "Action":
        parent_kind = parent.get("kind") if parent else None
        action_lane = (node.get("eventBinding") or {}).get("wiringLane")
        owned_by_section_disclosure_trigger = (
            parent_kind == "Section" and action_lane in SECTION_OWNABLE_ACTION_LANES
        )
        if parent_kind not in VALID_ACTION_OWNER_NODE_KINDS and not owned_by_section_disclosure_trigger:
            errors.append(
                err(
                    "ACTION_NOT_OWNED_BY_FORM_OR_WORKFLOW",
                    path,
                    "blocking",
                    f"Action '{node.get('key')}' must be a direct child of a Form, Workflow, or Modal node "
                    f"(or a Section node, only when wiringLane is one of {sorted(SECTION_OWNABLE_ACTION_LANES)}), "
                    f"not {parent_kind or 'the document root'}",
                )
            )


def walk_and_validate(node, lanes_def, declared_surface, errors, path="$.root", parent=None):
    validate_wiring_node(node, lanes_def, errors, path)
    validate_ui_catalog_node(node, declared_surface, errors, path)
    validate_structural_node(node, parent, errors, path)
    for i, child in enumerate(node.get("children") or []):
        walk_and_validate(child, lanes_def, declared_surface, errors, f"{path}.children[{i}]", parent=node)


def collect_node_kinds_by_key(node, out):
    """Maps every node's key -> react_schema kind, across the whole tree (children only --
    every node this translator builds lives in `children`, including Form/Workflow's Field/
    Action/Step members; finalize_node's fields/actions/steps arrays are key-string summaries
    derived FROM children, not a separate storage location). Used by
    validate_disclosure_targets to check a disclosureTargetNodeId/secondaryDisclosureAction
    targetNodeId actually refers to a real node of the expected kind."""
    key = node.get("key")
    if key:
        out[key] = node.get("kind")
    for c in node.get("children") or []:
        collect_node_kinds_by_key(c, out)


def _disclosure_target_checks_for_node(node):
    """Yields (attr_label, actionType, targetNodeId) for every disclosure-family target this
    node's eventBinding/secondaryDisclosureAction declares -- the two independent places
    build_node can populate one (see wiring_lane_contract.lanes.disclosure_state_wiring)."""
    eb = node.get("eventBinding") or {}
    if isinstance(eb, dict) and eb.get("wiringLane") == "disclosure_state_wiring":
        yield ("disclosureActionType", eb.get("disclosureActionType"), eb.get("disclosureTargetNodeId"))
    secondary = node.get("secondaryDisclosureAction")
    if secondary:
        yield ("secondaryDisclosureActionType", secondary.get("actionType"), secondary.get("targetNodeId"))


def validate_disclosure_targets(node, kinds_by_key, errors, path="$.root"):
    """Cross-tree check (cannot live in validate_structural_node, which only sees one node and
    its immediate parent): every disclosureActionType/secondaryDisclosureActionType must be a
    recognized actionType, every disclosure target must require a non-empty targetNodeId, and
    -- for the *Modal family, the only container kind this translator can emit and therefore
    cross-check today -- that targetNodeId must resolve to an actual Modal node in this tree.
    A stale/typo'd/never-declared target fails closed here rather than silently producing a
    runtimeInteractions[] entry the runtime would fail to resolve (RUNTIME_INTERACTION_TARGET_NODE_NOT_FOUND
    / RUNTIME_INTERACTION_TARGET_KIND_MISMATCH at the backend layout-patch boundary, or a
    silently-inert click at runtime for seed-only data that bypasses that boundary)."""
    node_path = node.get("_path", path)
    for label, action_type, target_node_id in _disclosure_target_checks_for_node(node):
        if not action_type or action_type not in DISCLOSURE_ACTION_TYPES:
            errors.append(err(
                "DISCLOSURE_ACTION_TYPE_UNSUPPORTED", node_path, "blocking",
                f"{label} '{action_type}' on Action/Step '{node.get('key')}' is not a recognized disclosure actionType",
            ))
            continue
        if not target_node_id:
            errors.append(err(
                "DISCLOSURE_TARGET_NODE_REQUIRED", node_path, "blocking",
                f"{label} '{action_type}' on Action/Step '{node.get('key')}' requires a non-empty target node id",
            ))
            continue
        expected_kind = DISCLOSURE_TARGET_KIND_BY_ACTION_TYPE.get(action_type)
        if expected_kind is None:
            continue
        actual_kind = kinds_by_key.get(target_node_id)
        if actual_kind != expected_kind:
            errors.append(err(
                "DISCLOSURE_TARGET_KIND_MISMATCH", node_path, "blocking",
                f"{label} '{action_type}' on Action/Step '{node.get('key')}' targets '{target_node_id}' "
                f"(kind {actual_kind or 'not found in this tree'}), expected a {expected_kind} node",
            ))
    for c in node.get("children") or []:
        validate_disclosure_targets(c, kinds_by_key, errors, path)


# ---------------------------------------------------------------------------
# react_schema -> topology_ui_seed conversion (generate_topology_ui_seed mode)
# ---------------------------------------------------------------------------

def assign_paths(node, path="$.root"):
    """A JSON-deserialized react schema candidate carries no _path bookkeeping
    (strip_internal removed it on the way out of generate-react-schema); this
    reconstructs sourceReactPath for every node before conversion/validation."""
    node["_path"] = path
    for i, c in enumerate(node.get("children") or []):
        assign_paths(c, f"{path}.children[{i}]")


COMMON_NODE_KEYS = {"kind", "key", "label", "sourceYamlRefs", "knownGapRefs", "authorityMarker", "children", "_path"}

KIND_SPECIFIC_CONSUMED_KEYS = {
    "Projection": set(),
    "Category": set(),
    "Section": {"sectionKind"},
    "Form": {"target", "mode", "fields", "actions"},
    "Field": {"control", "required"},
    "Table": {"source", "display"},
    "Workflow": {"steps"},
    "Modal": {"componentKind", "title", "body"},
    "Step": {"eventBinding", "actionRef", "runtimeInteractions", "idempotencyPolicy", "lifecycleDispatchConfirmed", "debounceMs", "secondaryDisclosureAction"},
    "Action": {"eventBinding", "actionRef", "runtimeInteractions", "idempotencyPolicy", "lifecycleDispatchConfirmed", "debounceMs", "secondaryDisclosureAction"},
    "Validation": {"rule", "severity", "appliesTo"},
    "PropBinding": {"targetProp", "source"},
    "PayloadFrom": {"targetField", "source"},
    "StyleRef": {"target", "tokenRef"},
    "Unresolved": {"rawFragment"},
}


def convert_node_to_seed_record(node, schema_to_seed_map, target_surface, loss_entries):
    """exchange_mapping.schema_to_seed_record_mapping, one react_schema node at a time.

    Appends exchange_report_contract.loss_entry_fields-shaped dicts to
    loss_entries for anything that could not be mapped (no_silent_loss)."""
    react_kind = node.get("kind")
    path = node.get("_path", "$.root")
    record_type = schema_to_seed_map.get(react_kind)
    if record_type is None:
        loss_entries.append({
            "sourceReactPath": path,
            "property": "kind",
            "reason": f"react schema node kind '{react_kind}' has no schema_to_seed_record_mapping entry",
            "severity": "blocking",
            "knownGapRef": f"runtime_dispatch_or_projection_gap:unmapped_react_node_kind:{react_kind}",
        })
        return None

    record = {
        "recordType": record_type,
        "key": node.get("key"),
        "label": node.get("label"),
        "sourceYamlRefs": node.get("sourceYamlRefs") or [],
        "sourceReactPath": path,
        "knownGapRefs": list(node.get("knownGapRefs") or []),
    }
    if node.get("authorityMarker"):
        record["authorityMarker"] = node["authorityMarker"]

    converted_children = []
    for c in node.get("children") or []:
        child_record = convert_node_to_seed_record(c, schema_to_seed_map, target_surface, loss_entries)
        if child_record is not None:
            converted_children.append(child_record)

    if react_kind == "Projection":
        record["surface"] = target_surface
        record["categories"] = [c for c in converted_children if c["recordType"] == "topology_ui_category"]
    elif react_kind == "Category":
        record["categoryKey"] = record["key"]
        record["sections"] = [c for c in converted_children if c["recordType"] == "topology_ui_section"]
    elif react_kind == "Section":
        record["sectionKey"] = record["key"]
        record["sectionKind"] = node.get("sectionKind", "")
        record["children"] = converted_children
    elif react_kind == "Form":
        record["formKey"] = record["key"]
        record["target"] = node.get("target", "")
        record["mode"] = node.get("mode", "")
        record["fields"] = [c for c in converted_children if c["recordType"] == "topology_ui_field"]
        record["actions"] = [c for c in converted_children if c["recordType"] == "topology_ui_action"]
    elif react_kind == "Field":
        record["fieldKey"] = record["key"]
        record["control"] = node.get("control", "")
        record["required"] = bool(node.get("required", False))
        record["validationRefs"] = [c["key"] for c in converted_children if c["recordType"] == "topology_ui_validation"]
    elif react_kind == "Table":
        record["tableKey"] = record["key"]
        record["source"] = node.get("source", "")
        record["display"] = node.get("display", "")
        record["columns"] = converted_children
    elif react_kind == "Workflow":
        record["workflowKey"] = record["key"]
        record["steps"] = [c for c in converted_children if c["recordType"] == "topology_ui_workflow_step"]
    elif react_kind == "Modal":
        record["modalKey"] = record["key"]
        record["componentKind"] = node.get("componentKind", "disclosure/modal")
        if node.get("title"):
            record["title"] = node["title"]
        if node.get("body"):
            record["body"] = node["body"]
        record["children"] = converted_children
        # modal_self_close_invariant (wiring_lane_contract.lanes.disclosure_state_wiring): every
        # Modal this translator emits carries its own toggle->closeModal runtimeInteraction,
        # never authored/omittable -- modalFactory's requireBinding(spec, "toggle") fails the
        # whole render closed without it.
        record["runtimeInteractions"] = [{
            "trigger": "toggle",
            "actionType": "closeModal",
            "targetNodeId": record["key"],
            "statePath": "open",
            "sourceActionKey": record["key"],
        }]
    elif react_kind == "Step":
        record["stepKey"] = record["key"]
        record["actionRef"] = node.get("actionRef", "")
        record["eventBinding"] = node.get("eventBinding")
        runtime_interactions = []
        runtime_interaction = build_runtime_interaction_candidate(node)
        if runtime_interaction is not None:
            runtime_interactions.append(runtime_interaction)
        secondary_interaction = build_secondary_disclosure_runtime_interaction_candidate(node)
        if secondary_interaction is not None:
            runtime_interactions.append(secondary_interaction)
        if runtime_interactions:
            record["runtimeInteractions"] = runtime_interactions
        admin_runtime_override = build_admin_runtime_dispatch_override_candidate(node)
        if admin_runtime_override is not None:
            record["adminRuntimeDispatchOverride"] = admin_runtime_override
    elif react_kind == "Action":
        record["actionKey"] = record["key"]
        record["actionRef"] = node.get("actionRef", "")
        record["eventBinding"] = node.get("eventBinding")
        runtime_interactions = []
        runtime_interaction = build_runtime_interaction_candidate(node)
        if runtime_interaction is not None:
            runtime_interactions.append(runtime_interaction)
        secondary_interaction = build_secondary_disclosure_runtime_interaction_candidate(node)
        if secondary_interaction is not None:
            runtime_interactions.append(secondary_interaction)
        if runtime_interactions:
            record["runtimeInteractions"] = runtime_interactions
        admin_runtime_override = build_admin_runtime_dispatch_override_candidate(node)
        if admin_runtime_override is not None:
            record["adminRuntimeDispatchOverride"] = admin_runtime_override
    elif react_kind == "Validation":
        record["validationKey"] = record["key"]
        record["rule"] = node.get("rule", "")
        record["severity"] = node.get("severity", "warning")
    elif react_kind == "PropBinding":
        record["targetProp"] = node.get("targetProp", "")
        record["source"] = node.get("source", "")
    elif react_kind == "PayloadFrom":
        record["targetField"] = node.get("targetField", "")
        record["source"] = node.get("source", "")
    elif react_kind == "StyleRef":
        record["target"] = node.get("target", "")
        record["tokenRef"] = node.get("tokenRef", "")
    elif react_kind == "Unresolved":
        record["rawFragment"] = node.get("rawFragment", "")

    consumed = COMMON_NODE_KEYS | KIND_SPECIFIC_CONSUMED_KEYS.get(react_kind, set())
    for key in node.keys():
        if key not in consumed:
            loss_entries.append({
                "sourceReactPath": path,
                "property": key,
                "reason": f"property '{key}' on a {react_kind} node has no seed-record mapping target",
                "severity": "warning",
                "knownGapRef": None,
            })

    return record


# ---------------------------------------------------------------------------
# output assembly
# ---------------------------------------------------------------------------

def new_output_shell():
    return {
        "normalizedInputElements": [],
        "reactSchemaCandidate": None,
        "topologyUiSeedCandidate": None,
        # storage_adoption_contract: populated only for generate_topology_ui_seed,
        # mirroring topologyUiSeedCandidate's populated-when rule. A flat list of
        # topology_ui_seed_record wrappers (see flatten_topology_ui_seed_tree),
        # each sized against MANIFEST_TOPOLOGY_ARRAY_ELEMENT_BYTE_BUDGET -- this
        # is the seed-safe adoption shape; topologyUiSeedCandidate's nested tree
        # remains debug/review-only and must never be adopted as a single
        # manifest.topology array element (see PR #573's index row size failure).
        "topologyUiSeedFlatRecords": None,
        # storage_adoption_contract.adoption_candidate_separation_contract: the
        # actual seed-safe adoption shape, built from topologyUiSeedFlatRecords.
        # Populated only for generate_topology_ui_seed, same as topologyUiSeedFlatRecords.
        "adoptionCandidates": None,
        "exchangeReport": {},
        "validationErrors": [],
        "unresolvedGaps": [],
        "reverseTranslationBlockers": [],
        # Populated from the translator entry gate's own authoringReferences
        # (schema_seed_translator_entry_gate.py) as soon as the gate runs --
        # see cmd_generate_react_schema / cmd_generate_topology_seed. Stays []
        # only for the pre-gate early-return paths (SSOT load failure, input
        # file unreadable), which precede gate execution entirely.
        "authoringReferences": [],
    }


def build_react_schema_candidate(root_node, target_surface, source_refs):
    if root_node is None:
        fallback = {
            "kind": "Unresolved",
            "key": "no_root_projection",
            "label": "no root projection found",
            "rawFragment": "",
            "knownGapRefs": ["ssot_ambiguity_gap:no_root_projection_found"],
            "sourceYamlRefs": source_refs,
        }
        root_out = fallback
    else:
        root_out = strip_internal(root_node)
    return {
        "schema": "topolactor.react_schema.v1",
        "presetKey": target_surface,
        "surface": target_surface,
        "sourceYamlRefs": source_refs,
        "root": root_out,
    }


def build_exchange_report(source_refs, emitted_count, known_gap_refs, loss_entries, reverse_blockers, validation_errors, source_schema_id="topolactor.translator_input.v1", output_seed_schema_id=None):
    return {
        "sourceSchemaId": source_schema_id,
        "outputSeedSchemaId": output_seed_schema_id,
        "sourceYamlRefs": source_refs,
        "emittedRecords": emitted_count,
        "knownGapRefs": known_gap_refs,
        "lossEntries": loss_entries,
        "reverseTranslationBlockers": reverse_blockers,
        "validationErrors": validation_errors,
    }


COMMON_SEED_RECORD_REQUIRED_FIELDS = ["recordType", "key", "label", "sourceYamlRefs", "sourceReactPath", "knownGapRefs"]

SEED_RECORD_CHILD_LIST_FIELDS = ("categories", "sections", "children", "fields", "actions", "steps", "columns")


def validate_seed_record_tree(record, record_types_def, errors):
    """topology_ui_seed_contract.record_common_required_fields plus each
    record type's own `required` list, checked recursively over every emitted
    seed record. A required field that is missing, None, or an empty string
    is blocking; an empty *list* required field (e.g. an empty `sections`)
    is not flagged here, since structural rules like EMPTY_FORM already cover
    the cases where an empty list is actually invalid."""
    record_type = record.get("recordType")
    path = record.get("sourceReactPath", "$.root")
    key = record.get("key")

    for field in COMMON_SEED_RECORD_REQUIRED_FIELDS:
        value = record.get(field, "__MISSING__")
        if value == "__MISSING__" or value is None or value == "":
            errors.append(
                err(
                    "SEED_RECORD_MISSING_REQUIRED_FIELD",
                    path,
                    "blocking",
                    f"{record_type} record '{key}' missing required common field '{field}'",
                )
            )
    if not record.get("sourceYamlRefs"):
        errors.append(err("SEED_RECORD_EMPTY_SOURCE_YAML_REFS", path, "blocking", f"{record_type} record '{key}' has empty sourceYamlRefs"))

    type_def = record_types_def.get(record_type) or {}
    for field in type_def.get("required", []):
        value = record.get(field, "__MISSING__")
        if value == "__MISSING__" or value is None or value == "":
            errors.append(
                err(
                    "SEED_RECORD_MISSING_REQUIRED_FIELD",
                    path,
                    "blocking",
                    f"{record_type} record '{key}' missing required type-specific field '{field}'",
                )
            )

    for list_field in SEED_RECORD_CHILD_LIST_FIELDS:
        for child in record.get(list_field) or []:
            if isinstance(child, dict) and "recordType" in child:
                validate_seed_record_tree(child, record_types_def, errors)


def build_topology_ui_seed_candidate(supplied_schema, target_surface, root_record, exchange_report):
    return {
        "schema": "topolactor.topology_ui_seed.v1",
        "role": "draft_intake_artifact_not_active_topology",
        "seedKey": target_surface,
        "surface": target_surface,
        "sourceReactSchema": supplied_schema,
        "sourceYamlRefs": supplied_schema.get("sourceYamlRefs") or [],
        "projections": [root_record] if root_record is not None else [],
        "exchangeReport": exchange_report,
    }


def flatten_topology_ui_seed_tree(record, seed_key, parent_key=None, out=None):
    """storage_adoption_contract.flattened_record_contract: split a nested
    topology_ui_seed_candidate record tree into a flat list of small
    `topology_ui_seed_record` wrappers, one per node, each carrying a
    `parentKey` so the original tree can be reconstructed. Every wrapper's
    `record` keeps the node's own recordType/key/label/sourceYamlRefs/
    sourceReactPath/knownGapRefs/authorityMarker/eventBinding (nothing is
    dropped) -- only nested list fields (categories/sections/children/
    fields/actions/columns/steps) are replaced with a list of child keys,
    since those children become their own flat entries."""
    if out is None:
        out = []
    if record is None:
        return out

    record_type = record.get("recordType")
    shell = dict(record)
    nested_specs = SEED_RECORD_NESTED_LIST_KEYS.get(record_type, [])
    child_records = []
    for list_key, shell_key in nested_specs:
        children = shell.pop(list_key, None)
        if children is None:
            continue
        shell[shell_key] = [c.get("key") for c in children]
        child_records.extend(children)

    out.append({
        "type": "topology_ui_seed_record",
        "seedKey": seed_key,
        "parentKey": parent_key,
        "record": shell,
    })

    this_key = record.get("key")
    for child in child_records:
        flatten_topology_ui_seed_tree(child, seed_key, this_key, out)

    return out


def validate_flat_seed_records(flat_records, budget_bytes=MANIFEST_TOPOLOGY_ARRAY_ELEMENT_BYTE_BUDGET):
    """storage_adoption_contract.validation_rules: every flattened wrapper
    must independently fit the manifest.topology jsonb[] GIN index's
    per-array-element byte budget once it is adopted as a seed array
    element. Checked on the exact JSON text the seed would carry (compact
    separators, matching the existing seed store's single-line jsonb
    literal style) so this catches the failure at generation time instead
    of at insert time. budget_bytes is a UTF-8 *byte* budget (the Postgres
    index item size limit is byte-based), so the check must measure UTF-8
    encoded length, not Python string/character length -- multi-byte
    labels (e.g. non-ASCII text) would otherwise silently pass a
    character-count check while still overflowing the real byte budget."""
    errors = []
    for wrapper in flat_records:
        size = len(json.dumps(wrapper, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))
        if size > budget_bytes:
            record = wrapper.get("record") or {}
            path = record.get("sourceReactPath", "$.root")
            errors.append(err(
                "SEED_RECORD_EXCEEDS_STORAGE_BUDGET",
                path,
                "blocking",
                f"flattened record '{record.get('key')}' ({record.get('recordType')}) serializes to "
                f"{size} bytes, exceeding the {budget_bytes}-byte manifest.topology / "
                f"idx_manifest_topology storage_adoption_contract budget",
            ))
    return errors


# ---------------------------------------------------------------------------
# storage_adoption_contract.adoption_candidate_separation_contract
#
# top_ssot_alignment: flatten_topology_ui_seed_tree/validate_flat_seed_records
# above produce a review/migration intermediate only (seed_sql_authority:
# false as of this Bundle). This is the actual seed-safe adoption shape:
# bucket flat_records by recordType into per-target-table candidates
# (topology.ui_component_package / components_layout_design /
# components_style_design / ui_wiring_registry / ui_topology_tensor) plus a
# refs-only manifestRefsCandidate, so nothing UI-entity-shaped is ever
# proposed for manifest.topology adoption.
# ---------------------------------------------------------------------------

def split_flat_records_into_adoption_candidates(flat_records, seed_key):
    """adoption_candidate_separation_contract.package_authority_boundary: two DISTINCT
    package identities, never conflated.

    packageAdoptionCandidates targets topology.components_package_design -- the
    manifest-facing package authority (manifest_reference:
    manifest.topology[ui_projection].packageIds per docs/design/db-schema.yaml).
    componentGroupBundleAdoptionCandidates targets topology.ui_component_package --
    a narrower identity required only by topology.ui_topology_tensor.package_id's
    own FK constraint, never a manifest.packageIds target.

    adoption_candidate_separation_contract.primary_and_derived_candidate_relationship:
    topology_ui_action / topology_ui_workflow_step records have exactly one PRIMARY
    storage bucket -- layoutAdoptionCandidates, alongside every other structural
    layout-tree record type, since that is where the record's full content
    (label/eventBinding/authorityMarker) is canonically stored. wiringAdoptionCandidates
    and tensorAdoptionCandidates are DERIVED candidates projected FROM that same
    primary record for their respective persistence targets (ui_wiring_registry,
    ui_topology_tensor) -- narrower views, not a second full storage of the record --
    so every_topologyUiSeedFlatRecords_element_is_assigned_to_exactly_one_non_manifest_bucket_by_recordType
    (required_rules) is satisfied at the PRIMARY-bucket level; derived candidates are
    additional projections, not competing primary assignments."""
    package_candidates = []
    component_group_bundle_candidates = []
    layout_records = []
    design_candidates = []
    wiring_action_entries = []
    tensor_nodes = []

    # parent_scoped_identity_reconstruction (docs/design/runtime-orchestration-ssot.yaml
    # ui_projection_render_reachability_contract.layout_schema_structural_render_contract):
    # a record's authored key is only guaranteed unique within its own branch -- the same
    # scheme LayoutSchemaTensorComposer.Compose (backend) applies when composing these same
    # flat records must apply HERE too, so a tensor node grouped by its owning Form's key
    # never silently merges two DIFFERENT Form instances that happen to share that key.
    # Duplicate keys are namespaced "{parentKey}::{key}"; a record's resolved identity is
    # tracked in document order (flatten_topology_ui_seed_tree already emits parent-before-
    # child) so a child always resolves against the instance immediately preceding it, never
    # a static, order-independent lookup that cannot distinguish between duplicates.
    key_counts = {}
    for wrapper in flat_records:
        key_counts[(wrapper.get("record") or {}).get("key")] = \
            key_counts.get((wrapper.get("record") or {}).get("key"), 0) + 1
    duplicate_keys = {k for k, count in key_counts.items() if count > 1}
    last_resolved_key_by_raw_key = {}

    def resolve_and_track_identity(wrapper):
        record = wrapper.get("record") or {}
        raw_key = record.get("key")
        raw_parent_key = wrapper.get("parentKey")
        # Namespace by the parent's OWN resolved identity (already tracked, since document order
        # is parent-before-child), never the raw parentKey string alone -- a duplicated child key
        # under a duplicated parent key would otherwise namespace to the SAME
        # "{rawParentKey}::{key}" string in every branch (e.g. two Sections both keyed
        # "shared_section" each having their own Field keyed "shared_field" would both resolve to
        # "shared_section::shared_field"), silently colliding instead of staying attached to the
        # actual instance each was nested under.
        resolved_parent_key = last_resolved_key_by_raw_key.get(raw_parent_key, raw_parent_key)
        resolved_key = f"{resolved_parent_key}::{raw_key}" if raw_key in duplicate_keys else raw_key
        last_resolved_key_by_raw_key[raw_key] = resolved_key
        return resolved_key

    for wrapper in flat_records:
        record = wrapper.get("record") or {}
        record_type = record.get("recordType")
        key = record.get("key")
        # Resolve (and record) THIS record's own disambiguated identity before looking at its
        # children below, so a child's parentKey lookup below sees the instance it is actually
        # nested under, in document order.
        this_resolved_key = resolve_and_track_identity(wrapper)

        if record_type == "topology_ui_projection":
            # components_package_design.layout shape: [{componentId?, layoutNodeId?,
            # designId}, ...] -- honestly empty here since this exchange never
            # authors component+design pairs via UI Component Builder itself.
            package_candidates.append({
                "packageKey": f"{seed_key}.package",
                "layout": [],
                "sourceRecordKey": key,
            })
            component_group_bundle_candidates.append({
                "componentGroupBundleKey": f"{seed_key}.component_group_bundle",
                "packageKind": "fixed_form_projection",
                "packageSchemaJson": {
                    "seedKey": seed_key,
                    "surface": record.get("surface"),
                    "categoryKeys": record.get("categoryKeys") or [],
                },
                "sourceRecordKey": key,
            })
        elif record_type == "topology_ui_style_ref":
            design_candidates.append({
                "designKey": f"{seed_key}.{key}.design",
                "designSchemaJson": record,
                "sourceRecordKey": key,
            })
        else:
            # topology_ui_category / section / form / field / table / workflow /
            # workflow_step / validation / unresolved / action -- structural
            # layout tree content; this is the PRIMARY storage bucket for
            # topology_ui_action / topology_ui_workflow_step too (see
            # primary_and_derived_candidate_relationship above). wiring/tensor
            # candidates below are derived projections of the same record, not
            # a second primary-bucket assignment.
            layout_records.append(wrapper)

        if record_type in ("topology_ui_action", "topology_ui_workflow_step"):
            event_binding = record.get("eventBinding") or {}
            # Derived wiring projection, collected here and aggregated into a
            # single wiringAdoptionCandidates entry after the loop (below) --
            # topology.ui_wiring_registry.wiring_id is a singular FK from
            # topology.ui_topology_tensor and manifest.topology[ui_projection]
            # carries a singular wiringId (db/manifest_tables.sql, top SSOT,
            # unchanged), so one Projection's N actions/steps must resolve to
            # ONE wiring row, not N independent rows.
            wiring_action_entries.append({
                "wiringKey": f"{seed_key}.{key}.wiring",
                "wiringKind": event_binding.get("wiringLane"),
                "targetSurface": "manifest",
                "wiringSchemaJson": {
                    "eventBinding": event_binding,
                    "sourceActionKey": key,
                    "authorityMarker": record.get("authorityMarker"),
                },
                "sourceRecordKey": key,
            })
            # Reuse the runtimeInteractions candidate convert_node_to_seed_record
            # already built via build_runtime_interaction_candidate at the
            # react-schema -> seed conversion step (single source of truth --
            # never rebuilt here) rather than deriving a second one. Same reuse
            # discipline for adminRuntimeDispatchOverride (round 17).
            interactions = record.get("runtimeInteractions") or []
            admin_runtime_override = record.get("adminRuntimeDispatchOverride")
            if interactions:
                parent_key = wrapper.get("parentKey")
                # Resolve against the OWNING Form's disambiguated identity (the running map
                # built above), never the raw parentKey string alone -- two different Form
                # instances that happen to share a key must never merge their actions'
                # runtimeInteractions into the same tensor node. Correct for runtimeInteractions
                # specifically: backend/repository/LayoutSchemaTensorComposer.cs's
                # BuildInteractionsBySourceActionKey groups tensor entries by
                # "{formTensorNodeId}::{sourceActionKey}" and Compose looks them up against each
                # LEAF's OWN resolved nodeId scoped by ITS OWNING FORM -- the tensor node carrying
                # runtimeInteractions is deliberately keyed by the FORM, not the leaf.
                owning_form_key = (
                    last_resolved_key_by_raw_key.get(parent_key, parent_key)
                    if parent_key is not None
                    else this_resolved_key
                )
                tensor_nodes.append({
                    "nodeId": owning_form_key,
                    "nodeKind": "catalog_component",
                    "runtimeInteractions": list(interactions),
                })
            if admin_runtime_override:
                # Round 19 fix: dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger are NOT
                # scoped by BuildInteractionsBySourceActionKey's sourceActionKey mechanism --
                # LayoutSchemaTensorComposer.Compose merges them via nodeLocalDataByNodeId, a
                # plain NodeId match against a CATALOG LEAF's own resolved key (NodeLocalData's own
                # doc comment: "the match key is the tensor node's own NodeId directly, not
                # {formNodeId}::{sourceActionKey}"). Attaching this override to owning_form_key (a
                # STRUCTURAL topology_ui_form record, per StructuralRecordTypes) meant Compose's
                # isCatalogLeaf gate silently excluded it from EVERY merge -- confirmed via a real
                # live-DB round trip (Topolactor.Integration.Tests
                # AdminEnumHubRelationUiProjectionLiveDbTests.
                # DispatchAsync_AdminEnumManagementManifest_CreateGroupFormNode_..., which failed
                # with a null DispatchTargetRefByTrigger before this fix). The override must be
                # keyed by the ACTION LEAF's own resolved identity instead, matching every other
                # NodeLocalData field's convention.
                tensor_nodes.append({
                    "nodeId": this_resolved_key,
                    "nodeKind": "catalog_component",
                    "runtimeInteractions": [],
                    "adminRuntimeDispatchOverride": admin_runtime_override,
                })

    layout_candidates = []
    if layout_records:
        layout_candidates.append({
            "layoutKey": f"{seed_key}.layout",
            "layoutKind": "fixed_form_projection",
            "layoutSchemaJson": {"records": layout_records},
        })

    # One aggregate wiringAdoptionCandidates entry per Projection, never one per
    # Action/Step (topology.ui_wiring_registry.wiring_id is a singular FK from
    # topology.ui_topology_tensor.wiring_id, and manifest.topology[ui_projection]
    # carries a single wiringId, per db/manifest_tables.sql -- top SSOT, unchanged).
    # wiringSchemaJson.actions[] holds each action's individual wiring entry from
    # wiring_action_entries above, matching db/seed_empty.sql's already-adopted
    # auth.external.credential_management.projection.wiring row shape.
    wiring_candidates = []
    if wiring_action_entries:
        wiring_candidates.append({
            "wiringKey": f"{seed_key}.wiring",
            "wiringKind": "action_bundle",
            "targetSurface": "manifest",
            "wiringSchemaJson": {"actions": wiring_action_entries},
        })

    tensor_candidates = []
    if tensor_nodes:
        merged = {}
        order = []
        # Round 17: dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger are trigger-keyed
        # maps (not a list like runtimeInteractions), each entry a full override for that
        # trigger -- so per-nodeId source-action-key tracking for the completeness check below
        # is kept in a SIBLING dict, never inside the maps themselves (which must match the
        # exact Record<string,string> / Record<string,Record<string,string>> shape
        # frontend/backend both validate, with no room for extra metadata fields).
        override_source_action_keys_by_node = {}
        for node in tensor_nodes:
            nid = node["nodeId"]
            if nid not in merged:
                merged[nid] = {
                    "nodeId": nid,
                    "nodeKind": node["nodeKind"],
                    "runtimeInteractions": [],
                    "dispatchTargetRefByTrigger": {},
                    "dispatchPayloadFromByTrigger": {},
                }
                order.append(nid)
                override_source_action_keys_by_node[nid] = []
            merged[nid]["runtimeInteractions"].extend(node["runtimeInteractions"])
            override = node.get("adminRuntimeDispatchOverride")
            if override:
                trigger = override.get("trigger")
                if trigger:
                    merged[nid]["dispatchTargetRefByTrigger"][trigger] = override.get("targetRef", "")
                    if override.get("payloadFrom"):
                        merged[nid]["dispatchPayloadFromByTrigger"][trigger] = override["payloadFrom"]
                    override_source_action_keys_by_node[nid].append(override.get("sourceActionKey"))

        def _clean_tensor_node(n):
            out = {"nodeId": n["nodeId"], "nodeKind": n["nodeKind"], "runtimeInteractions": n["runtimeInteractions"]}
            if n["dispatchTargetRefByTrigger"]:
                out["dispatchTargetRefByTrigger"] = n["dispatchTargetRefByTrigger"]
            if n["dispatchPayloadFromByTrigger"]:
                out["dispatchPayloadFromByTrigger"] = n["dispatchPayloadFromByTrigger"]
            return out

        tensor_candidates.append({
            "tensorKey": f"{seed_key}.tensor",
            # The persisted tensor row's package_id FK needs a real
            # topology.ui_component_package id -- reference the
            # componentGroupBundleAdoptionCandidates key, NEVER the
            # packageAdoptionCandidates key (package_authority_boundary).
            "packageIdRef": f"<{component_group_bundle_candidates[0]['componentGroupBundleKey']}>" if component_group_bundle_candidates else None,
            "layoutPatchJson": {"nodes": [_clean_tensor_node(merged[nid]) for nid in order]},
            # Completeness-check-only sibling (never adopted into the DB row itself, same
            # convention as wiring_action_entries' sourceRecordKey above): which Action/Step
            # sourceActionKeys contributed an adminRuntimeDispatchOverride to each nodeId.
            "adminRuntimeDispatchOverrideSourceActionKeysByNodeId": {
                nid: keys for nid, keys in override_source_action_keys_by_node.items() if keys
            },
        })

    manifest_refs_candidate = None
    if package_candidates or layout_candidates or wiring_candidates or tensor_candidates:
        manifest_refs_candidate = {
            "type": "ui_projection",
            # packageIds references packageAdoptionCandidates (components_package_design)
            # keys, NEVER componentGroupBundleAdoptionCandidates (ui_component_package)
            # keys -- package_authority_boundary.
            "packageIds": [f"<{c['packageKey']}>" for c in package_candidates],
            "layoutId": f"<{layout_candidates[0]['layoutKey']}>" if layout_candidates else None,
            "wiringId": f"<{seed_key}.wiring>" if wiring_candidates else None,
            "tensorId": f"<{tensor_candidates[0]['tensorKey']}>" if tensor_candidates else None,
        }

    return {
        "packageAdoptionCandidates": package_candidates,
        "componentGroupBundleAdoptionCandidates": component_group_bundle_candidates,
        "layoutAdoptionCandidates": layout_candidates,
        "designAdoptionCandidates": design_candidates,
        "wiringAdoptionCandidates": wiring_candidates,
        "tensorAdoptionCandidates": tensor_candidates,
        "manifestRefsCandidate": manifest_refs_candidate,
    }


# storage_adoption_contract.top_ssot_violation_rule_definitions
# key-based denylist for credential_secret_projection_detected: checked against
# dict KEYS (an actual leaked field), not against string values inside
# forbidden-field guard-list arrays (e.g. "forbidden_fields":["secret",...]),
# so a policy declaration that NAMES prohibited vocabulary as documentation
# does not itself false-positive as a leak.
CREDENTIAL_SECRET_KEY_DENYLIST = {
    "password_hash",
    "jwt_secret",
    "refresh_token_plaintext",
    "plaintext_secret",
    "connection_string",
    "endpoint_real_value",
    "credential_plaintext",
    "private_key_material",
    "runtime_only_decrypted_payload",
    "access_token",
    "refresh_token",
    "client_secret",
    "api_key",
    "secret",
}

MANIFEST_TOPOLOGY_UI_PAYLOAD_KEYS = {
    "record", "fields", "actions", "columns", "sections", "categories",
    "categoryKeys", "sectionKeys", "fieldKeys", "actionKeys", "columnKeys", "stepKeys",
}

RUNTIME_DISPATCH_ACTION_TYPES = {"dispatchExternalPort", "dispatchInstanceOperation"}
RUNTIME_DISPATCH_WIRING_LANES = {"external_integration_wiring", "external_instance_wiring"}


def _find_denylisted_keys(value, denylist, acc, path):
    if isinstance(value, dict):
        for k, v in value.items():
            next_path = f"{path}.{k}"
            if k in denylist:
                acc.append(next_path)
            _find_denylisted_keys(v, denylist, acc, next_path)
    elif isinstance(value, list):
        for i, v in enumerate(value):
            _find_denylisted_keys(v, denylist, acc, f"{path}[{i}]")


def validate_adoption_candidates(candidates, flat_records):
    """storage_adoption_contract.top_ssot_violation_rule_definitions: checks the
    built adoptionCandidates bundle for all eight new rule ids. Collects every
    violation found -- never returns on the first failure (fail-fast is
    prohibited for this check)."""
    errors = []

    manifest_refs = candidates.get("manifestRefsCandidate")
    if manifest_refs is not None:
        offending = MANIFEST_TOPOLOGY_UI_PAYLOAD_KEYS.intersection(manifest_refs.keys())
        if offending:
            errors.append(err(
                "MANIFEST_TOPOLOGY_CONTAINS_UI_PAYLOAD_MATERIAL",
                "$.adoptionCandidates.manifestRefsCandidate",
                "blocking",
                f"manifestRefsCandidate carries UI-payload field(s) {sorted(offending)}; manifest.topology must hold refs/vectors only",
            ))
        if manifest_refs.get("type") == "topology_ui_seed_record" or manifest_refs.get("record") is not None:
            errors.append(err(
                "FLATTENED_SEED_RECORD_USED_AS_MANIFEST_FINAL_SHAPE",
                "$.adoptionCandidates.manifestRefsCandidate",
                "blocking",
                "manifestRefsCandidate must never itself be (or embed) a topology_ui_seed_record wrapper",
            ))

    # package_authority_target_table_mismatch: package_authority_boundary --
    # manifestRefsCandidate.packageIds must reference packageAdoptionCandidates
    # (topology.components_package_design) keys, never
    # componentGroupBundleAdoptionCandidates (topology.ui_component_package) keys;
    # tensorAdoptionCandidates[].packageIdRef is the reverse (must reference
    # componentGroupBundleAdoptionCandidates, never packageAdoptionCandidates).
    package_keys = {f"<{c.get('packageKey')}>" for c in candidates.get("packageAdoptionCandidates") or []}
    component_group_bundle_keys = {f"<{c.get('componentGroupBundleKey')}>" for c in candidates.get("componentGroupBundleAdoptionCandidates") or []}
    if manifest_refs is not None:
        for ref in manifest_refs.get("packageIds") or []:
            if ref in component_group_bundle_keys:
                errors.append(err(
                    "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH",
                    "$.adoptionCandidates.manifestRefsCandidate.packageIds",
                    "blocking",
                    f"manifestRefsCandidate.packageIds entry '{ref}' references a componentGroupBundleAdoptionCandidates "
                    "(topology.ui_component_package) key -- manifest.packageIds must reference "
                    "packageAdoptionCandidates (topology.components_package_design) keys only",
                ))
    for tensor in candidates.get("tensorAdoptionCandidates") or []:
        ref = tensor.get("packageIdRef")
        if ref is not None and ref in package_keys:
            errors.append(err(
                "PACKAGE_AUTHORITY_TARGET_TABLE_MISMATCH",
                "$.adoptionCandidates.tensorAdoptionCandidates.packageIdRef",
                "blocking",
                f"tensorAdoptionCandidates packageIdRef '{ref}' references a packageAdoptionCandidates "
                "(topology.components_package_design) key -- topology.ui_topology_tensor.package_id must "
                "reference componentGroupBundleAdoptionCandidates (topology.ui_component_package) keys only",
            ))

    # manifest_refs_candidate_reference_resolution: manifestRefsCandidate's ref
    # fields must resolve to an actually-emitted candidate key in the matching
    # bucket -- a refs-only shape alone is not proof the refs are wired
    # correctly (PR580 review finding: wiringId pointed at a key no
    # wiringAdoptionCandidates entry ever emitted).
    layout_keys = {f"<{c.get('layoutKey')}>" for c in candidates.get("layoutAdoptionCandidates") or []}
    wiring_keys = {f"<{c.get('wiringKey')}>" for c in candidates.get("wiringAdoptionCandidates") or []}
    tensor_keys = {f"<{c.get('tensorKey')}>" for c in candidates.get("tensorAdoptionCandidates") or []}
    if manifest_refs is not None:
        for ref in manifest_refs.get("packageIds") or []:
            if ref not in package_keys:
                errors.append(err(
                    "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED",
                    "$.adoptionCandidates.manifestRefsCandidate.packageIds",
                    "blocking",
                    f"manifestRefsCandidate.packageIds entry '{ref}' does not resolve to any emitted packageAdoptionCandidates[].packageKey",
                ))
        layout_id = manifest_refs.get("layoutId")
        if layout_id is not None and layout_id not in layout_keys:
            errors.append(err(
                "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED",
                "$.adoptionCandidates.manifestRefsCandidate.layoutId",
                "blocking",
                f"manifestRefsCandidate.layoutId '{layout_id}' does not resolve to any emitted layoutAdoptionCandidates[].layoutKey",
            ))
        wiring_id = manifest_refs.get("wiringId")
        if wiring_id is not None and wiring_id not in wiring_keys:
            errors.append(err(
                "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED",
                "$.adoptionCandidates.manifestRefsCandidate.wiringId",
                "blocking",
                f"manifestRefsCandidate.wiringId '{wiring_id}' does not resolve to any emitted wiringAdoptionCandidates[].wiringKey",
            ))
        tensor_id = manifest_refs.get("tensorId")
        if tensor_id is not None and tensor_id not in tensor_keys:
            errors.append(err(
                "MANIFEST_REFS_CANDIDATE_REFERENCE_UNRESOLVED",
                "$.adoptionCandidates.manifestRefsCandidate.tensorId",
                "blocking",
                f"manifestRefsCandidate.tensorId '{tensor_id}' does not resolve to any emitted tensorAdoptionCandidates[].tensorKey",
            ))

    if flat_records and not any([
        candidates.get("packageAdoptionCandidates"),
        candidates.get("layoutAdoptionCandidates"),
        candidates.get("wiringAdoptionCandidates"),
        candidates.get("tensorAdoptionCandidates"),
    ]):
        errors.append(err(
            "UI_PAYLOAD_NOT_SPLIT_TO_PACKAGE_LAYOUT_DESIGN_WIRING_TENSOR",
            "$.adoptionCandidates",
            "blocking",
            "topologyUiSeedFlatRecords is non-empty but no package/layout/wiring/tensor adoption candidate bucket was populated",
        ))

    tensor_interaction_action_keys = set()
    admin_runtime_override_action_keys = set()
    for tensor in candidates.get("tensorAdoptionCandidates") or []:
        for node in dig(tensor, "layoutPatchJson", "nodes") or []:
            for interaction in node.get("runtimeInteractions") or []:
                if interaction.get("sourceActionKey"):
                    tensor_interaction_action_keys.add(interaction["sourceActionKey"])
        for keys in (tensor.get("adminRuntimeDispatchOverrideSourceActionKeysByNodeId") or {}).values():
            admin_runtime_override_action_keys.update(k for k in keys if k)

    for wrapper in flat_records:
        record = wrapper.get("record") or {}
        record_type = record.get("recordType")
        key = record.get("key")
        path = record.get("sourceReactPath", "$.root")
        if record_type not in ("topology_ui_action", "topology_ui_workflow_step"):
            continue

        event_binding = record.get("eventBinding") or {}
        if event_binding.get("wiringLane") in RUNTIME_DISPATCH_WIRING_LANES and key not in tensor_interaction_action_keys:
            errors.append(err(
                "RUNTIME_INTERACTIONS_NOT_PERSISTED_LAYOUT_PATH",
                path,
                "blocking",
                f"Action/Step '{key}' has a {event_binding.get('wiringLane')} eventBinding but no corresponding tensorAdoptionCandidates runtimeInteractions[] entry",
            ))

        if event_binding.get("wiringLane") == "admin_runtime_dispatch_override_wiring" and key not in admin_runtime_override_action_keys:
            errors.append(err(
                "ADMIN_RUNTIME_DISPATCH_OVERRIDE_NOT_PERSISTED_LAYOUT_PATH",
                path,
                "blocking",
                f"Action/Step '{key}' has an admin_runtime_dispatch_override_wiring eventBinding but no "
                "corresponding tensorAdoptionCandidates dispatchTargetRefByTrigger entry",
            ))

        for interaction in record.get("runtimeInteractions") or []:
            action_type = interaction.get("actionType")
            if "runtimeInteractionId" in interaction:
                errors.append(err(
                    "IDEMPOTENCY_CARRIER_MISSING_FOR_RUNTIME_DISPATCH",
                    path,
                    "blocking",
                    f"Action/Step '{key}' runtimeInteractions candidate must never carry runtimeInteractionId (backend-persist-time-only assignment authority)",
                ))
            if action_type not in RUNTIME_DISPATCH_ACTION_TYPES:
                continue
            missing = []
            if not interaction.get("trigger"):
                missing.append("trigger")
            target_field = "instanceTargetRef" if action_type == "dispatchInstanceOperation" else "portTargetRef"
            if not interaction.get(target_field):
                missing.append(target_field)
            if "payloadFrom" not in interaction:
                missing.append("payloadFrom")
            if missing:
                errors.append(err(
                    "IDEMPOTENCY_CARRIER_MISSING_FOR_RUNTIME_DISPATCH",
                    path,
                    "blocking",
                    f"Action/Step '{key}' {action_type} runtimeInteractions candidate is missing idempotency route field(s) {missing}",
                ))

    for bucket_name in (
        "packageAdoptionCandidates", "componentGroupBundleAdoptionCandidates", "layoutAdoptionCandidates",
        "designAdoptionCandidates", "wiringAdoptionCandidates", "tensorAdoptionCandidates",
    ):
        for i, item in enumerate(candidates.get(bucket_name) or []):
            hits = []
            _find_denylisted_keys(item, CREDENTIAL_SECRET_KEY_DENYLIST, hits, f"$.adoptionCandidates.{bucket_name}[{i}]")
            for path in hits:
                errors.append(err("CREDENTIAL_SECRET_PROJECTION_DETECTED", path, "blocking", f"{bucket_name}[{i}] carries a denylisted credential/secret field"))
    if manifest_refs is not None:
        hits = []
        _find_denylisted_keys(manifest_refs, CREDENTIAL_SECRET_KEY_DENYLIST, hits, "$.adoptionCandidates.manifestRefsCandidate")
        for path in hits:
            errors.append(err("CREDENTIAL_SECRET_PROJECTION_DETECTED", path, "blocking", "manifestRefsCandidate carries a denylisted credential/secret field"))

    return errors


def render_seed_sql_fragment(flat_records, indent="        "):
    """Render flattened records as ready-to-paste jsonb[] array element
    lines, matching the existing seed store's single-line-per-element
    style. This is still a generated artifact (generated_artifact_operation_policy):
    a reviewer must read and adopt it into the tracked seed store, not paste it blindly."""
    lines = []
    for wrapper in flat_records:
        text = json.dumps(wrapper, separators=(",", ":"), ensure_ascii=False)
        if "'" in text:
            # Defensive: single quotes would break the SQL string literal;
            # protected_boundary content never contains them, but fail loud
            # rather than emit broken SQL if some future field ever does.
            raise SystemExit(f"seed record for key {wrapper.get('record', {}).get('key')} contains a single quote; cannot render as a SQL literal")
        lines.append(f"{indent}'{text}'::jsonb")
    return ",\n".join(lines) + "\n" if lines else ""


def resolve_safe_output_path(output_arg):
    repo_root = REPO_ROOT.resolve()
    candidate = Path(output_arg)
    out_path = candidate if candidate.is_absolute() else (repo_root / candidate)
    out_path = out_path.resolve()
    try:
        out_path.relative_to(repo_root)
    except ValueError as exc:
        raise SystemExit(f"--output path escapes repository root: {output_arg}") from exc
    return out_path


# ---------------------------------------------------------------------------
# generate.log trace evidence
#
# *seed.sql / SSOT docs remain the production storage authority; generated
# JSON is a local/tmp projection (.agent/tools/generated/*, gitignored), not
# tracked evidence. .agent/tools/logs/generate.log is the tracked JSON
# Lines regeneration index instead: one line per generation attempt naming
# what was generated from what, with what command, and its output hash, so a
# proof can re-run the command and hash-check the result without the heavy
# JSON itself being committed. Append-only; only written when the caller
# opts in with --generate-log. This is trace evidence only -- never seed
# adoption authority, never proof completion by itself.
# ---------------------------------------------------------------------------

def sha256_file(path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def resolve_generate_log_path(path_arg):
    repo_root = REPO_ROOT.resolve()
    candidate = Path(path_arg)
    out_path = candidate if candidate.is_absolute() else (repo_root / candidate)
    out_path = out_path.resolve()
    try:
        out_path.relative_to(repo_root)
    except ValueError as exc:
        raise SystemExit(f"--generate-log path escapes repository root: {path_arg}") from exc
    return out_path


def _generate_log_mode_and_embedded_candidate_kind(doc):
    if doc.get("topologyUiSeedCandidate") is not None:
        return "generate_topology_ui_seed", "topology_ui_seed_candidate"
    if doc.get("reactSchemaCandidate") is not None:
        return "generate_react_schema", "react_schema_candidate"
    return None, None


def _generate_log_command(args):
    parts = ["react-schema-topology-seed-translator", getattr(args, "command", ""), "--input", args.input]
    if getattr(args, "output", None):
        parts += ["--output", args.output]
    return " ".join(parts)


def build_generate_log_record(args, doc, sha256_value):
    # sha256_value always hashes the full topolactor.translator_output.v1
    # document written/emitted by write_output (whether or not --output
    # persisted it to disk) -- outputKind/outputSchemaId must describe that
    # same artifact, not the react_schema_candidate/topologyUiSeedCandidate
    # embedded inside it. embeddedCandidateKind names which candidate the
    # document embeds, without claiming that candidate alone was hashed.
    mode, embedded_candidate_kind = _generate_log_mode_and_embedded_candidate_kind(doc)
    seed_evidence = doc.get("seedEvidence") or {}
    schema_candidate = doc.get("reactSchemaCandidate") or {}
    seed_candidate = doc.get("topologyUiSeedCandidate") or {}
    seed_key = schema_candidate.get("surface") or seed_candidate.get("surface")
    return {
        "datetime": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "nametag": getattr(args, "nametag", None) or Path(args.input).stem,
        "mode": mode,
        "source": args.input,
        "sourceSeedSql": getattr(args, "source_seed_sql", None),
        "seedKey": seed_key,
        "manifestId": seed_evidence.get("screenUuid"),
        "command": _generate_log_command(args),
        "outputKind": "translator_output_document",
        "outputSchemaId": doc.get("schemaId"),
        "embeddedCandidateKind": embedded_candidate_kind,
        "outputPath": getattr(args, "output", None),
        "sha256": sha256_value,
        "gateStatus": doc.get("gateStatus"),
        "validationErrorCount": len(doc.get("validationErrors") or []),
        "unresolvedGapCount": len(doc.get("unresolvedGaps") or []),
        "taskRef": getattr(args, "task_ref", None),
        "prRef": getattr(args, "pr_ref", None),
    }


def append_generate_log(path_arg, record):
    out_path = resolve_generate_log_path(path_arg)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n")


def write_output(args, doc):
    text = json.dumps(doc, indent=2, ensure_ascii=False, sort_keys=False) + "\n"
    out_path = None
    if getattr(args, "output", None):
        out_path = resolve_safe_output_path(args.output)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(text, encoding="utf-8")
    sys.stdout.write(text)
    if getattr(args, "generate_log", None):
        sha256_value = sha256_file(out_path) if out_path is not None else hashlib.sha256(text.encode("utf-8")).hexdigest()
        record = build_generate_log_record(args, doc, sha256_value)
        append_generate_log(args.generate_log, record)


# ---------------------------------------------------------------------------
# CLI commands
# ---------------------------------------------------------------------------

def cmd_generate_react_schema(args):
    repo_root = REPO_ROOT
    output = new_output_shell()

    ssot_root, ssot_errors = load_ssot(repo_root)
    if ssot_errors:
        output["validationErrors"].extend(ssot_errors)
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    try:
        with open(args.input, "r", encoding="utf-8") as f:
            envelope = json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        output["validationErrors"].append(err("INPUT_FILE_UNREADABLE", "$.input", "blocking", str(exc)))
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    # translator entry gate: same envelope JSON, no separate gate-only input.
    # gateStatus != pass fails closed before any conversion pipeline runs.
    gate = _gate_core()
    gate_result = gate.validate_translator_entry(envelope, ssot_root=ssot_root, expected_mode="generate_react_schema")
    output["gateStatus"] = gate_result["gateStatus"]
    output["authoringReferences"] = gate_result["authoringReferences"]
    if gate_result["gateStatus"] != gate.GATE_STATUS_PASS:
        output["validationErrors"].extend(gate_result["entryValidation"]["validationErrors"])
        output["unresolvedGaps"] = gate_result["unresolvedGaps"]
        output["exchangeReport"] = build_exchange_report(
            envelope.get("sourceYamlRefs") or [],
            0,
            gate_result["unresolvedGaps"],
            gate_result.get("lossEntries", []),
            [],
            output["validationErrors"],
        )
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    vocabulary = protected_vocabulary(ssot_root)
    val_errors, mode, input_text, target_surface = validate_input_envelope(envelope, ssot_root, vocabulary)
    output["validationErrors"].extend(val_errors)

    if mode is not None and mode != "generate_react_schema":
        output["validationErrors"].append(
            err(
                "MODE_MISMATCH",
                "$.mode",
                "blocking",
                f"generate-react-schema requires envelope mode 'generate_react_schema', got '{mode}'",
            )
        )

    fatal_rule_ids = {"INPUT_TEXT_EMPTY", "SOURCE_YAML_REFS_EMPTY", "INPUT_MODE_INVALID", "MODE_MISMATCH"}
    if any(e["ruleId"] in fatal_rule_ids for e in output["validationErrors"]):
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    normalized, root_node, parse_errors = parse_markup(input_text)
    output["normalizedInputElements"] = normalized
    output["validationErrors"].extend(parse_errors)

    if root_node is not None:
        finalize_node(root_node)
        lanes_def = dig(ssot_root, "wiring_lane_contract", "lanes") or {}
        declared_surfaces = dig(ssot_root, "declared_seed_surface_catalog", "known_declared_surfaces") or []
        declared_surface = next((s for s in declared_surfaces if s.get("seed_surface_key") == target_surface), None)
        tree_errors = []
        walk_and_validate(root_node, lanes_def, declared_surface, tree_errors)
        kinds_by_key = {}
        collect_node_kinds_by_key(root_node, kinds_by_key)
        validate_disclosure_targets(root_node, kinds_by_key, tree_errors)
        output["validationErrors"].extend(tree_errors)

    source_refs = envelope.get("sourceYamlRefs") or []
    output["reactSchemaCandidate"] = build_react_schema_candidate(root_node, target_surface, source_refs)
    output["topologyUiSeedCandidate"] = None
    output["reverseTranslationBlockers"] = []

    unresolved_gaps = collect_known_gap_refs(root_node) if root_node is not None else []
    for envelope_gap in envelope.get("knownGapRefs") or []:
        if envelope_gap not in unresolved_gaps:
            unresolved_gaps.append(envelope_gap)
    output["unresolvedGaps"] = unresolved_gaps

    output["exchangeReport"] = build_exchange_report(
        source_refs,
        count_records(root_node) if root_node is not None else 0,
        unresolved_gaps,
        [],
        [],
        output["validationErrors"],
    )

    doc = {"schemaId": "topolactor.translator_output.v1", **output}

    if args.scenario_uuid:
        doc["scenario"] = {
            "uuid": args.scenario_uuid,
            "worktype": args.scenario_worktype,
            "targetBranch": args.scenario_branch,
        }

    seed_evidence, evidence_errors = passthrough_seed_evidence(envelope)
    doc["validationErrors"].extend(evidence_errors)
    if seed_evidence is not None:
        doc["seedEvidence"] = seed_evidence

    write_output(args, doc)
    return 1 if any(is_blocking(e) for e in doc["validationErrors"]) else 0


def cmd_generate_topology_seed(args):
    repo_root = REPO_ROOT
    output = new_output_shell()

    ssot_root, ssot_errors = load_ssot(repo_root)
    if ssot_errors:
        output["validationErrors"].extend(ssot_errors)
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    try:
        with open(args.input, "r", encoding="utf-8") as f:
            envelope = json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        output["validationErrors"].append(err("INPUT_FILE_UNREADABLE", "$.input", "blocking", str(exc)))
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    # translator entry gate: same envelope JSON, no separate gate-only input.
    # gateStatus != pass fails closed before any conversion pipeline runs.
    gate = _gate_core()
    gate_result = gate.validate_translator_entry(envelope, ssot_root=ssot_root, expected_mode="generate_topology_ui_seed")
    output["gateStatus"] = gate_result["gateStatus"]
    output["authoringReferences"] = gate_result["authoringReferences"]
    if gate_result["gateStatus"] != gate.GATE_STATUS_PASS:
        output["validationErrors"].extend(gate_result["entryValidation"]["validationErrors"])
        output["unresolvedGaps"] = gate_result["unresolvedGaps"]
        output["exchangeReport"] = build_exchange_report(
            envelope.get("sourceYamlRefs") or [],
            0,
            gate_result["unresolvedGaps"],
            gate_result.get("lossEntries", []),
            [],
            output["validationErrors"],
            source_schema_id="topolactor.react_schema.v1",
            output_seed_schema_id="topolactor.topology_ui_seed.v1",
        )
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    vocabulary = protected_vocabulary(ssot_root)
    val_errors, mode, input_text, target_surface = validate_input_envelope(envelope, ssot_root, vocabulary)
    output["validationErrors"].extend(val_errors)

    if mode is not None and mode != "generate_topology_ui_seed":
        output["validationErrors"].append(
            err(
                "MODE_MISMATCH",
                "$.mode",
                "blocking",
                f"generate-topology-seed requires envelope mode 'generate_topology_ui_seed', got '{mode}'",
            )
        )

    fatal_rule_ids = {"INPUT_TEXT_EMPTY", "SOURCE_YAML_REFS_EMPTY", "INPUT_MODE_INVALID", "MODE_MISMATCH"}
    if any(e["ruleId"] in fatal_rule_ids for e in output["validationErrors"]):
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    # generate_topology_ui_seed's inputText is a JSON string carrying the
    # react schema candidate to convert (mode_vocabulary.generate_topology_ui_seed),
    # never free/markup text -- see input_format_contract.mode_vocabulary.
    try:
        supplied_schema = json.loads(input_text)
    except json.JSONDecodeError as exc:
        output["validationErrors"].append(err("INPUT_TEXT_NOT_VALID_JSON", "$.inputText", "blocking", f"inputText must be a JSON string of a react schema candidate: {exc}"))
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    if not isinstance(supplied_schema, dict) or supplied_schema.get("schema") != "topolactor.react_schema.v1":
        output["validationErrors"].append(err("REACT_SCHEMA_CANDIDATE_SCHEMA_MISMATCH", "$.inputText", "blocking", "parsed inputText must be a topolactor.react_schema.v1 object"))
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    root_node = supplied_schema.get("root")
    if not isinstance(root_node, dict):
        output["validationErrors"].append(err("REACT_SCHEMA_CANDIDATE_ROOT_MISSING", "$.inputText.root", "blocking", "react schema candidate is missing a root node"))
        doc = {"schemaId": "topolactor.translator_output.v1", **output}
        write_output(args, doc)
        return 3

    output["reactSchemaCandidate"] = supplied_schema  # carried through verbatim, for audit

    assign_paths(root_node)
    lanes_def = dig(ssot_root, "wiring_lane_contract", "lanes") or {}
    declared_surfaces = dig(ssot_root, "declared_seed_surface_catalog", "known_declared_surfaces") or []
    declared_surface = next((s for s in declared_surfaces if s.get("seed_surface_key") == target_surface), None)
    tree_errors = []
    # The supplied schema is caller-controlled input, not necessarily this
    # translator's own generate-react-schema output -- re-validate it against
    # the same contracts rather than trusting it blindly (exchange_mapping
    # canonical_direction rule).
    walk_and_validate(root_node, lanes_def, declared_surface, tree_errors)
    kinds_by_key = {}
    collect_node_kinds_by_key(root_node, kinds_by_key)
    validate_disclosure_targets(root_node, kinds_by_key, tree_errors)
    output["validationErrors"].extend(tree_errors)

    schema_to_seed_map = dig(ssot_root, "exchange_mapping", "schema_to_seed_record_mapping") or {}
    loss_entries = []
    root_record = convert_node_to_seed_record(root_node, schema_to_seed_map, target_surface, loss_entries)
    for entry in loss_entries:
        if entry["severity"] == "blocking":
            output["validationErrors"].append(err("REACT_NODE_KIND_UNMAPPED", entry["sourceReactPath"], "blocking", entry["reason"]))

    # Post-conversion check: every emitted seed record, recursively, against
    # topology_ui_seed_contract.record_common_required_fields and its own
    # record_types[...].required list. A record passing conversion is not
    # the same as a record satisfying the contract's required-field shape.
    if root_record is not None:
        record_types_def = dig(ssot_root, "topology_ui_seed_contract", "record_types") or {}
        validate_seed_record_tree(root_record, record_types_def, output["validationErrors"])

    unresolved_gaps = collect_known_gap_refs(root_node)
    for loss_gap in (e["knownGapRef"] for e in loss_entries if e.get("knownGapRef")):
        if loss_gap not in unresolved_gaps:
            unresolved_gaps.append(loss_gap)
    for envelope_gap in envelope.get("knownGapRefs") or []:
        if envelope_gap not in unresolved_gaps:
            unresolved_gaps.append(envelope_gap)
    output["unresolvedGaps"] = unresolved_gaps
    output["reverseTranslationBlockers"] = []

    source_refs = supplied_schema.get("sourceYamlRefs") or []
    exchange_report = build_exchange_report(
        source_refs,
        count_records(root_node),
        unresolved_gaps,
        loss_entries,
        [],
        output["validationErrors"],
        source_schema_id="topolactor.react_schema.v1",
        output_seed_schema_id="topolactor.topology_ui_seed.v1",
    )
    output["exchangeReport"] = exchange_report
    output["topologyUiSeedCandidate"] = build_topology_ui_seed_candidate(supplied_schema, target_surface, root_record, exchange_report)

    # storage_adoption_contract: derive the seed-safe flat adoption shape from
    # the same root_record the nested candidate above was built from, and
    # validate every flattened element against the manifest.topology /
    # idx_manifest_topology GIN index byte budget before it is ever proposed
    # for seed adoption.
    flat_records = flatten_topology_ui_seed_tree(root_record, target_surface) if root_record is not None else []
    output["topologyUiSeedFlatRecords"] = flat_records
    budget_errors = validate_flat_seed_records(flat_records)
    output["validationErrors"].extend(budget_errors)

    # adoption_candidate_separation_contract: the actual seed-safe adoption
    # shape. Built and validated even when budget_errors is non-empty, so a
    # caller sees every collected violation from one run (fail-fast is
    # prohibited for this check) rather than only the byte-budget errors.
    adoption_candidates = split_flat_records_into_adoption_candidates(flat_records, target_surface)
    output["adoptionCandidates"] = adoption_candidates
    output["validationErrors"].extend(validate_adoption_candidates(adoption_candidates, flat_records))

    doc = {"schemaId": "topolactor.translator_output.v1", **output}

    if args.scenario_uuid:
        doc["scenario"] = {
            "uuid": args.scenario_uuid,
            "worktype": args.scenario_worktype,
            "targetBranch": args.scenario_branch,
        }

    seed_evidence, evidence_errors = passthrough_seed_evidence(envelope)
    doc["validationErrors"].extend(evidence_errors)
    if seed_evidence is not None:
        doc["seedEvidence"] = seed_evidence

    write_output(args, doc)

    # --seed-sql-fragment: optional, opt-in convenience artifact rendering
    # topologyUiSeedFlatRecords as ready-to-paste jsonb[] array element
    # lines (generated_artifact_operation_policy: still a generated artifact,
    # not seed adoption authority by itself -- a reviewer must read and
    # adopt it into *seed.sql). Skipped when any record is over budget so a
    # broken fragment is never handed to a reviewer as if it were safe.
    if getattr(args, "seed_sql_fragment", None) and not any(is_blocking(e) for e in doc["validationErrors"]):
        fragment_path = resolve_safe_output_path(args.seed_sql_fragment)
        fragment_path.parent.mkdir(parents=True, exist_ok=True)
        fragment_path.write_text(render_seed_sql_fragment(flat_records), encoding="utf-8")

    return 1 if any(is_blocking(e) for e in doc["validationErrors"]) else 0


def _not_implemented_handler(mode):
    def handler(args):
        doc = {
            "schemaId": "topolactor.translator_output.v1",
            "status": "not_implemented_out_of_scope",
            "mode": mode,
            "normalizedInputElements": [],
            "reactSchemaCandidate": None,
            "topologyUiSeedCandidate": None,
            "exchangeReport": {},
            "validationErrors": [
                err(
                    "MODE_NOT_IMPLEMENTED",
                    "$.mode",
                    "blocking",
                    f"{mode} is out of scope for this Bundle; see docs/design/react-schema-topology-seed-translator-production-policy.md",
                )
            ],
            "unresolvedGaps": [],
            "reverseTranslationBlockers": [],
        }
        write_output(args, doc)
        return 3
    return handler


def build_arg_parser():
    parser = argparse.ArgumentParser(prog="react-schema-topology-seed-translator")
    sub = parser.add_subparsers(dest="command", required=True)

    gen = sub.add_parser("generate-react-schema")
    gen.add_argument("--input", required=True)
    gen.add_argument("--output")
    gen.add_argument("--scenario-uuid")
    gen.add_argument("--scenario-worktype", default="implementation_change")
    gen.add_argument("--scenario-branch", default="")
    gen.add_argument("--generate-log", default=None, help="Append a JSON-Lines regeneration-trace record to this path (e.g. .agent/tools/logs/generate.log); omit to skip logging entirely.")
    gen.add_argument("--nametag", default=None, help="Free-form label for the generate.log record; defaults to the --input file stem.")
    gen.add_argument("--task-ref", default=None, help="Optional taskRef value for the generate.log record (e.g. a PR reference).")
    gen.add_argument("--pr-ref", default=None, help="Optional prRef value for the generate.log record.")
    gen.add_argument("--source-seed-sql", default=None, help="Passthrough label only, e.g. a seed SQL file reference; recorded in generate.log verbatim, never opened or read by the translator.")
    gen.set_defaults(func=cmd_generate_react_schema)

    gen2 = sub.add_parser("generate-topology-seed")
    gen2.add_argument("--input", required=True)
    gen2.add_argument("--output")
    gen2.add_argument("--scenario-uuid")
    gen2.add_argument("--scenario-worktype", default="implementation_change")
    gen2.add_argument("--scenario-branch", default="")
    gen2.add_argument("--generate-log", default=None, help="Append a JSON-Lines regeneration-trace record to this path (e.g. .agent/tools/logs/generate.log); omit to skip logging entirely.")
    gen2.add_argument("--nametag", default=None, help="Free-form label for the generate.log record; defaults to the --input file stem.")
    gen2.add_argument("--task-ref", default=None, help="Optional taskRef value for the generate.log record (e.g. a PR reference).")
    gen2.add_argument("--pr-ref", default=None, help="Optional prRef value for the generate.log record.")
    gen2.add_argument("--source-seed-sql", default=None, help="Passthrough label only, e.g. a seed SQL file reference; recorded in generate.log verbatim, never opened or read by the translator.")
    gen2.add_argument("--seed-sql-fragment", default=None, help="Optional path (under the repo root) to write topologyUiSeedFlatRecords rendered as ready-to-paste jsonb[] array element lines, one per storage_adoption_contract-flattened record. Still a generated artifact (not seed adoption authority) -- review before pasting into the tracked seed store. Skipped when any record exceeds the manifest.topology / idx_manifest_topology storage budget.")
    gen2.set_defaults(func=cmd_generate_topology_seed)

    rtc = sub.add_parser("round-trip-check")
    rtc.add_argument("--input", required=False)
    rtc.add_argument("--output")
    rtc.set_defaults(func=_not_implemented_handler("round_trip_check"))

    return parser


def main(argv=None):
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
