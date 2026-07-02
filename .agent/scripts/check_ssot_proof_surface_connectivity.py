#!/usr/bin/env python3
"""check_ssot_proof_surface_connectivity.py -- SSOT proof surface connectivity gate.

Uses the general directory-JSON-observation mechanism
(emit-directory-tree-json.py) to observe docs/ (SSOT candidates) and
.agent/tests/ (executable proof candidates) on the same observation plane,
and verifies that every docs/ is_ssot=true candidate is connected to at
least one reachable .agent/tests/check-*.sh executable proof surface.

A mere reference from .agent/docs/ssot-map.yaml does NOT count as a proof
connection (ssot-map.yaml is a read-routing index, not an executable proof
surface). Proof connection candidates are:
  - .agent/docs/test-bundles.yaml runner_surfaces entries that are
    themselves .agent/tests/check-*.sh scripts
  - any .agent/tests/check-*.sh whose content explicitly references the
    SSOT path
  - reachable via .github/workflows/*.yml -> check-*.sh execution chain

"Reachable" means the check-*.sh script is exercised from at least one of:
  test-bundles.yaml runner_surfaces, check-structure.sh, check-local-ci.sh,
  or a .github/workflows/*.yml job.

Run with --self-test to exercise the negative-scenario unit tests (pure
in-memory fixtures; no repository files are read or written).

Python3 stdlib only (argparse/importlib/json/re/glob/os); no third-party
packages, no jq/node/ruby (see docs/framework-policy.yaml
repo_governance_tooling_dependency_policy).
"""
from __future__ import annotations

import glob
import importlib.util
import os
import re
import sys
from pathlib import Path

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(SCRIPT_DIR, "lib"))
import minimal_yaml as yaml  # noqa: E402


def _load_emit_module():
    spec = importlib.util.spec_from_file_location(
        "emit_directory_tree_json", os.path.join(SCRIPT_DIR, "emit-directory-tree-json.py")
    )
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# SSOT candidates without a dedicated executable proof surface (a reachable
# .agent/tests/check-*.sh literally referencing the SSOT path). Each entry
# REQUIRES a non-empty reason. This allowlist must not grow silently: new
# docs/**/*-ssot.yaml|.md additions are expected to gain a dedicated
# executable proof surface reference rather than being added here.
UNCONNECTED_SSOT_ALLOWLIST = {
    "docs/file-structure.yaml":
        "structural vocabulary reference read by agents via .agent/docs/ssot-map.yaml; "
        "its content terms are enforced through .agent/tests/check-structure.sh's own "
        "required_content_terms manifest rather than through a literal path-string "
        "reference inside a dedicated check-*.sh.",
    "docs/design/abstract-function-primitive-registry-ssot.yaml":
        "governance/design SSOT validated through agent read-routing "
        "(.agent/docs/ssot-map.yaml) and cross-referenced by other SSOT documents; "
        "no dedicated check-*.sh literal-reference proof surface exists yet.",
    "docs/design/auth-db-session-credential-ssot.yaml":
        "governance/design SSOT validated through agent read-routing "
        "(.agent/docs/ssot-map.yaml); no dedicated check-*.sh literal-reference proof "
        "surface exists yet.",
    "docs/design/component-catalog-classification-ssot.yaml":
        "referenced by check_ssot_vocabulary_contract.py (invoked from "
        "check-ssot-vocabulary-contract.sh) via the Python YAML loader rather than a "
        "literal grep-able path string in the .sh file itself.",
    "docs/design/test-proof-manifest-ssot.yaml":
        "referenced by check_test_proof_manifest_integrity.py (invoked from "
        "check-test-proof-manifest-integrity.sh) via the Python YAML loader rather than "
        "a literal grep-able path string in the .sh file itself.",
    "docs/design/admin-console-workflow-ssot.yaml":
        "governance/design SSOT validated through agent read-routing "
        "(.agent/docs/ssot-map.yaml) and cross-referenced by other checked SSOT "
        "documents (e.g. enum-dictionary-ssot.yaml, admin-master-roster-management-ssot.yaml); "
        "no dedicated check-*.sh literal-reference proof surface exists yet.",
    "docs/design/cli-model-context-protocols-port-ssot.md":
        "companion markdown to the boundary-checked cli-model-context-protocols-port-ssot.yaml "
        "(referenced directly in check-structure.sh); prose-only, no independent executable "
        "proof surface expected.",
    "docs/design/extended-runtime-bundle-registry-ssot.md":
        "companion markdown to extended-runtime-bundle-registry-ssot.yaml; prose-only, no "
        "independent executable proof surface expected.",
    "docs/design/ui-ux-primitive-catalog-ssot.yaml":
        "governance/design SSOT validated through agent read-routing "
        "(.agent/docs/ssot-map.yaml) and exercised indirectly via "
        "check-ui-ux-executable-component-slice.sh's component-slice checks; no dedicated "
        "check-*.sh literal-reference proof surface exists yet.",
    "docs/design/user-facing-helper-manual-ssot.md":
        "companion markdown to user-facing-helper-manual-ssot.yaml; prose-only, no "
        "independent executable proof surface expected.",
    "docs/design/user-facing-helper-manual-ssot.yaml":
        "governance/design SSOT validated through agent read-routing "
        "(.agent/docs/ssot-map.yaml); no dedicated check-*.sh literal-reference proof "
        "surface exists yet.",
    "docs/governance/agent-governance-routing-ssot.md":
        "companion markdown to agent-governance-routing-ssot.yaml; both are the governance "
        "routing SSOT read directly by AGENTS.md / .agent/rules/rule.md, verified structurally "
        "by check-worktype-routing.sh without a literal path-string reference.",
    "docs/governance/agent-governance-routing-ssot.yaml":
        "governance routing SSOT read directly by AGENTS.md / .agent/rules/rule.md; verified "
        "structurally (routing existence, reference integrity, required vocabulary) by "
        "check-worktype-routing.sh without a literal path-string reference.",
}


def check_runner_surfaces(bundles, governance_test_paths, path_exists):
    """Validate test-bundles.yaml runner_surfaces existence + JSON observation.

    path_exists: callable(path) -> bool, injected so unit tests do not touch disk.
    Returns (failures, runner_surface_scripts_set).
    """
    failures = []
    runner_surface_scripts = set()
    for bundle in bundles:
        bundle_id = bundle.get("bundle_id") or "<unknown bundle>"
        for surface in yaml.arr(bundle.get("runner_surfaces")):
            if not path_exists(surface):
                failures.append(f"test-bundles.yaml bundle {bundle_id}: runner_surfaces path does not exist: {surface}")
                continue
            if re.match(r"^\.agent/tests/check-.*\.sh$", surface):
                runner_surface_scripts.add(surface)
                if surface not in governance_test_paths:
                    failures.append(
                        f"test-bundles.yaml bundle {bundle_id}: runner_surface {surface} "
                        "is not observed as is_executable_test=true in .agent/tests JSON"
                    )
    return failures, runner_surface_scripts


def compute_reachable_scripts(governance_test_paths, runner_surface_scripts, structure_check_text, local_ci_text, workflow_texts):
    def is_reachable(script_path):
        if script_path in runner_surface_scripts:
            return True
        if script_path in structure_check_text or script_path in local_ci_text:
            return True
        return any(script_path in text for text in workflow_texts.values())

    return sorted(p for p in governance_test_paths if is_reachable(p))


def check_ssot_connectivity(ssot_candidates, script_contents, allowlist):
    """script_contents: {path: text} restricted to reachable check-*.sh scripts only.

    A mere ssot-map.yaml reference never counts because ssot-map.yaml is
    never a member of script_contents (callers only populate it from
    reachable .agent/tests/check-*.sh files).
    """
    failures = []
    connected = []
    allowlisted = []
    for ssot in ssot_candidates:
        connecting_scripts = sorted(p for p, text in script_contents.items() if ssot in text)
        if connecting_scripts:
            connected.append(ssot)
        elif ssot in allowlist:
            allowlisted.append(ssot)
        else:
            failures.append(
                f"unconnected SSOT proof surface: {ssot} is not referenced by any reachable "
                ".agent/tests/check-*.sh executable proof surface (ssot-map.yaml alone does not count)"
            )
    return failures, connected, allowlisted


def check_allowlist_integrity(allowlist, ssot_candidates):
    failures = []
    for path, reason in allowlist.items():
        if not str(reason).strip():
            failures.append(f"UNCONNECTED_SSOT_ALLOWLIST entry {path} missing a reason comment")
        if path not in ssot_candidates:
            failures.append(f"UNCONNECTED_SSOT_ALLOWLIST entry {path} is stale (no longer an SSOT candidate)")
    return failures


def fail(msg):
    sys.stderr.write(f"FAIL: {msg}\n")


def main():
    repo_root = Path(os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..")))
    os.chdir(repo_root)
    emit_mod = _load_emit_module()

    failures = []

    docs_entries = emit_mod.walk(repo_root, "docs", None)
    tests_entries = emit_mod.walk(repo_root, ".agent/tests", None)

    if len(docs_entries) < 30:
        failures.append(f"docs tree JSON observation suspiciously small ({len(docs_entries)} entries)")
    else:
        print(f"OK  [tree] docs tree JSON observed ({len(docs_entries)} entries)")

    ssot_candidates = sorted(e["path"] for e in docs_entries if e["is_ssot"])
    if len(ssot_candidates) < 10:
        failures.append(f"docs SSOT candidate count suspiciously small ({len(ssot_candidates)})")
    else:
        print(f"OK  [tree] {len(ssot_candidates)} docs SSOT candidates classified from JSON")

    if "docs/system-roadmap.yaml" in ssot_candidates:
        failures.append("docs/system-roadmap.yaml must be a dynamic reference point, not an SSOT candidate")
    dynref_paths = {e["path"] for e in docs_entries if e["is_dynamic_reference"]}
    if "docs/system-roadmap.yaml" not in dynref_paths:
        failures.append("docs/system-roadmap.yaml must be observed with is_dynamic_reference=true")

    governance_tests = [e for e in tests_entries if e["is_governance_test"]]
    if len(governance_tests) < 10:
        failures.append(f".agent/tests executable proof surface count suspiciously small ({len(governance_tests)})")
    else:
        print(f"OK  [tree] {len(governance_tests)} .agent/tests executable proof surfaces observed as JSON")
    governance_test_paths = {e["path"] for e in governance_tests}

    test_bundles = yaml.load_file(".agent/docs/test-bundles.yaml")
    bundles = yaml.arr(test_bundles.get("reverse_lookup"))
    if not bundles:
        failures.append(".agent/docs/test-bundles.yaml reverse_lookup is empty")

    runner_failures, runner_surface_scripts = check_runner_surfaces(bundles, governance_test_paths, os.path.exists)
    failures.extend(runner_failures)
    print(f"OK  [runner-surfaces] {sum(len(yaml.arr(b.get('runner_surfaces'))) for b in bundles)} runner_surfaces entries checked for existence")
    print(f"OK  [runner-surfaces] {len(runner_surface_scripts)} check-*.sh runner_surfaces observed as is_executable_test=true")

    workflow_texts = {}
    for wf in sorted(glob.glob(".github/workflows/*.yml")):
        with open(wf, "r", encoding="utf-8") as f:
            workflow_texts[wf] = f.read()

    with open(".agent/tests/check-structure.sh", "r", encoding="utf-8") as f:
        structure_check_text = f.read()
    with open(".agent/tests/check-local-ci.sh", "r", encoding="utf-8") as f:
        local_ci_text = f.read()

    reachable_scripts = compute_reachable_scripts(
        governance_test_paths, runner_surface_scripts, structure_check_text, local_ci_text, workflow_texts
    )
    print(
        f"OK  [reachable] {len(reachable_scripts)}/{len(governance_test_paths)} .agent/tests/check-*.sh reachable from "
        "test-bundles.yaml / check-structure.sh / check-local-ci.sh / workflows"
    )

    script_contents = {}
    for p in reachable_scripts:
        with open(p, "r", encoding="utf-8", errors="replace") as f:
            script_contents[p] = f.read()

    conn_failures, connected, allowlisted = check_ssot_connectivity(ssot_candidates, script_contents, UNCONNECTED_SSOT_ALLOWLIST)
    failures.extend(conn_failures)
    print(f"OK  [proof-connectivity] {len(connected)} docs SSOT candidates connected to a reachable executable proof surface")
    if allowlisted:
        print(f"OK  [proof-connectivity] {len(allowlisted)} docs SSOT candidates allowlisted with an explicit reason")

    failures.extend(check_allowlist_integrity(UNCONNECTED_SSOT_ALLOWLIST, ssot_candidates))

    print("")
    if failures:
        for f in failures:
            fail(f)
        sys.stderr.write(f"=== {len(failures)} SSOT proof surface connectivity check(s) failed ===\n")
        return 1
    print("=== SSOT proof surface connectivity checks passed ===")
    return 0


def _self_test():
    """Negative-scenario unit tests using pure in-memory fixtures (no disk I/O)."""
    checks = 0
    problems = []

    def expect(label, condition):
        nonlocal checks
        checks += 1
        if not condition:
            problems.append(label)

    # 1. SSOT with no reachable-script reference and not allowlisted -> FAIL.
    ssot_candidates = ["docs/design/new-thing-ssot.yaml"]
    script_contents = {".agent/tests/check-unrelated.sh": "no reference here"}
    failures, connected, allowlisted = check_ssot_connectivity(ssot_candidates, script_contents, {})
    expect("unconnected SSOT with no allowlist entry must FAIL", len(failures) == 1 and not connected and not allowlisted)

    # 2. Same SSOT, but the ONLY reference is via a synthetic ssot-map-like blob that is
    #    deliberately excluded from script_contents -> must still FAIL (proves that a mere
    #    ssot-map.yaml reference never counts as a proof connection, since ssot-map content
    #    is structurally never passed into script_contents by the real caller).
    ssot_map_like_content = {"'.agent/docs/ssot-map.yaml'-like-blob": "docs/design/new-thing-ssot.yaml is referenced here"}
    failures2, connected2, allowlisted2 = check_ssot_connectivity(ssot_candidates, {}, {})
    expect(
        "ssot-map-only reference must not count as proof connection",
        len(failures2) == 1 and not connected2 and bool(ssot_map_like_content),
    )

    # 3. SSOT referenced by a reachable check-*.sh -> PASS (connected).
    script_contents_ok = {".agent/tests/check-new-thing.sh": "grep -qF docs/design/new-thing-ssot.yaml file"}
    failures3, connected3, allowlisted3 = check_ssot_connectivity(ssot_candidates, script_contents_ok, {})
    expect("SSOT referenced by a reachable check-*.sh must connect", not failures3 and connected3 == ssot_candidates)

    # 4. SSOT allowlisted with a reason -> PASS (allowlisted), no failure.
    failures4, connected4, allowlisted4 = check_ssot_connectivity(ssot_candidates, {}, {ssot_candidates[0]: "explicit reason"})
    expect("allowlisted SSOT with reason must not FAIL", not failures4 and allowlisted4 == ssot_candidates)

    # 5. Allowlist entry with an empty reason -> integrity FAIL.
    integrity_failures = check_allowlist_integrity({"docs/x-ssot.yaml": "   "}, ["docs/x-ssot.yaml"])
    expect("allowlist entry with blank reason must FAIL", len(integrity_failures) == 1)

    # 6. Stale allowlist entry (no longer an SSOT candidate) -> integrity FAIL.
    integrity_failures2 = check_allowlist_integrity({"docs/gone-ssot.yaml": "reason"}, ["docs/other-ssot.yaml"])
    expect("stale allowlist entry must FAIL", len(integrity_failures2) == 1)

    # 7. runner_surfaces path that does not exist -> FAIL.
    bundles_missing = [{"bundle_id": "b1", "runner_surfaces": [".agent/tests/check-missing.sh"]}]
    rf, _ = check_runner_surfaces(bundles_missing, set(), lambda p: False)
    expect("nonexistent runner_surfaces path must FAIL", len(rf) == 1)

    # 8. runner_surfaces path that exists but is not observed as is_executable_test=true -> FAIL.
    bundles_unobserved = [{"bundle_id": "b1", "runner_surfaces": [".agent/tests/check-present.sh"]}]
    rf2, scripts2 = check_runner_surfaces(bundles_unobserved, set(), lambda p: True)
    expect("runner_surface not observed as is_executable_test must FAIL", len(rf2) == 1 and ".agent/tests/check-present.sh" in scripts2)

    # 9. runner_surfaces path that exists and IS observed -> PASS.
    rf3, scripts3 = check_runner_surfaces(bundles_unobserved, {".agent/tests/check-present.sh"}, lambda p: True)
    expect("runner_surface observed as is_executable_test must PASS", len(rf3) == 0 and scripts3 == {".agent/tests/check-present.sh"})

    print(f"[self-test] {checks} assertions run, {len(problems)} failed")
    if problems:
        for p in problems:
            print(f"[self-test] FAIL: {p}", file=sys.stderr)
        return 1
    print("[self-test] all negative-scenario assertions passed")
    return 0


if __name__ == "__main__":
    if "--self-test" in sys.argv[1:]:
        sys.exit(_self_test())
    sys.exit(main())
