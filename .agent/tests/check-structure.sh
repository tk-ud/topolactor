#!/usr/bin/env bash
# check-structure.sh — local structure check SSOT
# Verifies required directories, files, and architecture-critical content terms.
# Requires only bash. No credentials, no build tools, no business data.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
SUCCESSES=0
VERBOSE="${CHECK_VERBOSE:-0}"


if ! command -v rg >/dev/null 2>&1; then
  rg() {
    local opts=()
    while [ $# -gt 0 ]; do
      case "$1" in
        -n|-q|-s|-i|-v) opts+=("$1"); shift ;;
        -P|-U) shift ;;
        --) shift; break ;;
        -*) shift ;;
        *) break ;;
      esac
    done
    local pattern="${1:-}"
    [ $# -gt 0 ] && shift
    grep -E "${opts[@]}" -- "$pattern" "$@"
  }
fi

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

run_subcheck() {
  local script="$1"
  local tmp
  tmp="$(mktemp)"
  set +e
  CHECK_VERBOSE="$VERBOSE" bash "$REPO_ROOT/$script" >"$tmp" 2>&1
  local code=$?
  set -e
  if [ "$code" -eq 0 ]; then
    SUCCESSES=$((SUCCESSES + 1))
    if [ "$VERBOSE" = "1" ]; then
      cat "$tmp"
    fi
    rm -f "$tmp"
    return 0
  fi
  echo "FAIL subcheck script=$script exit=$code" >&2
  cat "$tmp" >&2
  rm -f "$tmp"
  fail "Subcheck failed: $script"
  return 0
}

check_dir() {
  local d="$REPO_ROOT/$1"
  if [ -d "$d" ]; then
    SUCCESSES=$((SUCCESSES + 1))
  else
    fail "Directory missing: $1"
  fi
}

check_file() {
  local f="$REPO_ROOT/$1"
  if [ -f "$f" ]; then
    SUCCESSES=$((SUCCESSES + 1))
  else
    fail "File missing: $1"
  fi
}

check_content() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    fail "Content check skipped (file missing): $1 — expected term: $term"
    return
  fi
  if grep -qF -- "$term" "$file"; then
    SUCCESSES=$((SUCCESSES + 1))
  else
    fail "Term not found in $1: \"$term\""
  fi
}



check_checklist_template_clean() {
  local file="$1"
  local bad
  bad="$(grep -En '^Answer:[[:space:]]*(yes|no|n/a)[[:space:]]*$' "$file" || true)"
  if [ -n "$bad" ]; then
    fail "$file contains filled Answer values; template must remain blank"
    return
  fi
  SUCCESSES=$((SUCCESSES + 1))
}

tmp_path_kind() {
  local path="$1"
  if [ -d "$REPO_ROOT/$path" ]; then
    echo "directory"
  elif [ -f "$REPO_ROOT/$path" ]; then
    echo "file"
  else
    echo "unknown"
  fi
}

declare -a TMP_VIOLATION_PATHS=()
declare -A TMP_VIOLATIONS_BY_PATH=()
declare -A TMP_KIND_BY_PATH=()

tmp_record_violation() {
  local path="$1"
  local violation="$2"
  local kind="$3"

  if [ -z "${TMP_VIOLATIONS_BY_PATH[$path]+set}" ]; then
    TMP_VIOLATION_PATHS+=("$path")
    TMP_VIOLATIONS_BY_PATH[$path]="$violation"
    TMP_KIND_BY_PATH[$path]="$kind"
    return
  fi

  if [[ ", ${TMP_VIOLATIONS_BY_PATH[$path]}, " != *", $violation, "* ]]; then
    TMP_VIOLATIONS_BY_PATH[$path]="${TMP_VIOLATIONS_BY_PATH[$path]}, $violation"
  fi
  if [ "$kind" = "tracked_file" ]; then
    TMP_KIND_BY_PATH[$path]="$kind"
  fi
}

github_step_summary_available() {
  [ -n "${GITHUB_STEP_SUMMARY:-}" ]
}

append_structure_failure_summary() {
  if ! github_step_summary_available || [ "${#TMP_VIOLATION_PATHS[@]}" -eq 0 ]; then
    return
  fi

  {
    echo "## Structure Check Failure"
    echo ""
    echo "| path | violations | kind | required action | owner |"
    echo "|---|---|---|---|---|"
    local path
    for path in "${TMP_VIOLATION_PATHS[@]}"; do
      printf '| `%s` | %s | %s | final auditor deletes after audit, or moves durable content into SSOT/todo | audit finalization |\n' \
        "$path" \
        "${TMP_VIOLATIONS_BY_PATH[$path]}" \
        "${TMP_KIND_BY_PATH[$path]}"
    done
  } >>"$GITHUB_STEP_SUMMARY"
}

emit_tmp_violation_annotation() {
  local path="$1"
  echo "::error file=$path,title=Non-mergeable .agent/tmp artifact::See Structure Check Failure summary."
}

print_tmp_violation_summary() {
  if [ "${#TMP_VIOLATION_PATHS[@]}" -eq 0 ]; then
    return
  fi

  fail ".agent/tmp contains non-mergeable artifacts"
  local path violation
  for path in "${TMP_VIOLATION_PATHS[@]}"; do
    {
      echo ""
      echo "[structure-check tmp summary]"
      echo "path: $path"
      echo "violations:"
      IFS=',' read -ra violations <<<"${TMP_VIOLATIONS_BY_PATH[$path]}"
      for violation in "${violations[@]}"; do
        violation="${violation# }"
        echo "  - $violation"
      done
      echo "kind: ${TMP_KIND_BY_PATH[$path]}"
      echo "required action: final auditor deletes after audit, or moves durable content into SSOT/todo."
      echo "owner: audit finalization"
    } >&2
    emit_tmp_violation_annotation "$path"
  done

  append_structure_failure_summary
}

check_tmp_runtime_artifacts() {
  local tmp_dir="$REPO_ROOT/.agent/tmp"
  local -a leftovers=()
  local artifact abs_path rel_path kind
  mapfile -t leftovers < <(find "$tmp_dir" -mindepth 1 \( -type f -o -type d \) ! -path "$tmp_dir/.gitkeep" | sort || true)

  if [ "${#leftovers[@]}" -eq 0 ]; then
    SUCCESSES=$((SUCCESSES + 1))
    return
  fi

  for artifact in "${leftovers[@]}"; do
    abs_path="$(cd "$(dirname "$artifact")" && pwd)/$(basename "$artifact")"
    rel_path="${abs_path#$REPO_ROOT/}"
    kind="$(tmp_path_kind "$rel_path")"
    tmp_record_violation "$rel_path" "runtime_leftover" "$kind"
  done
}

check_tmp_tracked_files() {
  local -a tracked=()
  local -a invalid_tracked=()
  local path
  mapfile -t tracked < <(git -C "$REPO_ROOT" ls-files .agent/tmp)

  for path in "${tracked[@]}"; do
    if [ "$path" != ".agent/tmp/.gitkeep" ]; then
      invalid_tracked+=("$path")
    fi
  done

  if [ "${#invalid_tracked[@]}" -eq 0 ]; then
    SUCCESSES=$((SUCCESSES + 1))
    return
  fi

  for path in "${invalid_tracked[@]}"; do
    tmp_record_violation "$path" "tracked_tmp_file" "tracked_file"
  done
}

report_tmp_violations() {
  print_tmp_violation_summary
}

check_no_in_progress_todos() {
  local todo_file="$REPO_ROOT/.agent/tasks/todo.md"
  if [ ! -f "$todo_file" ]; then
    fail "TODO marker check skipped (file missing): .agent/tasks/todo.md"
    return
  fi
  if rg -n "^\s*<!--\s*agent:in-progress\s*-->\s*$" "$todo_file" >/dev/null; then
    fail ".agent/tasks/todo.md contains unfinished in-progress marker: <!-- agent:in-progress -->"
  else
    SUCCESSES=$((SUCCESSES + 1))
  fi
}

check_no_annotated_pseudo_paths_in_ssot_map() {
  local ssot_map="$REPO_ROOT/.agent/docs/ssot-map.yaml"
  if [ ! -f "$ssot_map" ]; then
    fail "ssot-map path annotation check skipped (file missing): .agent/docs/ssot-map.yaml"
    return
  fi
  if rg -n '^[[:space:]]*-[[:space:]]+[^#]*\.(md|yaml|sh)[[:space:]]{2,}[^#]+' "$ssot_map" >/dev/null; then
    fail ".agent/docs/ssot-map.yaml contains inline-annotated pseudo-path entries (.md/.yaml/.sh path + trailing description)"
  else
    SUCCESSES=$((SUCCESSES + 1))
  fi
}

# ─── Manifest-driven required paths and content terms ────────────────────────
# Enumeration SSOT: .agent/docs/required-paths.yaml
# This script is the check executor/orchestrator; the manifest owns the
# required directory / required file / required content term enumerations.
# Complex guards (forbidden terms, drift guards, DB-specific structural checks)
# intentionally stay below as explicit shell checks.

MANIFEST="$REPO_ROOT/.agent/docs/required-paths.yaml"
if [ ! -f "$MANIFEST" ]; then
  fail "Manifest missing: .agent/docs/required-paths.yaml (enumeration SSOT for structure checks)"
fi

manifest_paths() {
  # $1: top-level section name (required_directories | required_files)
  awk -v section="$1" '
    { sub(/\r$/, "") }
    /^[^[:space:]]/ { in_section = ($0 == section ":"); next }
    in_section && /^    - / { line = $0; sub(/^    - /, "", line); sub(/[[:space:]]+$/, "", line); print line }
  ' "$MANIFEST"
}

manifest_content_terms() {
  # Emits tab-separated "<file>\t<term>" pairs from required_content_terms.
  awk '
    { sub(/\r$/, "") }
    /^[^[:space:]]/ { in_section = ($0 == "required_content_terms:"); next }
    !in_section { next }
    /^  [^[:space:]].*:[[:space:]]*$/ { file = $0; sub(/^  /, "", file); sub(/:[[:space:]]*$/, "", file); next }
    /^    - "/ {
      term = $0
      sub(/^    - "/, "", term)
      sub(/"[[:space:]]*$/, "", term)
      printf "%s\t%s\n", file, term
    }
  ' "$MANIFEST"
}

DIR_COUNT=0
while IFS= read -r d; do
  [ -n "$d" ] || continue
  check_dir "$d"
  DIR_COUNT=$((DIR_COUNT + 1))
done < <(manifest_paths required_directories)
if [ "$DIR_COUNT" -lt 25 ]; then
  fail "Manifest required_directories suspiciously small ($DIR_COUNT entries); manifest parse or content broken"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

FILE_COUNT=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  check_file "$f"
  FILE_COUNT=$((FILE_COUNT + 1))
done < <(manifest_paths required_files)
if [ "$FILE_COUNT" -lt 150 ]; then
  fail "Manifest required_files suspiciously small ($FILE_COUNT entries); manifest parse or content broken"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

TERM_COUNT=0
while IFS=$'\t' read -r f term; do
  [ -n "$f" ] || continue
  [ -n "$term" ] || continue
  check_content "$f" "$term"
  TERM_COUNT=$((TERM_COUNT + 1))
done < <(manifest_content_terms)
if [ "$TERM_COUNT" -lt 250 ]; then
  fail "Manifest required_content_terms suspiciously small ($TERM_COUNT terms); manifest parse or content broken"
fi

# CLI/MCP port out-of-scope boundary guard: email_send must be declared in explicitly_out_of_scope
if ! grep -qF "email_send" "$REPO_ROOT/docs/design/cli-model-context-protocols-port-ssot.yaml"; then
  fail "cli-model-context-protocols-port-ssot.yaml: email_send must be declared (as out_of_scope boundary)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
# email_send must not appear under mcp_surface tools (allowed MCP tools)
if awk '/^  mcp_surface:/,/^  [a-z]/' "$REPO_ROOT/docs/design/cli-model-context-protocols-port-ssot.yaml" | grep -qF "email_send"; then
  fail "cli-model-context-protocols-port-ssot.yaml: email_send must not appear under mcp_surface tools"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
# payment_approval must be declared in explicitly_out_of_scope
if ! grep -qF "payment_approval" "$REPO_ROOT/docs/design/cli-model-context-protocols-port-ssot.yaml"; then
  fail "cli-model-context-protocols-port-ssot.yaml: payment_approval must be declared (as out_of_scope boundary)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

run_subcheck ".agent/tests/check-enum-dictionary.sh"

run_subcheck ".agent/tests/check-admin-master-roster.sh"

run_subcheck ".agent/tests/check-topology-layout-class-ssot.sh"

run_subcheck ".agent/tests/check-admin-uibuilder-wiring.sh"

check_checklist_template_clean "$REPO_ROOT/.agent/checklists/policy-judgment.md"
check_checklist_template_clean "$REPO_ROOT/.agent/checklists/boundary-identity.md"

run_command_subcheck() {
  local label="$1"; shift
  local tmp
  tmp="$(mktemp)"
  set +e
  "$@" >"$tmp" 2>&1
  local code=$?
  set -e
  if [ "$code" -eq 0 ]; then
    SUCCESSES=$((SUCCESSES + 1))
    if [ "$VERBOSE" = "1" ]; then
      cat "$tmp"
    fi
    rm -f "$tmp"
    return 0
  fi
  echo "FAIL subcheck label=$label exit=$code command=$*" >&2
  cat "$tmp" >&2
  rm -f "$tmp"
  fail "Subcheck failed: $label"
}

run_command_subcheck "policy-judgment-self-test" bash "$REPO_ROOT/.agent/checklists/check-policy-judgment.sh" --self-test
run_command_subcheck "boundary-identity-self-test" bash "$REPO_ROOT/.agent/checklists/check-boundary-identity.sh" --self-test

TMP_MEMO_PATH="$REPO_ROOT/.agent/tmp/tmp.txt"
if [ -f "$TMP_MEMO_PATH" ]; then
  fail "Temporary scenario contract must be deleted before completion: .agent/tmp/tmp.txt"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

check_tmp_runtime_artifacts
check_tmp_tracked_files
report_tmp_violations
check_no_in_progress_todos
check_no_annotated_pseudo_paths_in_ssot_map

# Protocol split guard
if grep -q "## Completion Sequence (Mandatory)" "$REPO_ROOT/.agent/rules/rule.md"; then
  fail "rule.md must not contain long-form Completion Sequence section; keep procedure in .agent/protocols/completion.md"
else
  SUCCESSES=$((SUCCESSES + 1))
fi



if rg -n "REQUIRED_EXECUTED|required check failure|expected negative test|Governance Gaps|Proposed Governance Improvements|Completion Eligibility" "$REPO_ROOT/.agent/rules/rule.md" >/dev/null; then
  fail "rule.md must remain compact for gate details; move detailed classifications/sections to protocols"
else
  SUCCESSES=$((SUCCESSES + 1))
fi


# SQL Attention target-boundary negative checks
if rg -n "logs\.registry_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_current should not remain in SQL Attention SSOT"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "registry-neighbor exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry-neighbor exploration should not remain as SQL Attention target"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "registry_composition_neighbors|registry_composition_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_composition_neighbors/tables should not remain"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "scheduler_runtime_registry_neighbor_exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "scheduler_runtime_registry_neighbor_exploration should not remain"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

if rg -n "Initial registry_kind candidates" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "Initial registry_kind candidates should not remain"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if awk '
  BEGIN { in_dep=0; found=0 }
  /^deprecated_or_rejected:/ { in_dep=1; next }
  in_dep && /^[^[:space:]]/ { in_dep=0 }
  in_dep && /logs\.hub_current/ { found=1 }
  END { exit found ? 0 : 1 }
' "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml"; then
  fail "logs.hub_current must not be in deprecated_or_rejected"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -n "physical_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null || ! rg -n "logs\.hub_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "physical_tables must include logs.hub_current"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "\bregistry_table\b|\bregistry_id\b" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_table/registry_id should not remain in SQL Attention attention contract"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "\btopologys\b" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "topologys (naming drift) must not appear in SQL Attention SSOT files; use topology (canonical)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "^\s+x_y_z:\s+hub_side_record_count_bases" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "old Phase Attention axis (x_y_z: hub_side_record_count_bases) must not remain as canonical; use hubs.hub_relations/hubs.hub/hubs.topology_manifests per phase_attention_axis_mapping"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

if ! rg -q "topology_manifest_id" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "hubs.hub_relations must reference topology_manifest_id in db/topology_tables.sql"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -q "UNIQUE \\(topology_manifest_id, sequence_position\\)" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "hubs.hub_relations must enforce UNIQUE(topology_manifest_id, sequence_position)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "hub_relations.*hub_id.*REFERENCES hubs\\.hub \\(hub_id\\)" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "hubs.hub_relations must not use hub_id as source authority column"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if awk '/CREATE TABLE hubs\.hub_relations/,/;/' "$REPO_ROOT/db/topology_tables.sql" | rg -n "target_hub_id|relation_registry_id|^\s+hub_id\s" >/dev/null; then
  fail "hubs.hub_relations must not retain hub_id, target_hub_id, or relation_registry_id columns"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "DROP TABLE IF EXISTS.*CASCADE" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "db/topology_tables.sql must not contain destructive DROP TABLE ... CASCADE (bootstrap-only CREATE TABLE IF NOT EXISTS required)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
HUB_REL_MIGRATION="$REPO_ROOT/db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql"
if [ ! -f "$HUB_REL_MIGRATION" ]; then
  fail "hub_relations legacy migration SQL missing: db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "DROP TABLE IF EXISTS.*CASCADE|DROP TABLE .* CASCADE" "$HUB_REL_MIGRATION" >/dev/null; then
  fail "hub_relations migration must not use DROP TABLE ... CASCADE"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -q "hub_relations_has_legacy_shape" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must expose legacy shape detection (hub_relations_has_legacy_shape)"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -q "RAISE EXCEPTION" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must fail explicitly on ambiguous/unmigratable cases"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -q "hub_relations_legacy_columns_absent|DROP COLUMN IF EXISTS hub_id" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must remove legacy columns and validate absence"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if ! rg -q "db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "db/topology_tables.sql must reference db/legacy_utils/hub_relations_legacy_to_manifest_scoped.sql for legacy DB migration"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "weight.*sequence|sequence.*weight" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "weight must not remain as sequence authority in topology_tables.sql"
else
  SUCCESSES=$((SUCCESSES + 1))
fi
if rg -n "hubs\\.hubs|topologys\\." "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "non-canonical schema names (hubs.hubs, topologys.*) must not appear in topology_tables.sql"
else
  SUCCESSES=$((SUCCESSES + 1))
fi

# ─── Result ───────────────────────────────────────────────────────────────────

run_subcheck ".agent/tests/check-worktype-routing.sh"

run_subcheck ".agent/tests/check-docs-ssot-connectivity.sh"

run_subcheck ".agent/tests/check-ssot-proof-surface-connectivity.sh"

run_subcheck ".agent/tests/check-agent-ui-protocol-ssot.sh"

run_subcheck ".agent/tests/check-no-ruby-dependency.sh"

run_subcheck ".agent/tests/check-agent-tools-surface.sh"

run_subcheck ".agent/tests/check-topology-seed-discussion-bits-length.sh"

run_subcheck ".agent/tests/check-system-ci-admin-runtime-callable-ssot.sh"

run_subcheck ".agent/tests/check-runtime-bundle-ssots.sh"

run_subcheck ".agent/tests/check-cli-mcp-port-implementation-ssot.sh"

run_subcheck ".agent/tests/check-scheduler-job-manifest-ssot.sh"

run_subcheck ".agent/tests/check-design-ssot-progress-terms.sh"

run_subcheck ".agent/tests/check-yaml-parse-completeness.sh"

if [ "$FAILURES" -eq 0 ]; then
    echo "PASS check-structure dirs=${DIR_COUNT:-0} files=${FILE_COUNT:-0} content_terms=${TERM_COUNT:-0} success_assertions=${SUCCESSES}"
  exit 0
else
  echo "=== $FAILURES check(s) failed ===" >&2
  exit 1
fi
