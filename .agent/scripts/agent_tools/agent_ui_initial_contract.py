#!/usr/bin/env python3
"""agent_ui_initial_contract -- initial_contract step of the Agent UI protocol.

Implements docs/governance/agent-ui-protocol-ssot.yaml's
agent_protocols.flow_order.initial_contract as a stateless, multi-invocation
CLI (mirroring the existing topology-seed-discussion tool's step-by-step
model): task_name/worktype input -> worktype prompt guidance -> target SSOT
resolution -> section listing/selection -> senario-tmp.md + tool.log on quit.

AI supplies task_name, worktype selection, target SSOT name, section
selection, and senario content. This tool generates uuid/datetime/worktype
metadata and owns the docs/governance/logs/tool.log append -- the AI must
copy those tool-generated values forward between steps rather than
hand-authoring them (docs/governance/reference/agent-ui-tool-output-reference.yaml
authority_split).

Tool output is compact structured data only. It is not SSOT authority, proof
completion, semantic completion, or implemented/partial/not_started judgment.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

from agent_ui_common import (
    BOUNDARY,
    REPO_ROOT,
    SENARIO_TMP_PATH,
    YAML_SECTION_QUERY_TOOL,
    append_tool_log,
    emit_json,
    fail,
    new_uuid,
    now_iso,
    reject_output_flag,
    worktypes,
)

SENARIO_TMP_TEMPLATE = """# contracts
## 対象file名
- [ ] {target_file}

# negative cases
## NG boundary
- [ ] {ng_boundary}
"""

TOOL_NAME = "agent-ui-initial-contract"


def _cmd_worktypes(_args: argparse.Namespace) -> int:
    entries = [
        {"worktype": name, "prompt": info.get("prompt"), "required_checks": info.get("required_checks", [])}
        for name, info in worktypes().items()
    ]
    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "worktypes",
        "worktypes": entries,
    })


def _cmd_start(args: argparse.Namespace) -> int:
    routes = worktypes()
    if args.worktype not in routes:
        sys.stderr.write(
            f"FAIL: unknown worktype {args.worktype!r}; choose one of: {', '.join(sorted(routes))}\n"
        )
        return 2

    route = routes[args.worktype]
    prompt_path = route.get("prompt")
    excerpt_lines: list[str] = []
    if prompt_path:
        prompt_file = REPO_ROOT / prompt_path
        if prompt_file.is_file():
            for line in prompt_file.read_text(encoding="utf-8").splitlines():
                if line.strip():
                    excerpt_lines.append(line)
                if len(excerpt_lines) >= 12:
                    break

    protocol_trigger_hints = list(route.get("required_protocols", []) or [])
    triggered = route.get("triggered_protocols") or {}
    if isinstance(triggered, dict):
        protocol_trigger_hints.extend(sorted(triggered.keys()))

    usage_metadata = {
        "uuid": new_uuid(),
        "datetime": now_iso(),
        "task_name": args.task_name,
        "worktype": args.worktype,
    }

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "start",
        "usage_metadata": usage_metadata,
        "usage_metadata_note": "tool_generated: uuid/datetime/worktype. ai_authored: task_name. Reuse these values verbatim in later steps; do not hand-author them.",
        "worktype_prompt_path": prompt_path,
        "selected_prompt_excerpt": excerpt_lines,
        "required_reads_from_prompt": ["AGENTS.md", ".agent/rules/rule.md", prompt_path],
        "protocol_trigger_hints": protocol_trigger_hints,
    })


def _resolve_ssot_path(target: str) -> list[Path]:
    target_path = Path(target)
    if target_path.is_absolute():
        candidate = target_path
        return [candidate] if candidate.is_file() else []
    direct = REPO_ROOT / target
    if direct.is_file():
        return [direct]

    name = target_path.name
    matches: list[Path] = []
    for base in (REPO_ROOT / "docs", REPO_ROOT / ".agent" / "docs"):
        if not base.is_dir():
            continue
        for candidate in base.rglob(name):
            if candidate.is_file():
                matches.append(candidate)
        if not any(str(m).endswith(name) for m in matches):
            for candidate in base.rglob(f"{name}.yaml"):
                matches.append(candidate)
    return matches


def _cmd_resolve_ssot(args: argparse.Namespace) -> int:
    matches = _resolve_ssot_path(args.target)
    if not matches:
        return emit_json({
            "tool": TOOL_NAME,
            "boundary": BOUNDARY,
            "mode": "resolve_ssot",
            "target_ssot_name": args.target,
            "ssot_resolution_status": "not_found",
            "target_ssot_path": None,
            "section_list": [],
        })
    if len(matches) > 1:
        return emit_json({
            "tool": TOOL_NAME,
            "boundary": BOUNDARY,
            "mode": "resolve_ssot",
            "target_ssot_name": args.target,
            "ssot_resolution_status": "ambiguous",
            "candidates": [str(m.relative_to(REPO_ROOT)) for m in matches],
        })

    rel_path = str(matches[0].relative_to(REPO_ROOT))
    section_list = None
    if rel_path.endswith((".yaml", ".yml")):
        proc = subprocess.run(
            [str(YAML_SECTION_QUERY_TOOL), "--file", rel_path, "--list-sections"],
            cwd=REPO_ROOT, capture_output=True, text=True, timeout=30,
        )
        try:
            section_list = json.loads(proc.stdout).get("sections")
        except (json.JSONDecodeError, AttributeError):
            section_list = None

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "resolve_ssot",
        "target_ssot_name": args.target,
        "ssot_resolution_status": "resolved",
        "target_ssot_path": rel_path,
        "section_list": section_list,
    })


def _cmd_sections(args: argparse.Namespace) -> int:
    try:
        selected = json.loads(args.select)
    except json.JSONDecodeError as exc:
        return fail(f"--select must be a JSON array like '[\"section_a\",\"section_b\"]': {exc}")
    if not isinstance(selected, list) or not selected:
        return fail("--select must be a non-empty JSON array of section names")

    subtrees = []
    for key in selected:
        proc = subprocess.run(
            [str(YAML_SECTION_QUERY_TOOL), "--file", args.file, "--section", str(key)],
            cwd=REPO_ROOT, capture_output=True, text=True, timeout=30,
        )
        try:
            parsed = json.loads(proc.stdout)
        except json.JSONDecodeError:
            parsed = {"mode": "error", "error": "unparseable_yaml_section_query_output", "raw": proc.stdout}
        subtrees.append({"requested_section": key, "result": parsed})

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "sections",
        "file": args.file,
        "selected_section_subtrees": subtrees,
    })


def _cmd_end(args: argparse.Namespace) -> int:
    if not args.senario_summary.strip():
        return fail("senario contract is required on quit: --senario-summary must not be empty")
    if not args.target_file.strip():
        return fail("senario contract is required on quit: --target-file must not be empty")

    content = SENARIO_TMP_TEMPLATE.format(
        target_file=args.target_file.strip(),
        ng_boundary=(args.ng_boundary or "N/A").strip(),
    )
    SENARIO_TMP_PATH.write_text(content, encoding="utf-8")

    append_tool_log(args.datetime, args.uuid, args.task_name, args.worktype)

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "end",
        "initial_contract_summary": {
            "datetime": args.datetime,
            "uuid": args.uuid,
            "task_name": args.task_name,
            "worktype": args.worktype,
            "selected_ssot_sections": args.selected_sections.split(",") if args.selected_sections else [],
            "senario_tmp_path": str(SENARIO_TMP_PATH.relative_to(REPO_ROOT)),
            "tool_log_appended": True,
        },
    })


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="agent-ui-initial-contract",
        description=(
            "initial_contract step of the Agent UI protocol: task_name/worktype "
            "input, target ssot section selection, and senario-tmp.md + tool.log "
            "creation on quit. See docs/governance/agent-ui-protocol-ssot.yaml."
        ),
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("worktypes", help="List canonical worktype ids and their routed prompt/checks.").set_defaults(func=_cmd_worktypes)

    p_start = sub.add_parser("start", help="Resolve worktype metadata and emit usage_metadata (tool-generated uuid/datetime).")
    p_start.add_argument("--task-name", required=True)
    p_start.add_argument("--worktype", required=True)
    p_start.set_defaults(func=_cmd_start)

    p_ssot = sub.add_parser("resolve-ssot", help="Resolve a target ssot name to a repo-relative path and list its top-level sections.")
    p_ssot.add_argument("--target", required=True)
    p_ssot.set_defaults(func=_cmd_resolve_ssot)

    p_sections = sub.add_parser("sections", help="Output only the selected ssot section subtrees.")
    p_sections.add_argument("--file", required=True)
    p_sections.add_argument("--select", required=True, help='JSON array of section names, e.g. \'["section_a","section_b"]\'')
    p_sections.set_defaults(func=_cmd_sections)

    p_end = sub.add_parser("end", help="Require senario contract, write senario-tmp.md, append docs/governance/logs/tool.log.")
    p_end.add_argument("--task-name", required=True)
    p_end.add_argument("--worktype", required=True)
    p_end.add_argument("--uuid", required=True, help="uuid value emitted by a prior 'start' call; must not be hand-authored")
    p_end.add_argument("--datetime", required=True, help="datetime value emitted by a prior 'start' call; must not be hand-authored")
    p_end.add_argument("--selected-sections", default="", help="Comma-separated selected ssot section names")
    p_end.add_argument("--target-file", required=True, help="senario-tmp.md 対象file名 contract field")
    p_end.add_argument("--senario-summary", required=True, help="senario-tmp.md 作業概要 contract field")
    p_end.add_argument("--ng-boundary", default="", help="senario-tmp.md NG boundary contract field")
    p_end.set_defaults(func=_cmd_end)

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
