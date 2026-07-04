#!/usr/bin/env python3
"""agent_ui_initial_contract -- initial_contract step of the Agent UI protocol.

Implements docs/governance/agent-ui-protocol-ssot.yaml's
agent_protocols.flow_order.initial_contract as a stateless, multi-invocation
CLI (mirroring the existing topology-seed-discussion tool's step-by-step
model): task_name/worktype input -> worktype prompt guidance + required/
triggered protocol excerpts + workflow procedure order -> target SSOT
resolution -> section listing/selection -> senario-tmp.md + tool.log on quit.

`start` absorbs the reads an agent would otherwise have to open
`.agent/protocols/*` and `.agent/skills/agent-workflow.md` separately for:
once a worktype/trigger has narrowed which prompt/protocol files apply, it
returns those specific files' full content (not every worktype's files --
that breadth guard is unchanged) so no separate manual open is needed. It
does not itself judge whether a triggered_protocols condition applies to
the current task -- that stays agent judgment.

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
    REFERENCE_BASIS,
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
- [x] {target_file}
- [x] 作業概要: {senario_summary}

# negative cases
## NG boundary
- [x] {ng_boundary}
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
        "next_step": "Run `start --task-name <name> --worktype <one of the worktype ids above>` to begin initial_contract.",
    })


FULL_READ_LINE_CAP = 500


def _read_full(path_text: str | None) -> tuple[list[str] | None, bool]:
    """Full content of a repo-relative file already narrowed by worktype/trigger routing.

    This is not the "read every prompt/protocol/skill" breadth this tool exists to
    avoid -- the caller has already resolved exactly which file(s) apply to the
    current worktype/trigger, so returning that file's full text is a targeted
    read, not a dump. FULL_READ_LINE_CAP is a fail-explicit safety bound only (the
    largest current .agent/protocols file is well under it): if a file exceeds it,
    the return is truncated and the second tuple element is True so callers/agents
    never silently get a partial read without knowing it.

    Returns (None, False) when path_text is unset or the file does not exist on
    disk -- distinct from ([], False), an empty-but-present file -- so a missing
    routed file (a routes.yaml entry pointing at a path that doesn't exist) never
    reads the same as "this file has no content"; callers must fail closed on
    None rather than silently emitting empty content (rule.md: no silent fallback).
    """
    if not path_text:
        return None, False
    file_path = REPO_ROOT / path_text
    if not file_path.is_file():
        return None, False
    lines = file_path.read_text(encoding="utf-8").splitlines()
    if len(lines) > FULL_READ_LINE_CAP:
        return lines[:FULL_READ_LINE_CAP], True
    return lines, False


def _extract_fenced_block_after_heading(text: str, heading: str) -> list[str]:
    """Extracts the fenced code block immediately following a markdown heading line."""
    collecting = False
    in_block = False
    result: list[str] = []
    for line in text.splitlines():
        stripped = line.strip()
        if not collecting and stripped == heading:
            collecting = True
            continue
        if collecting and not in_block:
            if stripped.startswith("```"):
                in_block = True
            continue
        if collecting and in_block:
            if stripped.startswith("```"):
                break
            result.append(line)
    return result


WORKFLOW_SKILL_PATH = ".agent/skills/agent-workflow.md"


def _cmd_start(args: argparse.Namespace) -> int:
    routes = worktypes()
    if args.worktype not in routes:
        sys.stderr.write(
            f"FAIL: unknown worktype {args.worktype!r}; choose one of: {', '.join(sorted(routes))}\n"
        )
        return 2

    route = routes[args.worktype]
    prompt_path = route.get("prompt")
    missing_paths: list[str] = []

    prompt_content, prompt_truncated = _read_full(prompt_path)
    if prompt_content is None:
        missing_paths.append(prompt_path or "<route.prompt unset in worktype-required-protocols.yaml>")

    protocol_trigger_hints: list[dict] = []
    for path in route.get("required_protocols", []) or []:
        content, truncated = _read_full(path)
        if content is None:
            missing_paths.append(path)
        protocol_trigger_hints.append({
            "path": path,
            "trigger_condition": "always",
            "content": content or [],
            "truncated": truncated,
        })
    triggered = route.get("triggered_protocols") or {}
    if isinstance(triggered, dict):
        for condition, paths in sorted(triggered.items()):
            for path in paths or []:
                content, truncated = _read_full(path)
                if content is None:
                    missing_paths.append(path)
                protocol_trigger_hints.append({
                    "path": path,
                    "trigger_condition": condition,
                    "content": content or [],
                    "truncated": truncated,
                })

    workflow_file = REPO_ROOT / WORKFLOW_SKILL_PATH
    workflow_procedure: list[str] = []
    if not workflow_file.is_file():
        missing_paths.append(WORKFLOW_SKILL_PATH)
    else:
        workflow_procedure = _extract_fenced_block_after_heading(
            workflow_file.read_text(encoding="utf-8"), "## Execution Order"
        )
        if not workflow_procedure:
            missing_paths.append(f"{WORKFLOW_SKILL_PATH}#Execution Order (heading or fenced block not found)")

    if missing_paths:
        emit_json({
            "tool": TOOL_NAME,
            "boundary": BOUNDARY,
            "mode": "start",
            "read_status": "missing",
            "missing_path": missing_paths,
            "start_contract_status": "failed",
            "start_contract_status_note": (
                "start requires every routed prompt/protocol file and the workflow skill's "
                "Execution Order block to actually be readable; a missing one is a routing/"
                "repo integrity defect, not something to silently paper over with empty content."
            ),
            "reference_basis": REFERENCE_BASIS,
        })
        return 3

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
        "prompt_content": prompt_content,
        "prompt_content_truncated": prompt_truncated,
        "required_reads_from_prompt": ["AGENTS.md", ".agent/rules/rule.md", prompt_path],
        "protocol_trigger_hints": protocol_trigger_hints,
        "protocol_trigger_hints_note": "each entry's content is that single file's full text (already narrowed to this worktype/trigger, not every protocol) -- reading it is not judgment; whether a non-'always' trigger_condition applies to the current task is still the agent's call.",
        "workflow_procedure_path": WORKFLOW_SKILL_PATH,
        "workflow_procedure": workflow_procedure,
        "reference_basis": REFERENCE_BASIS,
        "next_step": "Read prompt_content/protocol_trigger_hints above for the target SSOT name(s) to check, then run `resolve-ssot --target <ssot_name>` for each.",
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
            "next_step": "Retry `resolve-ssot` with a corrected --target name (check protocol_trigger_hints/prompt_content from `start` for the exact SSOT name).",
        })
    if len(matches) > 1:
        return emit_json({
            "tool": TOOL_NAME,
            "boundary": BOUNDARY,
            "mode": "resolve_ssot",
            "target_ssot_name": args.target,
            "ssot_resolution_status": "ambiguous",
            "candidates": [str(m.relative_to(REPO_ROOT)) for m in matches],
            "next_step": "Retry `resolve-ssot` with --target set to one of the candidates above.",
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
        "next_step": (
            f"Run `sections --file {rel_path} --select '[\"section_a\",...]'` using names from "
            "section_list above; repeat resolve-ssot/sections for any other target SSOT; then "
            "call `end` once the senario contract is ready."
        ),
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
        "next_step": (
            "Call `sections` again for other files/sections still needed, otherwise run "
            "`end --task-name ... --worktype ... --uuid ... --datetime ... --target-file ... "
            "--senario-summary ...` (reuse uuid/datetime from `start`) to close out initial_contract."
        ),
    })


def _cmd_end(args: argparse.Namespace) -> int:
    if not args.senario_summary.strip():
        return fail("senario contract is required on quit: --senario-summary must not be empty")
    if not args.target_file.strip():
        return fail("senario contract is required on quit: --target-file must not be empty")

    content = SENARIO_TMP_TEMPLATE.format(
        target_file=args.target_file.strip(),
        senario_summary=args.senario_summary.strip(),
        ng_boundary=(args.ng_boundary or "N/A").strip(),
    )
    SENARIO_TMP_PATH.write_text(content, encoding="utf-8")

    append_tool_log(args.datetime, args.uuid, args.task_name, args.worktype)

    return emit_json({
        "tool": TOOL_NAME,
        "boundary": BOUNDARY,
        "mode": "end",
        "reference_basis": REFERENCE_BASIS,
        "initial_contract_summary": {
            "datetime": args.datetime,
            "uuid": args.uuid,
            "task_name": args.task_name,
            "worktype": args.worktype,
            "selected_ssot_sections": args.selected_sections.split(",") if args.selected_sections else [],
            "target_file": args.target_file.strip(),
            "senario_summary": args.senario_summary.strip(),
            "ng_boundary": (args.ng_boundary or "N/A").strip(),
            "senario_tmp_path": str(SENARIO_TMP_PATH.relative_to(REPO_ROOT)),
            "tool_log_appended": True,
        },
        "next_step": (
            "initial_contract is closed. Implement within the defined scope from prompt_content/"
            "protocol_trigger_hints and the senario contract above, then run agent-ui-local-test's "
            f"`run-worktype-tests --worktype {args.worktype}` to begin local_test."
        ),
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
