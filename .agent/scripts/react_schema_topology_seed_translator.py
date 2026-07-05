#!/usr/bin/env python3
"""react_schema_topology_seed_translator.py -- generate-react-schema implementation body.

Implements the `generate_react_schema` mode of
docs/design/react-schema-topology-seed-translator-ssot.yaml:

    inputText -> input_text_markup_grammar_contract parse
              -> text_decomposition_contract normalized elements
              -> react_schema_contract candidate
              -> wiring_lane_contract / ui_catalog_boundary_contract validation
              -> output_format_contract shaped document

`generate-topology-seed` and `round-trip-check` are explicitly out of scope for
this Bundle and fail closed with a `not_implemented_out_of_scope` document
rather than raising or silently succeeding.

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
import json
import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent.parent
sys.path.insert(0, str(SCRIPT_DIR / "lib"))
import minimal_yaml as yaml  # noqa: E402

SSOT_REL_PATH = "docs/design/react-schema-topology-seed-translator-ssot.yaml"

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

CONTAINER_UNITS = {"projection", "category", "section", "form", "workflow"}
LEAF_UNITS = {"field", "table", "step", "action", "validation", "prop_binding", "payload_from", "style_ref"}
ALL_TAGGABLE_UNITS = CONTAINER_UNITS | LEAF_UNITS

UNIT_TO_NODE_KIND = {
    "projection": "Projection",
    "category": "Category",
    "section": "Section",
    "form": "Form",
    "field": "Field",
    "table": "Table",
    "workflow": "Workflow",
    "step": "Step",
    "action": "Action",
    "validation": "Validation",
    "prop_binding": "PropBinding",
    "payload_from": "PayloadFrom",
    "style_ref": "StyleRef",
}

TAG_LINE_RE = re.compile(r'^\[(?P<slash>/)?(?P<kind>[A-Za-z_][A-Za-z0-9_]*)(?P<attrs>(?:\s+.+)?)\]$')
ATTR_RE = re.compile(r'([A-Za-z_][A-Za-z0-9_]*)=("(?:[^"\\]|\\.)*"|\S+)')
HTML_TAG_RE = re.compile(r'<\s*[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?\s*>')

NODE_VALUE_RE = re.compile(r'^node:[A-Za-z0-9_.]+\.value$')
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
        "label": attrs.get("label", key or ""),
        "sourceYamlRefs": source_refs,
    }
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
        node["eventBinding"] = event_binding
        node["actionRef"] = attrs.get("actionRef", "")
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


def validate_ui_catalog_node(node, declared_surface, errors, path):
    allowed_kinds = set()
    if declared_surface:
        for k in dig(declared_surface, "component_catalog_refs", "componentKinds") or []:
            allowed_kinds.add(k.split(" ")[0])

    if node.get("kind") == "Field":
        control = node.get("control")
        if control and allowed_kinds and control not in allowed_kinds and not node.get("knownGapRefs"):
            errors.append(err("UNKNOWN_COMPONENT_KIND", path, "blocking", f"field control '{control}' is not a registered componentKind for this surface and carries no knownGapRef"))

    if node.get("kind") == "StyleRef":
        token = node.get("tokenRef", "")
        if token and not TOKEN_LIKE_RE.match(token) and not node.get("knownGapRefs"):
            errors.append(err("STYLE_REF_NOT_TOKEN", path, "blocking", f"styleRef tokenRef '{token}' is not a css-dictionary/topology-layout-class token and carries no knownGapRef"))


def walk_and_validate(node, lanes_def, declared_surface, errors, path="$.root"):
    validate_wiring_node(node, lanes_def, errors, path)
    validate_ui_catalog_node(node, declared_surface, errors, path)
    for i, child in enumerate(node.get("children") or []):
        walk_and_validate(child, lanes_def, declared_surface, errors, f"{path}.children[{i}]")


# ---------------------------------------------------------------------------
# output assembly
# ---------------------------------------------------------------------------

def new_output_shell():
    return {
        "normalizedInputElements": [],
        "reactSchemaCandidate": None,
        "topologyUiSeedCandidate": None,
        "exchangeReport": {},
        "validationErrors": [],
        "unresolvedGaps": [],
        "reverseTranslationBlockers": [],
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


def build_exchange_report(source_refs, emitted_count, known_gap_refs, loss_entries, reverse_blockers, validation_errors):
    return {
        "sourceSchemaId": "topolactor.translator_input.v1",
        "outputSeedSchemaId": None,
        "sourceYamlRefs": source_refs,
        "emittedRecords": emitted_count,
        "knownGapRefs": known_gap_refs,
        "lossEntries": loss_entries,
        "reverseTranslationBlockers": reverse_blockers,
        "validationErrors": validation_errors,
    }


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


def write_output(args, doc):
    text = json.dumps(doc, indent=2, ensure_ascii=False, sort_keys=False) + "\n"
    if getattr(args, "output", None):
        out_path = resolve_safe_output_path(args.output)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(text, encoding="utf-8")
    sys.stdout.write(text)


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

    vocabulary = protected_vocabulary(ssot_root)
    val_errors, mode, input_text, target_surface = validate_input_envelope(envelope, ssot_root, vocabulary)
    output["validationErrors"].extend(val_errors)

    fatal_rule_ids = {"INPUT_TEXT_EMPTY", "SOURCE_YAML_REFS_EMPTY", "INPUT_MODE_INVALID"}
    if any(e["ruleId"] in fatal_rule_ids for e in val_errors):
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
        output["validationErrors"].extend(tree_errors)

    source_refs = envelope.get("sourceYamlRefs") or []
    output["reactSchemaCandidate"] = build_react_schema_candidate(root_node, target_surface, source_refs)
    output["topologyUiSeedCandidate"] = None
    output["reverseTranslationBlockers"] = []

    unresolved_gaps = collect_known_gap_refs(root_node) if root_node is not None else []
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
    gen.set_defaults(func=cmd_generate_react_schema)

    for mode, name in (("generate_topology_ui_seed", "generate-topology-seed"), ("round_trip_check", "round-trip-check")):
        p = sub.add_parser(name)
        p.add_argument("--input", required=False)
        p.add_argument("--output")
        p.set_defaults(func=_not_implemented_handler(mode))

    return parser


def main(argv=None):
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
