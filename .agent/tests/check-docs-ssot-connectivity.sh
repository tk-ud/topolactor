#!/usr/bin/env bash
# check-docs-ssot-connectivity.sh — docs/ SSOT connectivity gate
#
# Responsibility (structural connection checks only; NOT semantic judgment):
#   1. Enumerate docs/ via .agent/scripts/emit-docs-tree-json.sh (JSON observation input)
#      and verify every is_ssot=true entry is explicitly referenced by at least one
#      agent-side index / route / test / prompt / protocol / workflow surface.
#   2. Reverse direction: detect orphan workflow candidates (workflow yml unreachable
#      from the required-paths manifest, check-structure.sh, or local-ci) and FAIL on
#      workflow-referenced .agent/**.sh scripts that do not exist.
#   3. Detect orphan test candidates (.agent/tests/check-*.sh touching no governance
#      surface) outside an explicit, reason-commented allowlist.
#
# Boundary notes:
#   - docs/system-roadmap.yaml and .agent/tasks/todo.md are dynamic reference points,
#     not implementation-state authority. The emitter marks system-roadmap as
#     is_dynamic_reference=true / is_ssot=false and this gate asserts that boundary.
#   - This gate verifies reference existence only. It does not judge whether a
#     mapped read set is semantically sufficient.
#
# Dependencies: bash + find/grep/sed/awk only (no jq/python/node).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

FAILURES=0
PASS_COUNT=0
fail() { echo "FAIL: $1" >&2; FAILURES=$((FAILURES + 1)); }
ok()   { PASS_COUNT=$((PASS_COUNT + 1)); }

TREE_JSON="$(mktemp)"
cleanup() { rm -f "$TREE_JSON"; }
trap cleanup EXIT

# ─── 1. Observe docs tree as JSON ─────────────────────────────────────────────

bash .agent/scripts/emit-docs-tree-json.sh --root docs --output "$TREE_JSON"

TOTAL_ENTRIES="$(grep -c '"path":' "$TREE_JSON" || true)"
if [ "$TOTAL_ENTRIES" -lt 30 ]; then
  fail "docs tree JSON observation suspiciously small ($TOTAL_ENTRIES entries); emitter or docs tree broken"
else
  ok "[tree] docs tree JSON observed ($TOTAL_ENTRIES entries)"
fi

# Extract is_ssot=true paths from the JSON observation (stable one-object-per-line format).
extract_paths() {
  # $1: flag name (is_ssot | is_dynamic_reference)
  awk -v flag="$1" '
    index($0, "\"" flag "\":true") {
      if (match($0, /"path":"[^"]*"/)) {
        p = substr($0, RSTART + 8, RLENGTH - 9)
        print p
      }
    }
  ' "$TREE_JSON"
}

SSOT_PATHS="$(extract_paths is_ssot)"
SSOT_COUNT="$(printf '%s\n' "$SSOT_PATHS" | grep -c . || true)"
if [ "$SSOT_COUNT" -lt 10 ]; then
  fail "docs SSOT candidate count suspiciously small ($SSOT_COUNT); is_ssot classification broken"
else
  ok "[tree] $SSOT_COUNT docs SSOT candidates classified from JSON"
fi

# Dynamic reference boundary assertion: system-roadmap must never be classified as SSOT.
if printf '%s\n' "$SSOT_PATHS" | grep -qx "docs/system-roadmap.yaml"; then
  fail "docs/system-roadmap.yaml must be a dynamic reference point, not an SSOT candidate"
else
  ok "[boundary] docs/system-roadmap.yaml stays dynamic reference (not implementation SSOT)"
fi
if ! extract_paths is_dynamic_reference | grep -qx "docs/system-roadmap.yaml"; then
  fail "docs/system-roadmap.yaml must be observed with is_dynamic_reference=true"
else
  ok "[boundary] docs/system-roadmap.yaml observed as is_dynamic_reference=true"
fi

# ─── 2. Forward connectivity: docs SSOT -> agent/CI surfaces ─────────────────

# Surfaces that count as explicit connection points for a docs/ SSOT.
CONNECTION_SURFACES=(
  .agent/docs/ssot-map.yaml
  .agent/docs/required-paths.yaml
  .agent/docs/test-bundles.yaml
  .agent/routes/worktype-required-protocols.yaml
)
while IFS= read -r f; do CONNECTION_SURFACES+=("$f"); done < <(find .agent/tests -maxdepth 1 -name '*.sh' | sort)
while IFS= read -r f; do CONNECTION_SURFACES+=("$f"); done < <(find .agent/prompt -maxdepth 1 -name '*.md' | sort)
while IFS= read -r f; do CONNECTION_SURFACES+=("$f"); done < <(find .agent/protocols -maxdepth 1 -name '*.md' | sort)
while IFS= read -r f; do CONNECTION_SURFACES+=("$f"); done < <(find .github/workflows -maxdepth 1 -name '*.yml' | sort)

# Allowlist for intentionally unconnected SSOT candidates.
# Keep this empty unless a docs/ SSOT legitimately must not appear in any agent
# index/test/prompt/protocol/workflow surface; record the reason as a comment.
UNCONNECTED_SSOT_ALLOWLIST=()

is_allowlisted_ssot() {
  local p="$1" a
  for a in ${UNCONNECTED_SSOT_ALLOWLIST[@]+"${UNCONNECTED_SSOT_ALLOWLIST[@]}"}; do
    [ "$a" = "$p" ] && return 0
  done
  return 1
}

while IFS= read -r ssot; do
  [ -n "$ssot" ] || continue
  if grep -qF -- "$ssot" "${CONNECTION_SURFACES[@]}" 2>/dev/null; then
    ok "[ssot-connected] $ssot"
  elif is_allowlisted_ssot "$ssot"; then
    ok "[ssot-allowlisted] $ssot (explicit allowlist entry)"
  else
    fail "unconnected docs SSOT: $ssot is not referenced by any of ssot-map / required-paths / test-bundles / routes / .agent/tests / .agent/prompt / .agent/protocols / .github/workflows"
  fi
done <<< "$SSOT_PATHS"

# ─── 3. Reverse direction: workflow reachability + script existence ──────────

WORKFLOW_ANCHOR_SURFACES=(
  .agent/docs/required-paths.yaml
  .agent/tests/check-structure.sh
  .agent/tests/check-local-ci.sh
)

for wf in .github/workflows/*.yml; do
  if grep -qF -- "$wf" "${WORKFLOW_ANCHOR_SURFACES[@]}" 2>/dev/null; then
    ok "[workflow-reachable] $wf"
  else
    fail "orphan workflow candidate: $wf is not referenced by required-paths manifest, check-structure.sh, or check-local-ci.sh"
  fi

  # Every .agent/**.sh referenced from a workflow must exist.
  while IFS= read -r script; do
    [ -n "$script" ] || continue
    if [ -f "$script" ]; then
      ok "[workflow-script] $wf -> $script"
    else
      fail "workflow $wf references missing script: $script"
    fi
  done < <(grep -oE '\.agent/[A-Za-z0-9_./-]+\.sh' "$wf" | sort -u)
done

# ─── 4. Reverse direction: orphan test candidates ─────────────────────────────

# Allowlisted execution wrappers: these tests intentionally touch only
# code/db/infra execution surfaces (dotnet/deno/psql/docker) instead of
# docs//.agent governance surfaces. They are runners, not governance checks.
ORPHAN_TEST_ALLOWLIST=(
  .agent/tests/check-backend-tests.sh                       # dotnet test runtime wrapper
  .agent/tests/check-frontend-types.sh                      # deno check runtime wrapper
  .agent/tests/check-frontend-all-tests.sh                  # deno test runtime wrapper
  .agent/tests/check-default-entity-search.sh               # dotnet+deno integration runner
  .agent/tests/check-bootstrap-validation.sh                # db/infra bootstrap execution wrapper
  .agent/tests/check-db-schema.sh                           # db schema execution wrapper
  .agent/tests/check-migration-ui-topology.sh               # db migration execution wrapper
  .agent/tests/check-projection-lane-seed-hardening.sh      # backend/frontend lane test runner
  .agent/tests/check-recommendation-pressure-lane-boundary.sh # code-surface lane boundary runner
  .agent/tests/check-runtime-environment.sh                 # docker/compose environment wrapper
  .agent/tests/check-runtime-semantics.sh                   # runtime semantics test runner
  .agent/tests/check-local-ci.sh                            # local-ci orchestrator (delegates to other checks)
)

is_allowlisted_orphan_test() {
  local p="$1" a
  for a in "${ORPHAN_TEST_ALLOWLIST[@]}"; do
    [ "$a" = "$p" ] && return 0
  done
  return 1
}

for t in .agent/tests/check-*.sh; do
  if grep -qE 'docs/|\.agent/docs/|\.agent/routes/|\.agent/protocols/|\.agent/tasks/' "$t"; then
    ok "[test-connected] $t"
  elif is_allowlisted_orphan_test "$t"; then
    ok "[test-wrapper] $t (allowlisted execution wrapper)"
  else
    fail "orphan test candidate: $t touches no docs//.agent governance surface and is not an allowlisted execution wrapper"
  fi
done

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS check-docs-ssot-connectivity.sh assertions=${PASS_COUNT}"
  exit 0
else
  echo "=== $FAILURES docs SSOT connectivity check(s) failed ===" >&2
  exit 1
fi
