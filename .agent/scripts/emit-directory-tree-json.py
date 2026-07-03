#!/usr/bin/env python3
"""emit-directory-tree-json.py -- generic repo directory surface JSON observation.

Purpose:
  General-purpose repo directory surface observation mechanism. Emits any
  repository directory subtree as a stable JSON array of objects, one JSON
  object per line, for consumption by structure/connectivity gates (e.g.
  .agent/tests/check-ssot-proof-surface-connectivity.sh).

  docs/ SSOT candidates and .agent/tests/ executable proof candidates are
  both projections of this same general mechanism (A subset-of B): docs/ is
  not special-cased tooling, it is one --root argument among many. The
  pre-existing .agent/scripts/emit-docs-tree-json.sh remains the
  docs-specialized (bash/awk) legacy instance kept for backward
  compatibility with .agent/tests/check-docs-ssot-connectivity.sh; new
  governance surfaces (including the SSOT proof surface connectivity gate)
  should use this general emitter instead of writing another root-specific
  emitter.

Element shape (stable contract; one JSON object per line between [ and ]):
  {"path":..,"name":..,"dir":..,"type":"file|directory","depth":N,"ext":..,
   "surface_kind":..,"is_ssot":true|false,"is_dynamic_reference":true|false,
   "is_executable_test":true|false,"is_governance_test":true|false}

is_ssot classification (structural, not semantic) mirrors
emit-docs-tree-json.sh's docs/ vocabulary:
  - <root>/**/*-ssot.yaml and <root>/**/*-ssot.md
  - docs/framework-core.yaml, docs/framework-policy.yaml, docs/file-structure.yaml

is_dynamic_reference classification:
  - docs/system-roadmap.yaml is a dynamic reference point (constantly
    changing status surface), NOT an implementation-authority SSOT.

is_executable_test classification:
  - any file named check-*.sh (executable proof surface candidate,
    regardless of root -- primarily meaningful under .agent/tests/).

is_governance_test classification:
  - is_executable_test AND path starts with ".agent/tests/" (distinguishes
    the governance executable-test surface from a hypothetical check-*.sh
    living elsewhere).

Usage:
  python3 .agent/scripts/emit-directory-tree-json.py [--root <dir>] [--depth <n>] [--output <file>]
    --root    tree root relative to repository root (default: docs)
    --depth   max depth relative to root (default: unlimited)
    --output  write JSON to file instead of stdout

Dependencies: Python3 standard library only (argparse, os, json, pathlib).
  No third-party packages (no PyYAML, no jq, no node, no ruby) -- repo
  governance tooling structured processing must use Python3 stdlib only.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

PRUNE_NAMES = {"node_modules", ".git", "dist", "build", "coverage", ".cache", "_fresh", "vendor", "tmp"}

STRUCTURAL_SSOT_FILES = {
    "docs/framework-core.yaml",
    "docs/framework-policy.yaml",
    "docs/file-structure.yaml",
}
DYNAMIC_REFERENCE_FILES = {"docs/system-roadmap.yaml"}


def classify(rel_path: str, name: str, is_dir: bool):
    is_ssot = False
    is_dynamic_reference = False
    is_executable_test = False
    if not is_dir:
        if rel_path in DYNAMIC_REFERENCE_FILES:
            is_dynamic_reference = True
        elif name.endswith("-ssot.yaml") or name.endswith("-ssot.md"):
            is_ssot = True
        elif rel_path in STRUCTURAL_SSOT_FILES:
            is_ssot = True
        if name.startswith("check-") and name.endswith(".sh"):
            is_executable_test = True
    is_governance_test = is_executable_test and rel_path.startswith(".agent/tests/")
    return is_ssot, is_dynamic_reference, is_executable_test, is_governance_test


def surface_kind_for(is_dir, is_ssot, is_dynamic_reference, is_executable_test):
    if is_dir:
        return "directory"
    if is_dynamic_reference:
        return "dynamic_reference_surface"
    if is_ssot:
        return "ssot_surface"
    if is_executable_test:
        return "executable_test_surface"
    return "generic_file_surface"


def ext_for(name: str, is_dir: bool) -> str:
    if is_dir:
        return ""
    dot = name.rfind(".")
    if dot > 0:
        return name[dot + 1:]
    return ""


def make_entry(rel_path, name, rel_dir, kind, depth):
    is_dir = kind == "directory"
    is_ssot, is_dynamic_reference, is_executable_test, is_governance_test = classify(rel_path, name, is_dir)
    return {
        "path": rel_path,
        "name": name,
        "dir": rel_dir,
        "type": kind,
        "depth": depth,
        "ext": ext_for(name, is_dir),
        "surface_kind": surface_kind_for(is_dir, is_ssot, is_dynamic_reference, is_executable_test),
        "is_ssot": is_ssot,
        "is_dynamic_reference": is_dynamic_reference,
        "is_executable_test": is_executable_test,
        "is_governance_test": is_governance_test,
    }


def walk(repo_root: Path, root_rel: str, max_depth):
    root_abs = repo_root / root_rel
    root_depth = len(Path(root_rel).parts)
    entries = []
    for dirpath, dirnames, filenames in os.walk(root_abs):
        dirnames[:] = sorted(d for d in dirnames if d not in PRUNE_NAMES)
        filenames = sorted(filenames)
        rel_dirpath = os.path.relpath(dirpath, repo_root).replace(os.sep, "/")
        depth_of_dirpath = len(Path(rel_dirpath).parts) - root_depth
        child_depth = depth_of_dirpath + 1
        if max_depth is not None and child_depth > max_depth:
            dirnames[:] = []
            continue
        for name in dirnames:
            rel_path = f"{rel_dirpath}/{name}"
            entries.append(make_entry(rel_path, name, rel_dirpath, "directory", child_depth))
        for name in filenames:
            rel_path = f"{rel_dirpath}/{name}"
            entries.append(make_entry(rel_path, name, rel_dirpath, "file", child_depth))
    entries.sort(key=lambda e: e["path"])
    return entries


def render(entries) -> str:
    lines = ["["]
    for i, e in enumerate(entries):
        suffix = "," if i < len(entries) - 1 else ""
        lines.append("  " + json.dumps(e, ensure_ascii=False, separators=(",", ":")) + suffix)
    lines.append("]")
    return "\n".join(lines) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("--root", default="docs")
    parser.add_argument("--depth", type=int, default=2)
    parser.add_argument("--full", action="store_true", help="Emit the full tree instead of the bounded default depth.")
    parser.add_argument("--output", default=None)
    args = parser.parse_args(argv)

    repo_root = Path(__file__).resolve().parents[2]
    root_rel = args.root.rstrip("/")
    if not (repo_root / root_rel).is_dir():
        sys.stderr.write(f"FAIL: root directory not found: {root_rel}\n")
        return 1
    if args.depth is not None and args.depth < 0:
        sys.stderr.write(f"FAIL: --depth must be a non-negative integer: {args.depth}\n")
        return 2

    effective_depth = None if args.full else args.depth
    entries = walk(repo_root, root_rel, effective_depth)
    output_text = render(entries)
    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(output_text)
    else:
        sys.stdout.write(output_text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
