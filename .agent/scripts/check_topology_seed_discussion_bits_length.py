#!/usr/bin/env python3
"""check_topology_seed_discussion_bits_length.py -- bits_length metadata gate.

Verifies the topology-seed-discussion build-template/build `--bits` contract
that closes the prefix-zero-fill silent-fallback gap against
.agent/rules/rule.md's "Do not use silent fallback for runtime boundary
failures.":

  - bits shorter than the question_space schema length still succeed (exit 0,
    input-load reduction is preserved) and carry explicit bits_length
    metadata (mode=prefix_zero_fill, expected/provided/implicit_zero_fill,
    bits_length_mismatch=true) instead of silently zero-filling with no
    observable trace.
  - bits exactly matching the schema length succeed with the same metadata
    shape (mode=exact, bits_length_mismatch=false), so callers can rely on
    the field always being present.
  - bits longer than the schema, containing an out-of-range index, or
    containing a non-0/1 value still fail closed (non-zero exit); prefix
    shortening is the only length deviation accepted.
  - `build` mode passes discussion_metadata.bits_length through unchanged
    from a build-template-produced tmp file.

Uses the smallest question_space (admin_manifests_navigation) as fixture to
keep the check fast; the expected bit count is read from `inspect --space`
rather than hardcoded, so this check stays correct if the underlying SSOT
sources grow or shrink.

This check does not itself judge SSOT authority, proof completion, or
implementation status; it only verifies the bits_length observability
contract described in .agent/tools/README.md.
"""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL = REPO_ROOT / ".agent" / "tools" / "topology-seed-discussion"
SPACE = "admin_manifests_navigation"


def run(args: list[str]):
    proc = subprocess.run(
        [str(TOOL), *args], capture_output=True, text=True, cwd=REPO_ROOT, timeout=30
    )
    return proc.returncode, proc.stdout, proc.stderr


def main() -> int:
    checks = 0
    problems: list[str] = []

    def expect(label: str, condition: bool):
        nonlocal checks
        checks += 1
        if not condition:
            problems.append(label)

    code, out, err = run(["inspect", "--space", SPACE])
    if code != 0:
        print(f"FAIL: inspect --space {SPACE} exited {code}: {err}", file=sys.stderr)
        return 1
    expected_len = len(json.loads(out)["question_bit_schema"]["bits"])

    # --- short (prefix) bits: exit 0 + explicit mismatch metadata ---
    short_bits = json.dumps([1, 0, 1])
    code, out, err = run(["build-template", "--space", SPACE, "--bits", short_bits])
    expect("short bits: exit 0", code == 0)
    if code == 0:
        body = json.loads(out)
        bl = body.get("bits_length", {})
        expect("short bits: mode=prefix_zero_fill", bl.get("mode") == "prefix_zero_fill")
        expect("short bits: expected matches schema length", bl.get("expected") == expected_len)
        expect("short bits: provided=3", bl.get("provided") == 3)
        expect(
            "short bits: implicit_zero_fill matches expected-provided",
            bl.get("implicit_zero_fill") == expected_len - 3,
        )
        expect("short bits: bits_length_mismatch=true", bl.get("bits_length_mismatch") is True)
        expect(
            "short bits: discussion_metadata mirrors top-level bits_length",
            body.get("discussion_metadata", {}).get("bits_length") == bl,
        )
    else:
        problems.append(f"short bits: unexpected failure stderr={err.strip()}")

    # --- exact-length bits: exit 0 + bits_length_mismatch=false ---
    exact_bits = json.dumps([0] * expected_len)
    code, out, err = run(["build-template", "--space", SPACE, "--bits", exact_bits])
    expect("exact bits: exit 0", code == 0)
    if code == 0:
        bl = json.loads(out).get("bits_length", {})
        expect("exact bits: mode=exact", bl.get("mode") == "exact")
        expect("exact bits: provided matches expected", bl.get("provided") == expected_len)
        expect("exact bits: implicit_zero_fill=0", bl.get("implicit_zero_fill") == 0)
        expect("exact bits: bits_length_mismatch=false", bl.get("bits_length_mismatch") is False)
    else:
        problems.append(f"exact bits: unexpected failure stderr={err.strip()}")

    # --- long bits: fail closed, no partial JSON / no silent truncation ---
    long_bits = json.dumps([0] * (expected_len + 1))
    code, out, err = run(["build-template", "--space", SPACE, "--bits", long_bits])
    expect("long bits: non-zero exit", code != 0)
    expect("long bits: no stdout emitted on fail-close", out.strip() == "")

    # --- invalid bit value: fail closed ---
    code, out, err = run(["build-template", "--space", SPACE, "--bits", "[2]"])
    expect("invalid bit value: non-zero exit", code != 0)

    # --- build mode passthrough of bits_length from a build-template tmp file ---
    code, out, err = run(["build-template", "--space", SPACE, "--bits", short_bits])
    expect("passthrough fixture: build-template exit 0", code == 0)
    if code == 0:
        template = json.loads(out)["tmp_json_template"]
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
            json.dump(template, f)
            tmp_path = f.name
        try:
            build_code, build_out, build_err = run(["build", "--answers", tmp_path])
            expect("build passthrough: exit 0", build_code == 0)
            if build_code == 0:
                build_body = json.loads(build_out)
                expect(
                    "build passthrough: top-level bits_length mirrors discussion_metadata",
                    build_body.get("bits_length")
                    == build_body.get("discussion_metadata", {}).get("bits_length"),
                )
                expect(
                    "build passthrough: bits_length_mismatch stays true",
                    build_body.get("bits_length", {}).get("bits_length_mismatch") is True,
                )
            else:
                problems.append(f"build passthrough: unexpected failure stderr={build_err.strip()}")
        finally:
            Path(tmp_path).unlink(missing_ok=True)
    else:
        problems.append(f"passthrough fixture: unexpected failure stderr={err.strip()}")

    print(f"[bits-length-gate] {checks} assertions run, {len(problems)} failed")
    if problems:
        for p in problems:
            print(f"[bits-length-gate] FAIL: {p}", file=sys.stderr)
        return 1
    print(f"PASS check-topology-seed-discussion-bits-length assertions={checks}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
