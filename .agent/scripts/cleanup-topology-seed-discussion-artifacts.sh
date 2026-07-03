#!/usr/bin/env bash
set -euo pipefail

# Remove generated topology-seed-discussion artifacts without touching tracked fixtures.
# The tool itself remains read-only/stdout-only; this explicit cleanup helper is opt-in.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

remove_if_untracked_file() {
  local path="$1"
  [[ -f "$path" ]] || return 0
  if git -C "$REPO_ROOT" ls-files --error-unmatch "$path" >/dev/null 2>&1; then
    printf 'skip tracked file: %s\n' "$path"
    return 0
  fi
  rm -f -- "$path"
  printf 'removed generated artifact: %s\n' "$path"
}

shopt -s nullglob
for path in \
  "$REPO_ROOT"/.agent/tmp/topology-seed-discussion*.json \
  "$REPO_ROOT"/topology-seed-discussion*.tmp.json \
  "$REPO_ROOT"/topology-seed-discussion*.seed.json; do
  remove_if_untracked_file "$path"
done
