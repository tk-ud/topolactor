#!/usr/bin/env python3
"""Shared helpers for the Agent UI protocol tools (initial-contract, local-test).

Distinct from .agent/scripts/agent_tools/readonly_observation.py: these tools are
Agent UI / governance-side tools. Per docs/governance/agent-ui-protocol-ssot.yaml's
mutation_boundary, they may write tool-owned governance artifacts
(.agent/tools/logs/tool.log, senario-tmp.md) because that SSOT and this task
scope explicitly require it -- but they still must not mutate Topolactor
application/runtime/product source surfaces, and they expose no arbitrary
--output file-write escape hatch.
"""
from __future__ import annotations

import json
import sys
import uuid as uuid_lib
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_DIR = REPO_ROOT / ".agent" / "scripts"
sys.path.insert(0, str(SCRIPT_DIR / "lib"))
import minimal_yaml as yaml  # noqa: E402

ROUTES_PATH = REPO_ROOT / ".agent" / "routes" / "worktype-required-protocols.yaml"
TOOL_LOG_PATH = REPO_ROOT / ".agent" / "tools" / "logs" / "tool.log"
SENARIO_TMP_PATH = REPO_ROOT / "senario-tmp.md"
YAML_SECTION_QUERY_TOOL = REPO_ROOT / ".agent" / "tools" / "yaml-section-query"

# Structured pointer to the Reference basis this tool implements. Exposed as a
# tool output field (not markdown prose) so a UI/AI consumer can trace tool
# output back to its governing Reference files without parsing markdown.
REFERENCE_BASIS = {
    "tool_output_reference": "docs/governance/reference/agent-ui-tool-output-reference.yaml",
    "senario_tmp_reference": "docs/governance/reference/agent-ui-senario-tmp-reference.yaml",
    "negative_boundary_reference": "docs/governance/reference/agent-ui-negative-boundary-reference.yaml",
}

# senario-tmp.md heading/prefix contract, per
# docs/governance/reference/agent-ui-negative-boundary-reference.yaml's
# senario_tmp_markdown block. Markdown is the human/UI-rendering surface;
# parse_senario_tmp() below is what turns it back into structured evidence.
SENARIO_TMP_TARGET_FILE_HEADING = "## 対象file名"
SENARIO_TMP_BOUNDARY_HEADING = "## NG boundary"
SENARIO_SUMMARY_PREFIX = "作業概要:"

BOUNDARY = {
    "authority_boundary": (
        "agent_ui_tool_output_not_ssot_authority_not_proof_completion_not_"
        "completion_judgment_not_semantic_completion_not_implemented_status"
    ),
    "prohibited_judgments": [
        "implemented", "partial", "not_started", "proof_passed", "semantic_completion",
    ],
    "mutation_boundary": "governance_owned_artifacts_only_not_product_runtime_source",
}

MUTATION_FLAG = "--output"


def emit_json(data) -> int:
    sys.stdout.write(json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
    return 0


def fail(message: str) -> int:
    sys.stderr.write(f"FAIL: {message}\n")
    return 1


def reject_output_flag(argv: list[str]) -> int | None:
    """Rejects the ordinary-tool --output mutation escape hatch.

    This tool's own governance-owned writes (tool.log, senario-tmp.md) are
    fixed, internally-controlled artifact paths, not an arbitrary --output
    target, so --output is never a supported flag here.
    """
    for arg in argv:
        opt = arg.split("=", 1)[0]
        if opt == MUTATION_FLAG:
            sys.stderr.write(
                "FAIL: mutation-oriented --output option is not supported by this Agent UI "
                "tool; it only writes its own fixed governance-owned artifact surfaces "
                "(.agent/tools/logs/tool.log, senario-tmp.md), never an arbitrary output "
                "file.\n"
            )
            return 2
    return None


def load_routes() -> dict:
    return yaml.load_file(str(ROUTES_PATH))


def worktypes() -> dict:
    return load_routes().get("worktypes", {}) or {}


def new_uuid() -> str:
    return str(uuid_lib.uuid4())


def now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def append_tool_log(datetime_value: str, uuid_value: str, task_name: str, worktype_value: str) -> None:
    TOOL_LOG_PATH.parent.mkdir(parents=True, exist_ok=True)
    with TOOL_LOG_PATH.open("a", encoding="utf-8") as fh:
        fh.write(f"{datetime_value} {uuid_value} {task_name} {worktype_value}\n")


def tail_lines(text: str, count: int = 20) -> list[str]:
    lines = text.splitlines()
    return lines[-count:] if len(lines) > count else lines


def parse_senario_tmp(content: str) -> dict:
    """Deterministically extracts structured contract fields from senario-tmp.md.

    The markdown file is the human/UI-rendering surface. This parser is the
    tool/runtime-side structured evidence extraction: it walks the fixed
    heading/checkbox contract (## 対象file名 / ## NG boundary) rather than
    treating markdown wording itself as the authority. Callers should surface
    these fields directly, not re-derive them ad hoc from raw text.
    """
    section: str | None = None
    target_files: list[str] = []
    senario_summary: str | None = None
    ng_boundary: str | None = None

    for raw_line in content.splitlines():
        line = raw_line.strip()
        if line == SENARIO_TMP_TARGET_FILE_HEADING:
            section = "target_file"
            continue
        if line == SENARIO_TMP_BOUNDARY_HEADING:
            section = "ng_boundary"
            continue
        if line.startswith("#"):
            section = None
            continue
        if not line.startswith("- [") or "]" not in line:
            continue

        item_text = line.split("]", 1)[1].strip()
        if section == "target_file":
            if item_text.startswith(SENARIO_SUMMARY_PREFIX):
                senario_summary = item_text[len(SENARIO_SUMMARY_PREFIX):].strip()
            elif item_text and item_text != "作業概要":
                target_files.append(item_text)
        elif section == "ng_boundary" and item_text:
            ng_boundary = item_text

    return {
        "target_file": target_files[0] if target_files else None,
        "target_files": target_files,
        "senario_summary": senario_summary,
        "ng_boundary": ng_boundary,
    }
