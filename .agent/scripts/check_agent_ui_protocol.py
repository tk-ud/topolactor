#!/usr/bin/env python3
"""check_agent_ui_protocol.py -- Agent UI protocol/tool-boundary gate.

Verifies the Agent UI protocol boundary that ordinary observation tools remain
read-only over product/runtime/source surfaces while Agent UI / governance tools
may write tool-owned evidence only when the SSOT/task scope explicitly requires
it. This check is structural only; it does not prove an Agent UI implementation
is complete and does not judge implemented / partial / not_started status.
"""
from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

REQUIRED_FILES = [
    "docs/governance/agent-ui-protocol-ssot.yaml",
    "docs/governance/reference/agent-ui-tool-output-reference.yaml",
    "docs/governance/reference/agent-ui-senario-tmp-reference.yaml",
    "docs/governance/reference/agent-ui-negative-boundary-reference.yaml",
    "docs/governance/logs/tool.log",
    ".agent/rules/rule.md",
    ".agent/tools/README.md",
    ".gitignore",
]

REQUIRED_TERMS = {
    ".agent/tools/README.md": [
        "Agent-facing repository tool surface",
        "Existing observation tools",
        "Tool-side or governance-side writes are different",
        "applicable SSOT and task scope explicitly require it",
        "ordinary mutation of Topolactor product/runtime/source implementation surfaces",
        "Tool output is observation data only",
    ],
    ".agent/rules/rule.md": [
        "Do not bypass existing `.agent/tools` when an existing tool fits the work.",
        "Tool-side and governance-side writes",
        "explicit SSOT/task scope",
        "product/runtime/source",
    ],
    "docs/governance/agent-ui-protocol-ssot.yaml": [
        "mutation_boundary",
        "allowed_in_agent_ui_scope",
        ".agent/tools implementation files",
        "docs/governance/logs/tool.log",
        "senario-tmp.md",
        "tool_generated",
    ],
    "docs/governance/reference/agent-ui-tool-output-reference.yaml": [
        "authority_split",
        "tool_generated",
        "AI-authored tool.log records",
        "Append only",
        "owner: human",
        "docs/governance/logs/tool.log",
    ],
    "docs/governance/reference/agent-ui-senario-tmp-reference.yaml": [
        "senario-tmp.md",
        "gitignore_required: true",
        "created_by:",
    ],
    ".gitignore": [
        "senario-tmp.md",
    ],
}

FORBIDDEN_TERMS = {
    ".agent/tools/README.md": [
        "Tools in this directory must not mutate repository files",
        "must not mutate repository files, edit TODO/roadmap/SSOT/proof manifests",
    ],
    ".agent/rules/rule.md": [
        "Do not use `.agent/tools` for repository mutation; `.agent/tools` is an Agent-facing read-only repo observation surface",
    ],
}


def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)


def read_text(path: str) -> str | None:
    full_path = REPO_ROOT / path
    if not full_path.is_file():
        return None
    return full_path.read_text(encoding="utf-8")


def missing_file_failures() -> list[str]:
    failures: list[str] = []
    for path in REQUIRED_FILES:
        if not (REPO_ROOT / path).is_file():
            failures.append(f"required file missing: {path}")
    return failures


def required_term_failures() -> list[str]:
    failures: list[str] = []
    for path, terms in REQUIRED_TERMS.items():
        text = read_text(path)
        if text is None:
            failures.append(f"term check skipped because file is missing: {path}")
            continue
        for term in terms:
            if term not in text:
                failures.append(f"term not found in {path}: {term!r}")
    return failures


def forbidden_term_failures() -> list[str]:
    failures: list[str] = []
    for path, terms in FORBIDDEN_TERMS.items():
        text = read_text(path)
        if text is None:
            failures.append(f"forbidden-term check skipped because file is missing: {path}")
            continue
        for term in terms:
            if term in text:
                failures.append(f"forbidden old boundary term found in {path}: {term!r}")
    return failures


def main() -> int:
    failures: list[str] = []
    failures.extend(missing_file_failures())
    failures.extend(required_term_failures())
    failures.extend(forbidden_term_failures())

    if failures:
        for message in failures:
            fail(message)
        print(f"=== {len(failures)} Agent UI protocol boundary check(s) failed ===", file=sys.stderr)
        return 1

    assertion_count = len(REQUIRED_FILES) + sum(len(v) for v in REQUIRED_TERMS.values()) + sum(len(v) for v in FORBIDDEN_TERMS.values())
    print(f"PASS check-agent-ui-protocol.py assertions={assertion_count}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
