#!/usr/bin/env bash
# emit-docs-tree-json.sh — emit a machine-readable JSON array describing a docs tree.
#
# Purpose:
#   Stable observation input for structure/connectivity gates
#   (e.g. .agent/tests/check-docs-ssot-connectivity.sh).
#   The primary output is a JSON array, not a text tree.
#
# Dependencies:
#   bash + find + sed + awk only. jq / python / node are intentionally NOT required
#   so the local structure gate keeps working in minimal environments.
#
# Usage:
#   bash .agent/scripts/emit-docs-tree-json.sh [--root <dir>] [--depth <n>] [--output <file>]
#     --root    tree root relative to repository root (default: docs)
#     --depth   max depth relative to root (default: unlimited)
#     --output  write JSON to file instead of stdout
#
# Element shape (stable contract; one JSON object per line between [ and ]):
#   {"path":..,"name":..,"dir":..,"type":"file|directory","depth":N,"ext":..,
#    "is_ssot":true|false,"is_dynamic_reference":true|false}
#
# is_ssot classification (structural, not semantic):
#   - <root>/**/*-ssot.yaml and <root>/**/*-ssot.md
#   - docs/governance/*-ssot.yaml and docs/governance/*-ssot.md (covered by the glob above)
#   - docs/framework-core.yaml, docs/framework-policy.yaml, docs/file-structure.yaml
#
# is_dynamic_reference classification:
#   - docs/system-roadmap.yaml is a dynamic reference point (constantly changing
#     status surface), NOT an implementation-authority SSOT. It is therefore
#     emitted with is_ssot=false and is_dynamic_reference=true.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

ROOT="docs"
DEPTH=""
OUTPUT=""

while [ $# -gt 0 ]; do
  case "$1" in
    --root)   ROOT="${2:?--root requires a value}"; shift 2 ;;
    --depth)  DEPTH="${2:?--depth requires a value}"; shift 2 ;;
    --output) OUTPUT="${2:?--output requires a value}"; shift 2 ;;
    -h|--help)
      sed -n '2,30p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *)
      echo "FAIL: unknown argument: $1" >&2; exit 2 ;;
  esac
done

ROOT="${ROOT%/}"
if [ ! -d "$REPO_ROOT/$ROOT" ]; then
  echo "FAIL: root directory not found: $ROOT" >&2
  exit 1
fi

# Bulky/transient directories excluded from observation.
PRUNE_NAMES=(node_modules .git dist build coverage .cache _fresh vendor tmp)

find_args=("$REPO_ROOT/$ROOT" -mindepth 1)
if [ -n "$DEPTH" ]; then
  case "$DEPTH" in
    ''|*[!0-9]*) echo "FAIL: --depth must be a non-negative integer: $DEPTH" >&2; exit 2 ;;
  esac
  find_args+=(-maxdepth "$DEPTH")
fi

prune_expr=()
for name in "${PRUNE_NAMES[@]}"; do
  if [ ${#prune_expr[@]} -gt 0 ]; then prune_expr+=(-o); fi
  prune_expr+=(-name "$name")
done

emit() {
  find "${find_args[@]}" \( -type d \( "${prune_expr[@]}" \) -prune \) -o \( -type f -o -type d \) -print \
    | LC_ALL=C sort \
    | sed "s#^$REPO_ROOT/##" \
    | awk -v root="$ROOT" '
      function json_escape(s) {
        gsub(/\\/, "\\\\", s)
        gsub(/"/, "\\\"", s)
        gsub(/\t/, "\\t", s)
        return s
      }
      BEGIN { print "["; first = 1 }
      {
        path = $0
        n = split(path, parts, "/")
        name = parts[n]
        dir = substr(path, 1, length(path) - length(name) - 1)

        rootdepth = split(root, rootparts, "/")
        depth = n - rootdepth

        type = "file"
        if (system("test -d \"" path "\"") == 0) type = "directory"

        ext = ""
        if (type == "file") {
          dot = 0
          for (i = length(name); i >= 1; i--) {
            if (substr(name, i, 1) == ".") { dot = i; break }
          }
          if (dot > 1) ext = substr(name, dot + 1)
        }

        is_ssot = "false"
        is_dynamic_reference = "false"
        if (type == "file") {
          # dynamic reference points are status surfaces, never implementation SSOT
          if (path == "docs/system-roadmap.yaml") {
            is_dynamic_reference = "true"
          } else if (name ~ /-ssot\.(yaml|md)$/) {
            is_ssot = "true"
          } else if (path == "docs/framework-core.yaml" ||
                     path == "docs/framework-policy.yaml" ||
                     path == "docs/file-structure.yaml") {
            is_ssot = "true"
          }
        }

        line = sprintf("{\"path\":\"%s\",\"name\":\"%s\",\"dir\":\"%s\",\"type\":\"%s\",\"depth\":%d,\"ext\":\"%s\",\"is_ssot\":%s,\"is_dynamic_reference\":%s}",
          json_escape(path), json_escape(name), json_escape(dir), type, depth, json_escape(ext), is_ssot, is_dynamic_reference)

        if (first) { printf "  %s", line; first = 0 }
        else { printf ",\n  %s", line }
      }
      END { if (!first) printf "\n"; print "]" }
    '
}

cd "$REPO_ROOT"
if [ -n "$OUTPUT" ]; then
  emit > "$OUTPUT"
else
  emit
fi
