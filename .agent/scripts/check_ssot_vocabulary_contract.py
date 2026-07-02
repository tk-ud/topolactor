#!/usr/bin/env python3
"""check_ssot_vocabulary_contract.py -- SSOT vocabulary subset checks.

Python3 stdlib structured-processing counterpart of the Ruby heredoc
previously embedded in check-ssot-vocabulary-contract.sh. Repo governance
tooling structured processing (YAML/regex extraction, set operations) must
use Python3 stdlib only; Ruby is prohibited for repo governance tooling
(see docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
"""
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import minimal_yaml as yaml  # noqa: E402

ROOT = os.getcwd()


def fail(msg):
    sys.stderr.write(f"FAIL: {msg}\n")
    sys.exit(1)


def req(h, *keys):
    cur = h
    for k in keys:
        if not isinstance(cur, dict) or k not in cur:
            fail("missing SSOT path: " + "/".join(keys))
        cur = cur[k]
    return cur


def read(path):
    with open(os.path.join(ROOT, path), "r", encoding="utf-8") as f:
        return f.read()


def scan_all(pattern, rows, flags=0):
    out = set()
    for r in rows:
        out.update(re.findall(pattern, r, flags))
    return out


def main():
    comp = yaml.load_file(os.path.join(ROOT, "docs/design/component-catalog-classification-ssot.yaml"))
    pipe = yaml.load_file(os.path.join(ROOT, "docs/design/pipeline-continuity-ssot.yaml"))
    runtime = yaml.load_file(os.path.join(ROOT, "docs/design/runtime-orchestration-ssot.yaml"))
    sysci = yaml.load_file(os.path.join(ROOT, "docs/design/system-ci-admin-runtime-callable-surface.yaml"))

    catalog = read("frontend/components/catalog.ts")
    cap = set()
    for block in re.findall(r"capabilityTags:\s*\[([^\]]+)\]", catalog, re.S):
        cap.update(re.findall(r'"([^"]+)"', block))
    fields = {
        "componentFamily": set(re.findall(r'componentFamily:\s*"([^"]+)"', catalog)),
        "semanticRole": set(re.findall(r'semanticRole:\s*"([^"]+)"', catalog)),
        "visualRole": set(re.findall(r'visualRole:\s*"([^"]+)"', catalog)),
        "lifecycleStatus": set(re.findall(r'lifecycleStatus:\s*"([^"]+)"', catalog)),
        "capabilityTags": cap,
    }
    allowed = {
        "componentFamily": set(str(x) for x in req(comp, "component_family")),
        "semanticRole": set(str(x) for x in req(comp, "semantic_role")),
        "visualRole": set(str(x) for x in req(comp, "visual_role")),
        "lifecycleStatus": set(str(x) for x in req(comp, "lifecycle_status")),
        "capabilityTags": set(str(x) for x in req(comp, "capability_tags")),
    }
    for k, v in fields.items():
        if not v:
            fail(f"empty extraction: catalog {k}")
        bad = v - allowed[k]
        if bad:
            fail(f"catalog {k} not in SSOT: " + ", ".join(sorted(bad)))

    ss = req(sysci, "system_ci_admin_runtime_callable_surface_ssot")
    ops = set(x["operation"] for x in req(ss, "allowed_operations"))
    if not ops:
        fail("allowed_operations empty")
    admin = read("backend/runtime/AdminRuntime.cs")
    for op in ops:
        if f'"{op}"' not in admin:
            fail(f"AdminRuntime missing allowed operation: {op}")
    targets = set(str(x) for x in req(ss, "initial_target_vocabulary", "values"))
    if not targets:
        fail("initial_target_vocabulary empty")
    sysop = read("backend/runtime/SystemOperationCiRuntime.cs")
    for t in targets:
        if f'"{t}"' not in sysop:
            fail(f"SystemOperationCiRuntime missing target: {t}")
    tests = read("backend/tests/Topolactor.Runtime.Tests/SystemCiAdminRuntimeTests.cs")
    if '"system_ci", "list_targets"' not in tests:
        fail("SystemCiAdminRuntimeTests missing list_targets semantic path")
    if '"system_ci", "inspect"' not in tests:
        fail("SystemCiAdminRuntimeTests missing inspect semantic path")
    statuses = set(str(x) for x in req(ss, "output_contract", "current_status_values"))
    if not statuses:
        fail("status values empty")
    contracts = read("backend/schema/SystemCiContracts.cs")
    for st in statuses:
        if st not in contracts:
            fail(f"SystemCiStatus missing value: {st}")

    lanes = req(pipe, "pipeline_continuity_ssot", "lanes")
    if not lanes:
        fail("pipeline lanes empty")
    required_ids = set()
    prohibited = set()
    events = set()
    for lv in lanes.values():
        if not isinstance(lv, dict):
            continue
        required_ids.update(str(x) for x in yaml.arr(lv.get("required_identity")))
        prohibited.update(str(x) for x in yaml.arr(lv.get("prohibited")))
        events.update(str(x) for x in yaml.arr(lv.get("event_types")))
        for sub in ("persistence_policy", "transport_selection_policy"):
            prohibited.update(str(x) for x in yaml.arr(yaml.dig(lv, sub, "prohibited")))
    if not required_ids:
        fail("pipeline required_identity empty extraction")
    if not prohibited:
        fail("pipeline prohibited empty extraction")
    if not events:
        fail("pipeline event_types empty extraction")
    chk = read(".agent/tests/check-pipeline-continuity.sh")
    for tk in ("componentId_drift", "silent_fallback_for_malformed_component_definition_payload", "boolean_coercion"):
        if tk not in chk:
            fail(f"check-pipeline-continuity.sh missing token: {tk}")
        if tk not in prohibited:
            fail(f"token not in SSOT lane prohibited: {tk}")

    rv = req(runtime, "runtime_orchestration_ssot", "runtime_vocabulary_contract")
    map_types = set(str(x) for x in yaml.arr(req(rv, "mapping_types")))
    rtd = set(str(x) for x in yaml.arr(req(rv, "backend_runtime_destinations")))
    if not map_types:
        fail("runtime mapping_types empty")
    if not rtd:
        fail("runtime destinations empty")
    seed = read("db/seed_empty.sql") + "\n" + read("db/demo_seed.sql")
    seed_dests = set(re.findall(r'"runtime_destination"\s*:\s*"([a-z_]+)"', seed))
    seed_types = set(re.findall(r'"type"\s*:\s*"([a-z_]+)"\s*,\s*"runtime_destination"', seed))
    if not seed_dests:
        fail("seed runtime_destination extraction empty")
    if not seed_types:
        fail("seed mapping type extraction empty")
    bad = seed_dests - rtd
    if bad:
        fail("seed runtime_destination not in SSOT: " + ", ".join(sorted(bad)))
    bad = seed_types - map_types
    if bad:
        fail("seed mapping type not in SSOT: " + ", ".join(sorted(bad)))

    seed_class_rows = re.findall(r'"classification"\s*:\s*\{([^}]*)\}', seed, re.S)
    if not seed_class_rows:
        fail("components_bucket classification extraction empty from seed SQL")

    seed_component_family = scan_all(r'"componentFamily"\s*:\s*"([^"]+)"', seed_class_rows)
    seed_semantic_role = scan_all(r'"semanticRole"\s*:\s*"([^"]+)"', seed_class_rows)
    seed_visual_role = scan_all(r'"visualRole"\s*:\s*"([^"]+)"', seed_class_rows)
    seed_lifecycle_status = scan_all(r'"lifecycleStatus"\s*:\s*"([^"]+)"', seed_class_rows)
    seed_capability_tags = set()
    for r in seed_class_rows:
        for arr_block in re.findall(r'"capabilityTags"\s*:\s*\[([^\]]+)\]', r, re.S):
            seed_capability_tags.update(re.findall(r'"([^"]+)"', arr_block))

    checks = {
        "componentFamily": seed_component_family,
        "semanticRole": seed_semantic_role,
        "visualRole": seed_visual_role,
        "lifecycleStatus": seed_lifecycle_status,
        "capabilityTags": seed_capability_tags,
    }
    for k, vals in checks.items():
        if not vals:
            fail(f"seed classification extraction empty: {k}")
        bad = vals - allowed[k]
        if bad:
            fail(f"seed classification {k} not in SSOT: " + ", ".join(sorted(bad)))


if __name__ == "__main__":
    main()
    print("PASS check-ssot-vocabulary-contract assertions=5")
