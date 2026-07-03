#!/usr/bin/env bash
# check-design-ssot-progress-terms.sh
# Verifies that design SSOTs (docs/design/ and .agent/docs/) outside of
# docs/system-roadmap.yaml and .agent/tasks/todo.md do not contain progress
# management terms that belong exclusively in the Roadmap.
#
# NOTE: Document-level metadata like "status: ssot_design_document" or
# "status: subordinate_design_document" is NOT forbidden — these are
# file-type classifiers, not progress trackers.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
PASS_COUNT=0
VERBOSE="${CHECK_VERBOSE:-0}"

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check_absent() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    return
  fi
  if grep -qF -- "$term" "$file"; then
    fail "Progress term found in $1: \"$term\""
  else
    PASS_COUNT=$((PASS_COUNT + 1))
    if [ "$VERBOSE" = "1" ]; then
      echo "OK  [absent] $1 excludes \"$term\""
    fi
  fi
}

FORBIDDEN_PROGRESS_TERMS=(
  "status: not_started"
  "status: partial"
  "status: implemented"
  "status: production_ready"
  "status: future_bundle_candidate"
  "status: acceptance_pending"
  "status: planned"
  "status: skeleton"
  "owner_status:"
  "core_bundle_status:"
  "implementation_status:"
  "future_implementation_requirements:"
  "assigned_to_design_ssot"
  "covered_by_this_pr"
)

DESIGN_SSOTS=(
  "docs/design/admin-console-workflow-ssot.yaml"
  "docs/design/admin-master-roster-management-ssot.yaml"
  "docs/design/auth-db-session-credential-ssot.yaml"
  "docs/design/cli-mcp-port-implementation-ssot.yaml"
  "docs/design/cli-model-context-protocols-port-ssot.yaml"
  "docs/design/commit-inference-engine.yaml"
  "docs/design/css-dictionary-ssot.yaml"
  "docs/design/enum-dictionary-ssot.yaml"
  "docs/design/extended-runtime-bundle-registry-ssot.yaml"
  "docs/design/external-port-substrate-ssot.yaml"
  "docs/design/mock-preset-intake-compiler-ssot.yaml"
  "docs/design/pipeline-continuity-ssot.yaml"
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"
  "docs/design/runtime-bundle-email-ssot.yaml"
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"
  "docs/design/runtime-bundle-file-storage-ssot.yaml"
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml"
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"
  "docs/design/runtime-bundle-stripe-ssot.yaml"
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml"
  "docs/design/runtime-orchestration-ssot.yaml"
  "docs/design/sql-attention-logs-ssot.yaml"
  "docs/design/team-markdown-dashboard-saved-view-ssot.yaml"
  "docs/design/ui-builder-preset-ecosystem-ssot.yaml"
  "docs/design/user-facing-helper-manual-ssot.yaml"
)

AGENT_SSOTS=(
  ".agent/docs/ssot-map.yaml"
  ".agent/docs/required-paths.yaml"
  ".agent/docs/structure-map.yaml"
)

if [ "$VERBOSE" = "1" ]; then
  echo ""
  echo "=== Design SSOTs: no progress management terms ==="
fi

for f in "${DESIGN_SSOTS[@]}"; do
  for term in "${FORBIDDEN_PROGRESS_TERMS[@]}"; do
    check_absent "$f" "$term"
  done
done

if [ "$VERBOSE" = "1" ]; then
  echo ""
  echo "=== Agent SSOT docs: no progress management terms ==="
fi

for f in "${AGENT_SSOTS[@]}"; do
  for term in "${FORBIDDEN_PROGRESS_TERMS[@]}"; do
    check_absent "$f" "$term"
  done
done

if [ "$VERBOSE" = "1" ]; then
  echo ""
  echo "=== Design SSOTs: no duplicate YAML keys ==="
fi

check_no_duplicate_keys() {
  local file="$REPO_ROOT/$1"
  if [ ! -f "$file" ]; then
    return
  fi
  local result
  result=$(awk '
    # Reset per-file state
    FNR == 1 {
      for (k in seen) delete seen[k]
      dup = ""
    }
    # Skip blank lines and comments
    /^[[:space:]]*$/ || /^[[:space:]]*#/ { next }
    {
      line = $0
      stripped = line
      sub(/^[[:space:]]*/, "", stripped)
      indent = length(line) - length(stripped)
      # List item marker: a new list item opens a new sibling mapping scope.
      # Clear all seen entries deeper than this indent so keys from the
      # previous item do not collide with keys in this new item.
      if (match(stripped, /^-([[:space:]]|$)/)) {
        for (entry in seen) {
          split(entry, parts, SUBSEP)
          if (parts[1] + 0 > indent) delete seen[entry]
        }
        next
      }
      # Match a plain YAML mapping key: identifier chars followed by ":"
      if (match(stripped, /^[a-zA-Z_][a-zA-Z0-9_.-]*[[:space:]]*:/)) {
        key_colon = substr(stripped, 1, RLENGTH)
        sub(/[[:space:]]*:.*/, "", key_colon)
        key = key_colon
        # Clear all seen entries that belong to deeper (child) scopes
        for (entry in seen) {
          split(entry, parts, SUBSEP)
          if (parts[1] + 0 > indent) delete seen[entry]
        }
        sid = indent SUBSEP key
        if (sid in seen) {
          dup = sprintf("FAIL: duplicate key \"%s\" (indent %d) at line %d", key, indent, FNR)
          exit
        }
        seen[sid] = 1
      }
    }
    END { if (dup != "") print dup; else print "OK" }
  ' "$file")
  if [[ "$result" == OK ]]; then
    PASS_COUNT=$((PASS_COUNT + 1))
    if [ "$VERBOSE" = "1" ]; then
      echo "OK  [no-dup-keys] $1"
    fi
  else
    fail "Duplicate YAML key in $1: $result"
  fi
}

for f in "${DESIGN_SSOTS[@]}"; do
  check_no_duplicate_keys "$f"
done

if [ "$VERBOSE" = "1" ]; then echo ""; fi
if [ "$FAILURES" -eq 0 ]; then
  echo "PASS check-design-ssot-progress-terms.sh assertions=${PASS_COUNT}"
  exit 0
else
  echo "=== check-design-ssot-progress-terms.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
