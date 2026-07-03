#!/usr/bin/env python3
"""agent_ui_local_test -- local_test step of the Agent UI protocol.

Implements docs/governance/agent-ui-protocol-ssot.yaml's
agent_protocols.flow_order.local_test: run worktype-routed tests, output
senario-tmp.md for checklist interview, list the existing checklist
interview items, run required checks, and summarize pass/fail. Raw
execution logs are not streamed into Agent context -- only a bounded tail
and pass/fail per check.

Tool output is compact structured data only. It is not SSOT authority, proof
completion, semantic completion, or implemented/partial/not_started judgment.
"""
from __future__ import annotations

import argparse
import subprocess
import sys

from agent_ui_common import (
    BOUNDARY,
    REPO_ROOT,
    SENARIO_TMP_PATH,
    emit_json,
    fail,
    reject_output_flag,
    tail_lines,
    worktypes,
)

TOOL_NAME = "agent-ui-local-test"
STRUCTURE_CHECK = ".agent/tests/check-structure.sh"

CHECKLIST_FILES = [
    ".agent/checklists/policy-judgment.md",
    ".agent/checklists/boundary-identity.md",
]


def _run_check(rel_path: str) -> dict:
    proc = subprocess.run(
        ["bash", str(REPO_ROOT / rel_path)],
        cwd=REPO_ROOT, capture_output=True, text=True, timeout=600,
    )
    combined = f"{proc.stdout}\n{proc.stderr}"
    return {
        "check": rel_path,
        "pass": proc.returncode == 0,
        "exit_code": proc.returncode,
        "tail": tail_lines(combined, 20),
    }


def _cmd_run_worktype_tests(args: argparse.Namespace) -> int:
    routes = worktypes()
    if args.worktype not in routes:
        sys.stderr.write(
            f"FAIL: unknown worktype {args.worktype!r}; choose one of: {', '.join(sorted(routes))}\n"
        )
        return 2
    required_checks = routes[args.worktype].get("required_checks", []) or []
    results = [_run_check(c) for c in required_checks]
    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "run_worktype_tests",
        "worktype": args.worktype,
        "worktype_test_commands": required_checks,
        "worktype_test_result": results,
        "worktype_test_pass": all(r["pass"] for r in results) if results else False,
    })


def _cmd_read_senario_tmp(_args: argparse.Namespace) -> int:
    if not SENARIO_TMP_PATH.is_file():
        sys.stderr.write(
            "FAIL: senario-tmp.md cannot be checked (missing); checklist interview must not "
            "proceed until an initial-contract 'end' step creates it.\n"
        )
        return 1
    content = SENARIO_TMP_PATH.read_text(encoding="utf-8")
    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "read_senario_tmp",
        "senario_tmp_path": str(SENARIO_TMP_PATH.relative_to(REPO_ROOT)),
        "senario_tmp_content": content,
    })


def _checklist_items(path: str) -> list[str]:
    full = REPO_ROOT / path
    if not full.is_file():
        return []
    return [
        line.strip("# ").strip()
        for line in full.read_text(encoding="utf-8").splitlines()
        if line.startswith("## ")
    ]


def _cmd_checklist(args: argparse.Namespace) -> int:
    files = args.files.split(",") if args.files else CHECKLIST_FILES
    items = {f: _checklist_items(f) for f in files}
    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "checklist",
        "checklist_interview_items": items,
        "checklist_result": "requires_ai_interview_using_listed_items",
        "note": "This tool lists existing checklist items only; it does not evaluate free-form checklist answers.",
    })


def _cmd_checks(_args: argparse.Namespace) -> int:
    result = _run_check(STRUCTURE_CHECK)
    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "checks",
        "check_commands": [STRUCTURE_CHECK],
        "check_result": result,
    })


def _cmd_summary(args: argparse.Namespace) -> int:
    routes = worktypes()
    if args.worktype not in routes:
        return fail(f"unknown worktype {args.worktype!r}; choose one of: {', '.join(sorted(routes))}")

    required_checks = routes[args.worktype].get("required_checks", []) or []
    worktype_results = [_run_check(c) for c in required_checks]
    check_result = _run_check(STRUCTURE_CHECK)

    missing_evidence: list[str] = []
    scenario_contract_summary = None
    senario_tmp_path = None
    if not SENARIO_TMP_PATH.is_file():
        missing_evidence.append("senario-tmp.md missing")
    else:
        senario_tmp_path = str(SENARIO_TMP_PATH.relative_to(REPO_ROOT))
        for line in SENARIO_TMP_PATH.read_text(encoding="utf-8").splitlines():
            stripped = line.strip()
            if stripped.startswith("- [x] 作業概要:"):
                scenario_contract_summary = stripped.split(":", 1)[1].strip()
                break
    if not all(r["pass"] for r in worktype_results):
        missing_evidence.append("one or more worktype-routed checks failed")
    if not check_result["pass"]:
        missing_evidence.append("check-structure.sh failed")

    pass_or_fail = "pass" if not missing_evidence else "fail"

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "summary",
        "datetime": args.datetime,
        "uuid": args.uuid,
        "task_name": args.task_name,
        "worktype": args.worktype,
        "worktype_test_result": worktype_results,
        "scenario_contract_summary": scenario_contract_summary,
        "senario_tmp_path": senario_tmp_path,
        "checklist_result": "requires_ai_interview_using_listed_items",
        "check_result": check_result,
        "missing_evidence_if_any": missing_evidence,
        "pass_or_fail": pass_or_fail,
        "completion_summary_note": "pass_or_fail reflects required-check pass only; it is not an implemented/partial/not_started judgment.",
    })


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="agent-ui-local-test",
        description=(
            "local_test step of the Agent UI protocol: run worktype-routed tests, "
            "output senario-tmp.md for checklist interview, list checklist items, "
            "run required checks, and summarize pass/fail. See "
            "docs/governance/agent-ui-protocol-ssot.yaml."
        ),
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_run = sub.add_parser("run-worktype-tests", help="Run the required_checks routed for a worktype and summarize pass/fail.")
    p_run.add_argument("--worktype", required=True)
    p_run.set_defaults(func=_cmd_run_worktype_tests)

    sub.add_parser("read-senario-tmp", help="Output senario-tmp.md, or Error if it cannot be checked.").set_defaults(func=_cmd_read_senario_tmp)

    p_checklist = sub.add_parser("checklist", help="List existing checklist interview items (policy-judgment, boundary-identity).")
    p_checklist.add_argument("--files", default="", help="Comma-separated checklist file paths (default: policy-judgment.md,boundary-identity.md)")
    p_checklist.set_defaults(func=_cmd_checklist)

    sub.add_parser("checks", help=f"Run {STRUCTURE_CHECK} and summarize pass/fail without raw log dump.").set_defaults(func=_cmd_checks)

    p_summary = sub.add_parser("summary", help="Run worktype tests + checks, and emit pass_or_fail completion summary.")
    p_summary.add_argument("--task-name", required=True)
    p_summary.add_argument("--worktype", required=True)
    p_summary.add_argument("--uuid", required=True, help="uuid value emitted by initial-contract 'start'; must not be hand-authored")
    p_summary.add_argument("--datetime", required=True, help="datetime value emitted by initial-contract 'start'; must not be hand-authored")
    p_summary.set_defaults(func=_cmd_summary)

    return parser


def main(argv: list[str] | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    rejected = reject_output_flag(argv)
    if rejected is not None:
        return rejected
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
