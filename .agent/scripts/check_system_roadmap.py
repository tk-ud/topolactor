#!/usr/bin/env python3
"""check_system_roadmap.py -- system roadmap YAML SSOT structural + drift checks.

Python3 stdlib structured-processing counterpart of the Ruby heredoc
previously embedded in check-system-roadmap.sh.
"""
import glob
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import minimal_yaml as yaml  # noqa: E402

arr = yaml.arr


def detect_duplicate_keys(path):
    duplicates = []
    mapping_stack = [{"indent": -1, "keys": {}}]
    key_re = re.compile(r"^([A-Za-z0-9_.\-]+):(?:\s|$)")
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    for idx, line in enumerate(text.split("\n")):
        if line.strip() == "" or line.lstrip().startswith("#"):
            continue
        indent = len(line) - len(line.lstrip(" "))
        stripped = line.strip()
        while len(mapping_stack) > 1 and indent <= mapping_stack[-1]["indent"]:
            mapping_stack.pop()
        if stripped.startswith("- "):
            continue
        m = key_re.match(stripped)
        if m:
            key = m.group(1)
            scope = mapping_stack[-1]["keys"]
            if key in scope:
                duplicates.append(f"{key} (lines {scope[key]} and {idx + 1})")
            else:
                scope[key] = idx + 1
            if stripped.endswith(":"):
                mapping_stack.append({"indent": indent, "keys": {}})
    return duplicates


def statuses_for_path(path, file_statuses, dir_statuses):
    statuses = list(file_statuses.get(path, []))
    for d, entry_statuses in dir_statuses.items():
        if path.startswith(d):
            statuses.extend(entry_statuses)
    seen = []
    for s in statuses:
        if s not in seen:
            seen.append(s)
    return seen


def main():
    repo = sys.argv[1]
    roadmap_path = sys.argv[2]
    failures = []
    warnings = []

    if not os.path.exists(roadmap_path):
        failures.append("docs/system-roadmap.yaml not found")
        print("=== system roadmap check ===")
        for x in failures:
            print(f"FAIL: {x}")
        return 1

    for dup in detect_duplicate_keys(roadmap_path):
        failures.append(f"duplicate YAML key detected: {dup}")

    raw = yaml.load_file(roadmap_path) or {}
    root = raw.get("system_roadmap_ssot") if isinstance(raw.get("system_roadmap_ssot"), dict) else {}
    if not root:
        failures.append("system_roadmap_ssot must exist")
    status_terms = root.get("status_terms") if isinstance(root.get("status_terms"), dict) else {}
    if not status_terms:
        failures.append("system_roadmap_ssot.status_terms must be defined")
    allowed = list(status_terms.keys())
    impl = root.get("implementation_registry") if isinstance(root.get("implementation_registry"), dict) else {}
    if not impl:
        failures.append("implementation_registry must exist")
    ux_gates = root.get("ux_acceptance_gates") if isinstance(root.get("ux_acceptance_gates"), dict) else {}
    frontend_ux = ux_gates.get("frontend_projection_surface_general_ux") if isinstance(
        ux_gates.get("frontend_projection_surface_general_ux"), dict
    ) else {}
    if not frontend_ux:
        failures.append("ux_acceptance_gates.frontend_projection_surface_general_ux must exist")
    else:
        if frontend_ux.get("production_ready_independent") is not True:
            failures.append("frontend UX acceptance gate must stay independent from production_ready")
        criteria = frontend_ux.get("acceptance_criteria") if isinstance(frontend_ux.get("acceptance_criteria"), dict) else {}
        required_criteria = [
            "lifecycle_state_visibility", "recovery_navigation", "actionable_validation_errors",
            "progressive_disclosure_vocabulary", "non_pointer_operation", "accessibility_observability",
            "first_run_guidance", "css_token_visual_diff",
        ]
        for criterion in required_criteria:
            if criterion not in criteria:
                failures.append(f"frontend UX acceptance criterion missing: {criterion}")
        boundary = frontend_ux.get("authority_boundary") if isinstance(frontend_ux.get("authority_boundary"), dict) else {}
        frontend_only = boundary.get("frontend_projection_surface_only") if isinstance(
            boundary.get("frontend_projection_surface_only"), list
        ) else []
        if "frontend_must_not_write_DB_directly" not in frontend_only:
            failures.append("frontend UX acceptance gate must prohibit direct DB write")
        if "frontend_must_not_decide_topology_promotion_persistence_or_SQL_attention_policy" not in frontend_only:
            failures.append("frontend UX acceptance gate must prohibit frontend topology judgment")
        if "frontend_may_submit_intent_only_through_backend_preview_validate_apply_route" not in frontend_only:
            failures.append("frontend UX acceptance gate must preserve backend preview validate apply route")

    file_statuses = {}
    dir_statuses = {}
    for k, v in impl.items():
        if not isinstance(v, dict):
            failures.append(f"implementation_registry.{k} must be a map")
            continue
        st = v.get("status")
        if st not in allowed:
            failures.append(f"implementation_registry.{k}.status '{st}' is not in status_terms")
        pr = bool(v.get("production_ready"))
        evidence = v.get("evidence")
        cc = v.get("completion_condition")
        kg = v.get("known_gap_ref")
        ps = v.get("public_summary")
        ready = (st == "production_ready" or pr)
        if st == "implemented" and evidence is None and cc is None:
            failures.append(f"implemented entry {k} requires evidence or completion_condition")
        if ready and kg is not None:
            failures.append(f"production_ready entry {k} must not keep known_gap_ref")
        if (st == "implemented" or ready) and (ps is None or str(ps).strip() == ""):
            failures.append(f"{k} requires public_summary for implemented/production_ready")
        if st in ("skeleton", "partial") and kg is None and cc is None:
            failures.append(f"{k} status {st} requires known_gap_ref or completion_condition")
        for fp in arr(v.get("files")):
            if fp is None or str(fp).strip() == "":
                continue
            normalized = str(fp)
            if normalized.endswith("/"):
                dir_statuses.setdefault(normalized, []).append(st)
            else:
                file_statuses.setdefault(normalized, []).append(st)

    regex = re.compile(r"(skeleton|stub|dummy|mock|fake|pass-through|passthrough|temporary|not implemented)", re.I)
    exclude = ["/tests/", "/fixtures/", "/bin/", "/obj/", "/node_modules/", "/.git/"]
    for base in ("backend", "frontend"):
        for path in glob.glob(os.path.join(repo, base, "**", "*"), recursive=True):
            if not os.path.isfile(path):
                continue
            ext = os.path.splitext(path)[1].lower()
            if ext not in (".cs", ".ts", ".tsx", ".js", ".jsx"):
                continue
            norm = path[len(repo) + 1:] if path.startswith(repo + os.sep) else path
            norm = norm.replace(os.sep, "/")
            if any(e in norm for e in exclude):
                continue
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                txt = f.read()
            if not regex.search(txt):
                continue
            statuses = statuses_for_path(norm, file_statuses, dir_statuses)
            if any(s in ("skeleton", "partial") for s in statuses):
                warnings.append(f"marker in registered {'/'.join(statuses)} file: {norm}")
            elif any(s in ("implemented", "production_ready") for s in statuses):
                failures.append(f"marker found in {'/'.join(statuses)} file: {norm}")
            else:
                failures.append(f"marker found in unregistered file: {norm}")

    if failures:
        print("=== system roadmap check ===")
        for w in warnings:
            print(f"WARN: {w}")
        for f in failures:
            print(f"FAIL: {f}")
        print(f"=== {len(failures)} failure(s), {len(warnings)} warning(s) ===")
        return 1
    if warnings:
        sample = "; ".join(warnings[:3])
        suffix = "" if len(warnings) <= 3 else "; ..."
        print(f"PASS system-roadmap warnings={len(warnings)} sample={sample}{suffix}")
    else:
        print("PASS system-roadmap warnings=0")
    return 0


if __name__ == "__main__":
    sys.exit(main())
