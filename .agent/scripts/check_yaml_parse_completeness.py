#!/usr/bin/env python3
"""check_yaml_parse_completeness.py -- detects silent minimal_yaml truncation.

minimal_yaml.py (.agent/scripts/lib/minimal_yaml.py) is a hand-rolled YAML
subset parser (no PyYAML dependency allowed for repo governance tooling; see
docs/framework-policy.yaml repo_governance_tooling_dependency_policy). Because
it is a recursive-descent parser, any line it cannot handle at any nesting
depth makes the innermost _parse_map/_parse_seq loop stop early, and that
stuck cursor position propagates all the way up: the outermost call always
ends up returning fewer top-level keys than the file actually has. So a
top-level-key cross-check is a complete detector for this whole bug class,
not just a sample of it.

This check parses every governance-owned yaml file with minimal_yaml, and
independently scans the raw text with a regex for column-0 `key:` lines, then
requires the two top-level key lists to match. A mismatch means minimal_yaml
silently dropped content -- exactly what happened, undetected, to
docs/design/abstract-function-primitive-registry-ssot.yaml and
docs/design/topology-recommendation-ci-runtime.yaml before both the parser
fix and this check were added.
"""
from __future__ import annotations

import glob
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import minimal_yaml as yaml  # noqa: E402

ROOT = os.getcwd()

SCAN_GLOBS = [
    "docs/**/*.yaml",
    "docs/**/*.yml",
    ".agent/**/*.yaml",
    ".agent/**/*.yml",
]

# Matches a column-0 mapping key: bare word/path chars, or a quoted key.
_TOP_LEVEL_KEY_RE = re.compile(r'^(?:([A-Za-z0-9_.\-/]+)|"([^"]+)"|\'([^\']+)\'):(?:\s|$)')


def _raw_top_level_keys(path: str) -> list[str]:
    keys: list[str] = []
    in_block_scalar = False
    with open(path, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\n")
            if line.strip() == "" or line.startswith("#"):
                continue
            if line[:1] in (" ", "\t"):
                continue
            if line in ("---", "..."):
                continue
            m = _TOP_LEVEL_KEY_RE.match(line)
            if m:
                keys.append(next(g for g in m.groups() if g is not None))
    return keys


def _collect_files() -> list[str]:
    files: set[str] = set()
    for pattern in SCAN_GLOBS:
        files.update(glob.glob(pattern, recursive=True))
    return sorted(files)


def main() -> int:
    os.chdir(ROOT)
    files = _collect_files()
    if not files:
        sys.stderr.write("FAIL: no yaml files found under docs/design, docs/governance, .agent\n")
        return 1

    failures: list[str] = []
    checked = 0
    for path in files:
        expected = _raw_top_level_keys(path)
        if not expected:
            continue  # not a top-level mapping (e.g. a top-level list document) -- out of scope
        try:
            parsed = yaml.load_file(path)
        except Exception as exc:  # noqa: BLE001 -- report as a failure, not a crash
            failures.append(f"{path}: minimal_yaml raised {exc!r}")
            continue
        if not isinstance(parsed, dict):
            failures.append(f"{path}: expected a top-level mapping, got {type(parsed).__name__}")
            continue
        checked += 1
        actual = list(parsed.keys())
        missing = [k for k in expected if k not in actual]
        if missing:
            failures.append(
                f"{path}: minimal_yaml silently dropped top-level key(s) {missing} "
                f"(raw scan found {expected}, parser returned {actual}) -- likely an "
                "unmarked multi-line plain scalar or other unsupported construct upstream"
            )

    if failures:
        for message in failures:
            sys.stderr.write(f"FAIL: {message}\n")
        sys.stderr.write(f"=== {len(failures)} yaml parse-completeness check(s) failed ===\n")
        return 1

    print(f"PASS check-yaml-parse-completeness files={len(files)} checked={checked}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
