#!/usr/bin/env python3
"""check_agent_tools_surface.py -- .agent/tools proof/structure gate.

Verifies the governance contract declared in
docs/governance/agent-governance-routing-ssot.yaml
(agent_repo_observation_tools_surface) and .agent/tools/README.md:

  - Required .agent/tools entrypoints exist and are executable.
  - Each entrypoint is a thin wrapper delegating structured processing to
    .agent/scripts/agent_tools/readonly_observation.py (no inline JSON/YAML
    parsing, no forked structural processing body).
  - Each entrypoint rejects mutation-oriented options (e.g. --output) with a
    non-zero exit and an explicit stderr explanation, instead of silently
    forwarding them.
  - readonly_observation.py declares the BOUNDARY / _reject_mutation_args
    contract and contains no file-write or file-mutation calls.
  - A baseline invocation of each tool emits the declared authority-boundary
    metadata (read_only, writes_repo_files, authority_boundary disclaimer,
    prohibited_judgments vocabulary) and does not assert a top-level
    completion/authority claim (implemented / partial / not_started /
    proof_passed / semantic_completion). directory-map keeps the stable JSON
    array shape from emit-directory-tree-json.py instead (its no-authority
    boundary lives in README/tool-boundary documentation, not output shape).

This check does not itself judge SSOT authority, proof completion, or
implementation status of any bundle observed by the tools; it only verifies
.agent/tools' own structural / read-only / no-authority contract.

Structured subprocess invocation and JSON verification live here (Python3
stdlib only); the paired .agent/tests/check-agent-tools-surface.sh is a bash
CI entrypoint/orchestration wrapper only (see docs/framework-policy.yaml
repo_governance_tooling_dependency_policy).

Run with --self-test to exercise the negative-scenario unit tests (pure
in-memory fixtures; no repository files are read, and no subprocess runs).
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_DIR = REPO_ROOT / ".agent" / "tools"
READONLY_OBSERVATION = REPO_ROOT / ".agent" / "scripts" / "agent_tools" / "readonly_observation.py"

REQUIRED_TOOL_ENTRYPOINTS = [
    "directory-map",
    "ssot-map-query",
    "proof-surface-map",
    "topology-seed-discussion",
]

# Baseline args chosen so every tool exits 0 and emits JSON without touching
# unrelated repository semantics (deliberately unmatched query/id probes).
BASELINE_ARGS = {
    "directory-map": ["--root", "docs", "--depth", "0"],
    "ssot-map-query": ["--query", "__check_agent_tools_surface_probe__"],
    "proof-surface-map": ["--proof-id", "__check_agent_tools_surface_probe__"],
    "topology-seed-discussion": ["inspect"],
}

MUTATION_PROBE_ARGS = ["--output", "/tmp/check-agent-tools-surface-should-not-write.json"]

THIN_WRAPPER_MAX_LINES = 10
THIN_WRAPPER_BANNED_SNIPPETS = ("import json", "import yaml", "jq ", "python3 -c", "python -c")

WRITE_OPERATION_BANNED_SNIPPETS = ("os.remove(", "os.unlink(", "shutil.rmtree(", ".write_text(", "os.rename(")

REQUIRED_PROHIBITED_JUDGMENT_VOCAB = {"implemented", "partial", "not_started", "proof_passed", "semantic_completion"}
REQUIRED_AUTHORITY_DISCLAIMER_NEEDLES = (
    "not_ssot_authority",
    "not_proof_completion",
    "not_completion_judgment",
    "not_semantic_audit_judgment",
)
FORBIDDEN_TOP_LEVEL_TRUE_KEYS = {
    "implemented", "partial", "not_started", "proof_passed", "semantic_completion", "completion_judgment",
}


def thin_wrapper_failures(name: str, text: str) -> list[str]:
    failures = []
    non_blank_lines = [line for line in text.splitlines() if line.strip()]
    if len(non_blank_lines) > THIN_WRAPPER_MAX_LINES:
        failures.append(
            f"{name}: entrypoint has {len(non_blank_lines)} non-blank lines; expected a thin wrapper "
            f"(<= {THIN_WRAPPER_MAX_LINES}) delegating to .agent/scripts"
        )
    if "readonly_observation.py" not in text:
        failures.append(f"{name}: entrypoint does not delegate to .agent/scripts/agent_tools/readonly_observation.py")
    for snippet in THIN_WRAPPER_BANNED_SNIPPETS:
        if snippet in text:
            failures.append(f"{name}: entrypoint appears to embed structured processing ({snippet!r}); must delegate to .agent/scripts")
    return failures


def write_operation_findings(text: str) -> list[str]:
    findings = []
    if re.search(r"""open\([^)]*["']w""", text):
        findings.append("readonly_observation.py contains a file write open() call")
    for snippet in WRITE_OPERATION_BANNED_SNIPPETS:
        if snippet in text:
            findings.append(f"readonly_observation.py contains a file-mutation call: {snippet}")
    return findings


def mutation_rejection_failures(tool_name: str, returncode: int, stderr: str) -> list[str]:
    failures = []
    if returncode == 0:
        failures.append(f"{tool_name}: mutation-oriented option was accepted (exit 0) instead of rejected")
    if "mutation" not in stderr.lower():
        failures.append(f"{tool_name}: rejection stderr does not explain the mutation/file-write boundary")
    return failures


def boundary_metadata_failures(tool_name: str, result: dict) -> list[str]:
    failures = []
    boundary = result.get("boundary") if isinstance(result, dict) else None
    if not isinstance(boundary, dict):
        return [f"{tool_name}: output missing 'boundary' authority metadata object"]
    if boundary.get("read_only") is not True:
        failures.append(f"{tool_name}: boundary.read_only must be true")
    if boundary.get("writes_repo_files") is not False:
        failures.append(f"{tool_name}: boundary.writes_repo_files must be false")
    authority = str(boundary.get("authority_boundary", ""))
    for needle in REQUIRED_AUTHORITY_DISCLAIMER_NEEDLES:
        if needle not in authority:
            failures.append(f"{tool_name}: boundary.authority_boundary missing '{needle}' disclaimer")
    prohibited = boundary.get("prohibited_judgments")
    if not isinstance(prohibited, list) or not REQUIRED_PROHIBITED_JUDGMENT_VOCAB.issubset(set(prohibited)):
        failures.append(f"{tool_name}: boundary.prohibited_judgments missing required judgment vocabulary {sorted(REQUIRED_PROHIBITED_JUDGMENT_VOCAB)}")
    for key in FORBIDDEN_TOP_LEVEL_TRUE_KEYS:
        if isinstance(result, dict) and result.get(key) is True:
            failures.append(f"{tool_name}: output top-level key {key!r} asserts True, which is a completion/authority claim")
    return failures


def array_shape_failures(tool_name: str, result) -> list[str]:
    if not isinstance(result, list):
        return [f"{tool_name}: expected a stable JSON array shape, got {type(result).__name__}"]
    return []


def fail(msg: str) -> None:
    sys.stderr.write(f"FAIL: {msg}\n")


def main() -> int:
    os.chdir(REPO_ROOT)
    failures: list[str] = []

    if not READONLY_OBSERVATION.is_file():
        failures.append("missing .agent/scripts/agent_tools/readonly_observation.py")
    else:
        source_text = READONLY_OBSERVATION.read_text(encoding="utf-8")
        if "BOUNDARY" not in source_text or "_reject_mutation_args" not in source_text:
            failures.append("readonly_observation.py missing BOUNDARY / _reject_mutation_args contract")
        else:
            print("OK  [contract] readonly_observation.py declares BOUNDARY / _reject_mutation_args")
        write_findings = write_operation_findings(source_text)
        failures.extend(write_findings)
        if not write_findings:
            print("OK  [no-write] readonly_observation.py contains no file-mutation calls")

    readme = TOOLS_DIR / "README.md"
    if not readme.is_file():
        failures.append("missing .agent/tools/README.md")
    else:
        print("OK  [file] .agent/tools/README.md present")

    for name in REQUIRED_TOOL_ENTRYPOINTS:
        entry = TOOLS_DIR / name
        if not entry.is_file():
            failures.append(f"missing .agent/tools/{name}")
            continue

        if not os.access(entry, os.X_OK):
            failures.append(f".agent/tools/{name} is not executable (chmod +x required)")
        else:
            print(f"OK  [exec] .agent/tools/{name} is executable")

        text = entry.read_text(encoding="utf-8")
        wrapper_failures = thin_wrapper_failures(name, text)
        failures.extend(wrapper_failures)
        if not wrapper_failures:
            print(f"OK  [thin] .agent/tools/{name} delegates to readonly_observation.py")

        try:
            mut_proc = subprocess.run(
                [str(entry), *MUTATION_PROBE_ARGS], capture_output=True, text=True, cwd=REPO_ROOT, timeout=30
            )
        except OSError as exc:
            failures.append(f"{name}: failed to execute mutation probe: {exc}")
        else:
            mut_failures = mutation_rejection_failures(name, mut_proc.returncode, mut_proc.stderr)
            failures.extend(mut_failures)
            if not mut_failures:
                print(f"OK  [no-mutation] .agent/tools/{name} rejects --output")

        try:
            baseline_proc = subprocess.run(
                [str(entry), *BASELINE_ARGS.get(name, [])], capture_output=True, text=True, cwd=REPO_ROOT, timeout=30
            )
        except OSError as exc:
            failures.append(f"{name}: failed to execute baseline invocation: {exc}")
            continue

        if baseline_proc.returncode != 0:
            failures.append(f"{name}: baseline invocation failed (exit {baseline_proc.returncode}): {baseline_proc.stderr.strip()}")
            continue
        try:
            result = json.loads(baseline_proc.stdout)
        except json.JSONDecodeError as exc:
            failures.append(f"{name}: baseline invocation did not emit valid JSON: {exc}")
            continue

        if name == "directory-map":
            shape_failures = array_shape_failures(name, result)
            failures.extend(shape_failures)
            if not shape_failures:
                print(f"OK  [shape] {name} baseline output keeps the stable JSON array shape")
        else:
            boundary_failures = boundary_metadata_failures(name, result)
            failures.extend(boundary_failures)
            if not boundary_failures:
                print(f"OK  [no-authority] {name} output declares boundary metadata without an authority claim")

    print("")
    if failures:
        for msg in failures:
            fail(msg)
        sys.stderr.write(f"=== {len(failures)} .agent/tools surface check(s) failed ===\n")
        return 1
    print("=== .agent/tools surface checks passed ===")
    return 0


def _self_test() -> int:
    """Negative-scenario unit tests using pure in-memory fixtures (no disk I/O, no subprocess)."""
    checks = 0
    problems: list[str] = []

    def expect(label, condition):
        nonlocal checks
        checks += 1
        if not condition:
            problems.append(label)

    # thin_wrapper_failures
    clean_wrapper = '#!/usr/bin/env bash\nset -euo pipefail\nexec python3 readonly_observation.py directory-map "$@"\n'
    expect("clean thin wrapper must PASS", thin_wrapper_failures("t", clean_wrapper) == [])

    fat_wrapper = "\n".join([f"line{i}" for i in range(20)]) + "\nreadonly_observation.py\n"
    expect("oversized wrapper must FAIL", len(thin_wrapper_failures("t", fat_wrapper)) >= 1)

    non_delegating_wrapper = '#!/usr/bin/env bash\nset -euo pipefail\necho "no delegation"\n'
    expect("wrapper without delegation must FAIL", any("delegate" in f for f in thin_wrapper_failures("t", non_delegating_wrapper)))

    embedded_json_wrapper = '#!/usr/bin/env bash\nimport json\nreadonly_observation.py\n'
    expect("wrapper embedding structured processing must FAIL", any("structured processing" in f for f in thin_wrapper_failures("t", embedded_json_wrapper)))

    # write_operation_findings
    clean_source = 'with open(path, "r", encoding="utf-8") as f:\n    return json.load(f)\n'
    expect("read-only source must PASS", write_operation_findings(clean_source) == [])

    write_open_source = 'with open(path, "w") as f:\n    f.write(data)\n'
    expect("write-mode open() must FAIL", len(write_operation_findings(write_open_source)) >= 1)

    remove_source = "os.remove(path)\n"
    expect("os.remove call must FAIL", len(write_operation_findings(remove_source)) >= 1)

    # mutation_rejection_failures
    expect("rejected mutation (exit!=0, mentions mutation) must PASS", mutation_rejection_failures("t", 2, "FAIL: mutation/file-write option is not exposed") == [])
    expect("accepted mutation (exit 0) must FAIL", len(mutation_rejection_failures("t", 0, "FAIL: mutation")) >= 1)
    expect("rejection without explanation must FAIL", len(mutation_rejection_failures("t", 2, "FAIL: unknown option")) >= 1)

    # boundary_metadata_failures
    good_boundary = {
        "boundary": {
            "read_only": True,
            "writes_repo_files": False,
            "authority_boundary": "observation_only_not_ssot_authority_not_proof_completion_not_completion_judgment_not_semantic_audit_judgment_not_implemented_status",
            "prohibited_judgments": ["implemented", "partial", "not_started", "proof_passed", "semantic_completion"],
        }
    }
    expect("well-formed boundary metadata must PASS", boundary_metadata_failures("t", good_boundary) == [])

    expect("missing boundary key must FAIL", len(boundary_metadata_failures("t", {})) == 1)

    mutated_boundary = json.loads(json.dumps(good_boundary))
    mutated_boundary["boundary"]["read_only"] = False
    expect("read_only=False must FAIL", len(boundary_metadata_failures("t", mutated_boundary)) >= 1)

    mutated_boundary2 = json.loads(json.dumps(good_boundary))
    mutated_boundary2["boundary"]["authority_boundary"] = "nothing_declared"
    expect("authority_boundary missing disclaimers must FAIL", len(boundary_metadata_failures("t", mutated_boundary2)) >= 1)

    mutated_boundary3 = json.loads(json.dumps(good_boundary))
    mutated_boundary3["boundary"]["prohibited_judgments"] = ["implemented"]
    expect("prohibited_judgments missing vocabulary must FAIL", len(boundary_metadata_failures("t", mutated_boundary3)) >= 1)

    claim_result = json.loads(json.dumps(good_boundary))
    claim_result["implemented"] = True
    expect("top-level implemented=True claim must FAIL", len(boundary_metadata_failures("t", claim_result)) >= 1)

    # array_shape_failures
    expect("list result must PASS array shape", array_shape_failures("t", []) == [])
    expect("dict result must FAIL array shape", len(array_shape_failures("t", {})) == 1)

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
