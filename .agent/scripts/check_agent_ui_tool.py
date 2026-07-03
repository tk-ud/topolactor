#!/usr/bin/env python3
"""check_agent_ui_tool.py -- Agent UI tool contract gate.

This is an implementation-facing tool test. It intentionally fails until the
Agent UI tools required by docs/governance/agent-ui-protocol-ssot.yaml exist and
expose the expected contract surface.

This check must not be satisfied by documentation-only boundary text. It verifies
actual .agent/tools entrypoints for the Agent UI flow.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

REQUIRED_TOOL_ENTRYPOINTS = {
    ".agent/tools/agent-ui-initial-contract": [
        "initial_contract",
        "worktype",
        "ssot",
        "senario-tmp.md",
        "tool.log",
    ],
    ".agent/tools/agent-ui-local-test": [
        "local_test",
        "worktype",
        "senario-tmp.md",
        "checklist",
        "checks",
    ],
}

FORBIDDEN_OUTPUT_CLAIMS = (
    "implemented=True",
    "partial=True",
    "not_started=True",
    "proof_passed=True",
    "semantic_completion=True",
)

MUTATION_PROBE_ARGS = ["--output", "/tmp/topolactor-agent-ui-tool-test-should-not-write.json"]


def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)


def entrypoint_failures(path_text: str, required_help_terms: list[str]) -> list[str]:
    failures: list[str] = []
    path = REPO_ROOT / path_text

    if not path.is_file():
        return [f"required Agent UI tool entrypoint missing: {path_text}"]

    if not os.access(path, os.X_OK):
        failures.append(f"Agent UI tool entrypoint is not executable: {path_text}")

    try:
        source = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        source = ""

    if "check_agent_ui_protocol.py" in source:
        failures.append(f"{path_text}: must not delegate to documentation-only protocol guard")

    try:
        help_proc = subprocess.run(
            [str(path), "--help"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            timeout=30,
        )
    except OSError as exc:
        failures.append(f"{path_text}: --help execution failed: {exc}")
        return failures

    if help_proc.returncode != 0:
        failures.append(f"{path_text}: --help must exit 0, got {help_proc.returncode}")
        return failures

    help_text = f"{help_proc.stdout}\n{help_proc.stderr}"
    for term in required_help_terms:
        if term not in help_text:
            failures.append(f"{path_text}: --help missing required contract term: {term!r}")

    for forbidden in FORBIDDEN_OUTPUT_CLAIMS:
        if forbidden in help_text:
            failures.append(f"{path_text}: --help must not assert completion/status claim: {forbidden}")

    try:
        mutation_proc = subprocess.run(
            [str(path), *MUTATION_PROBE_ARGS],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            timeout=30,
        )
    except OSError as exc:
        failures.append(f"{path_text}: mutation probe execution failed: {exc}")
        return failures

    if mutation_proc.returncode == 0:
        failures.append(f"{path_text}: mutation-oriented --output probe must fail unless the subcommand explicitly owns the artifact")

    mutation_output = f"{mutation_proc.stdout}\n{mutation_proc.stderr}".lower()
    if "mutation" not in mutation_output and "artifact" not in mutation_output and "output" not in mutation_output:
        failures.append(f"{path_text}: mutation probe must explain mutation/artifact/output boundary")

    return failures


def main() -> int:
    os.chdir(REPO_ROOT)
    failures: list[str] = []

    for path_text, required_help_terms in REQUIRED_TOOL_ENTRYPOINTS.items():
        failures.extend(entrypoint_failures(path_text, required_help_terms))

    if failures:
        for message in failures:
            fail(message)
        print(f"=== {len(failures)} Agent UI tool contract check(s) failed ===", file=sys.stderr)
        return 1

    print(f"PASS check-agent-ui-tool.py entrypoints={len(REQUIRED_TOOL_ENTRYPOINTS)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
