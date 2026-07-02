#!/usr/bin/env bash
# check-structure.sh — local structure check SSOT
# Verifies required directories, files, and architecture-critical content terms.
# Requires only bash. No credentials, no build tools, no business data.
# Exits non-zero on any failure.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0


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

check_dir() {
  local d="$REPO_ROOT/$1"
  if [ -d "$d" ]; then
    echo "OK  [dir]  $1"
  else
    fail "Directory missing: $1"
  fi
}

check_file() {
  local f="$REPO_ROOT/$1"
  if [ -f "$f" ]; then
    echo "OK  [file] $1"
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
    echo "OK  [term] $1 → \"$term\""
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
  echo "OK  [template-clean] $file"
}

check_tmp_runtime_artifacts() {
  local tmp_dir="$REPO_ROOT/.agent/tmp"
  local leftovers
  leftovers="$(find "$tmp_dir" -mindepth 1 \( -type f -o -type d \) ! -path "$tmp_dir/.gitkeep" | sort || true)"
  if [ -n "$leftovers" ]; then
    fail "Runtime tmp artifacts must be cleaned before completion; found: ${leftovers//$'\n'/, }"
  else
    echo "OK  [tmp]  runtime artifacts cleaned (.gitkeep only)"
  fi
}

check_tmp_tracked_files() {
  local tracked
  tracked="$(git -C "$REPO_ROOT" ls-files .agent/tmp)"
  if [ "$tracked" = ".agent/tmp/.gitkeep" ]; then
    echo "OK  [tmp]  tracked files in .agent/tmp are limited to .gitkeep"
  else
    fail "Tracked files under .agent/tmp must be only .agent/tmp/.gitkeep; found: ${tracked:-<none>}"
  fi
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
    echo "OK  [todo] no in-progress marker in .agent/tasks/todo.md"
  fi
}

check_test_proof_manifest_integrity() {
  if bash "$REPO_ROOT/.agent/tests/check-test-proof-manifest-integrity.sh"; then
    echo "OK  [test-proof] manifest integrity"
  else
    fail "test proof manifest integrity failed"
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
    echo "OK  [ssot-map] no inline-annotated pseudo-path entries (.md/.yaml/.sh)"
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

echo ""
echo "=== Directory checks (manifest-driven) ==="
DIR_COUNT=0
while IFS= read -r d; do
  [ -n "$d" ] || continue
  check_dir "$d"
  DIR_COUNT=$((DIR_COUNT + 1))
done < <(manifest_paths required_directories)
if [ "$DIR_COUNT" -lt 25 ]; then
  fail "Manifest required_directories suspiciously small ($DIR_COUNT entries); manifest parse or content broken"
else
  echo "OK  [manifest] $DIR_COUNT required directories checked from manifest"
fi

echo ""
echo "=== File checks (manifest-driven) ==="
FILE_COUNT=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  check_file "$f"
  FILE_COUNT=$((FILE_COUNT + 1))
done < <(manifest_paths required_files)
if [ "$FILE_COUNT" -lt 150 ]; then
  fail "Manifest required_files suspiciously small ($FILE_COUNT entries); manifest parse or content broken"
else
  echo "OK  [manifest] $FILE_COUNT required files checked from manifest"
fi

echo ""
echo "=== Content checks (manifest-driven) ==="
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
  echo "OK  [boundary] cli-mcp-port: email_send declared in boundary"
fi
# email_send must not appear under mcp_surface tools (allowed MCP tools)
if awk '/^  mcp_surface:/,/^  [a-z]/' "$REPO_ROOT/docs/design/cli-model-context-protocols-port-ssot.yaml" | grep -qF "email_send"; then
  fail "cli-model-context-protocols-port-ssot.yaml: email_send must not appear under mcp_surface tools"
else
  echo "OK  [boundary] cli-mcp-port: email_send absent from mcp_surface tools"
fi
# payment_approval must be declared in explicitly_out_of_scope
if ! grep -qF "payment_approval" "$REPO_ROOT/docs/design/cli-model-context-protocols-port-ssot.yaml"; then
  fail "cli-model-context-protocols-port-ssot.yaml: payment_approval must be declared (as out_of_scope boundary)"
else
  echo "OK  [boundary] cli-mcp-port: payment_approval declared in boundary"
fi

if bash "$REPO_ROOT/.agent/tests/check-css-dictionary.sh"; then
  echo "OK  [subcheck] .agent/tests/check-css-dictionary.sh"
else
  fail "Subcheck failed: .agent/tests/check-css-dictionary.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-enum-dictionary.sh"; then
  echo "OK  [subcheck] .agent/tests/check-enum-dictionary.sh"
else
  fail "Subcheck failed: .agent/tests/check-enum-dictionary.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-admin-master-roster.sh"; then
  echo "OK  [subcheck] .agent/tests/check-admin-master-roster.sh"
else
  fail "Subcheck failed: .agent/tests/check-admin-master-roster.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-topology-layout-class-ssot.sh"; then
  echo "OK  [subcheck] .agent/tests/check-topology-layout-class-ssot.sh"
else
  fail "Subcheck failed: .agent/tests/check-topology-layout-class-ssot.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-ui-ux-executable-component-slice.sh"; then
  echo "OK  [subcheck] .agent/tests/check-ui-ux-executable-component-slice.sh"
else
  fail "Subcheck failed: .agent/tests/check-ui-ux-executable-component-slice.sh"
fi

echo ""
echo "=== Template checklist pollution guard ==="
check_checklist_template_clean "$REPO_ROOT/.agent/checklists/policy-judgment.md"
check_checklist_template_clean "$REPO_ROOT/.agent/checklists/boundary-identity.md"

echo ""
echo "=== Checklist self-tests ==="
bash "$REPO_ROOT/.agent/checklists/check-policy-judgment.sh" --self-test
bash "$REPO_ROOT/.agent/checklists/check-boundary-identity.sh" --self-test

TMP_MEMO_PATH="$REPO_ROOT/.agent/tmp/tmp.txt"
if [ -f "$TMP_MEMO_PATH" ]; then
  fail "Temporary scenario contract must be deleted before completion: .agent/tmp/tmp.txt"
else
  echo "OK  [tmp]  .agent/tmp/tmp.txt absent"
fi

check_tmp_runtime_artifacts
check_tmp_tracked_files
check_no_in_progress_todos
check_no_annotated_pseudo_paths_in_ssot_map

# Protocol split guard
if grep -q "## Completion Sequence (Mandatory)" "$REPO_ROOT/.agent/rules/rule.md"; then
  fail "rule.md must not contain long-form Completion Sequence section; keep procedure in .agent/protocols/completion.md"
else
  echo "OK  [split] rule.md completion procedure remains split"
fi



if rg -n "REQUIRED_EXECUTED|required check failure|expected negative test|Governance Gaps|Proposed Governance Improvements|Completion Eligibility" "$REPO_ROOT/.agent/rules/rule.md" >/dev/null; then
  fail "rule.md must remain compact for gate details; move detailed classifications/sections to protocols"
else
  echo "OK  [compact] rule.md gate details remain in protocols"
fi


# SQL Attention target-boundary negative checks
if rg -n "logs\.registry_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_current should not remain in SQL Attention SSOT"
else
  echo "OK  [ssot] registry_current removed from SQL Attention target docs"
fi
if rg -n "registry-neighbor exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry-neighbor exploration should not remain as SQL Attention target"
else
  echo "OK  [ssot] registry-neighbor exploration removed from SQL Attention target docs"
fi
if rg -n "registry_composition_neighbors|registry_composition_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_composition_neighbors/tables should not remain"
else
  echo "OK  [ssot] registry_composition_neighbors/tables should not remain"
fi
if rg -n "scheduler_runtime_registry_neighbor_exploration" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "scheduler_runtime_registry_neighbor_exploration should not remain"
else
  echo "OK  [ssot] scheduler_runtime_registry_neighbor_exploration should not remain"
fi

if rg -n "Initial registry_kind candidates" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "Initial registry_kind candidates should not remain"
else
  echo "OK  [ssot] Initial registry_kind candidates removed"
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
  echo "OK  [ssot] logs.hub_current not in deprecated_or_rejected"
fi
if ! rg -n "physical_tables" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null || ! rg -n "logs\.hub_current" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "physical_tables must include logs.hub_current"
else
  echo "OK  [ssot] physical_tables includes logs.hub_current"
fi
if rg -n "\bregistry_table\b|\bregistry_id\b" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "registry_table/registry_id should not remain in SQL Attention attention contract"
else
  echo "OK  [ssot] registry_table/registry_id removed from SQL Attention attention contract"
fi
if rg -n "\btopologys\b" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.md" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "topologys (naming drift) must not appear in SQL Attention SSOT files; use topology (canonical)"
else
  echo "OK  [ssot] topologys naming drift absent from SQL Attention SSOT files"
fi
if rg -n "^\s+x_y_z:\s+hub_side_record_count_bases" "$REPO_ROOT/docs/design/sql-attention-logs-ssot.yaml" >/dev/null; then
  fail "old Phase Attention axis (x_y_z: hub_side_record_count_bases) must not remain as canonical; use hubs.hub_relations/hubs.hub/hubs.topology_manifests per phase_attention_axis_mapping"
else
  echo "OK  [ssot] old Phase Attention x_y_z canonical axis removed from SQL Attention SSOT"
fi

if ! rg -q "topology_manifest_id" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "hubs.hub_relations must reference topology_manifest_id in db/topology_tables.sql"
else
  echo "OK  [db] hub_relations includes topology_manifest_id FK"
fi
if ! rg -q "UNIQUE \\(topology_manifest_id, sequence_position\\)" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "hubs.hub_relations must enforce UNIQUE(topology_manifest_id, sequence_position)"
else
  echo "OK  [db] hub_relations unique constraint on topology_manifest_id + sequence_position"
fi
if rg -n "hub_relations.*hub_id.*REFERENCES hubs\\.hub \\(hub_id\\)" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "hubs.hub_relations must not use hub_id as source authority column"
else
  echo "OK  [db] hub_relations hub_id source authority column absent"
fi
if awk '/CREATE TABLE hubs\.hub_relations/,/;/' "$REPO_ROOT/db/topology_tables.sql" | rg -n "target_hub_id|relation_registry_id|^\s+hub_id\s" >/dev/null; then
  fail "hubs.hub_relations must not retain hub_id, target_hub_id, or relation_registry_id columns"
else
  echo "OK  [db] hub_relations old global graph columns absent"
fi
if rg -n "DROP TABLE IF EXISTS.*CASCADE" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "db/topology_tables.sql must not contain destructive DROP TABLE ... CASCADE (bootstrap-only CREATE TABLE IF NOT EXISTS required)"
else
  echo "OK  [db] topology_tables.sql destructive DROP TABLE CASCADE absent"
fi
HUB_REL_MIGRATION="$REPO_ROOT/db/migrations/hub_relations_legacy_to_manifest_scoped.sql"
if [ ! -f "$HUB_REL_MIGRATION" ]; then
  fail "hub_relations legacy migration SQL missing: db/migrations/hub_relations_legacy_to_manifest_scoped.sql"
else
  echo "OK  [db] hub_relations legacy migration SQL present"
fi
if rg -n "DROP TABLE IF EXISTS.*CASCADE|DROP TABLE .* CASCADE" "$HUB_REL_MIGRATION" >/dev/null; then
  fail "hub_relations migration must not use DROP TABLE ... CASCADE"
else
  echo "OK  [db] hub_relations migration destructive DROP TABLE CASCADE absent"
fi
if ! rg -q "hub_relations_has_legacy_shape" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must expose legacy shape detection (hub_relations_has_legacy_shape)"
else
  echo "OK  [db] hub_relations legacy shape detection present"
fi
if ! rg -q "RAISE EXCEPTION" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must fail explicitly on ambiguous/unmigratable cases"
else
  echo "OK  [db] hub_relations migration explicit failure paths present"
fi
if ! rg -q "hub_relations_legacy_columns_absent|DROP COLUMN IF EXISTS hub_id" "$HUB_REL_MIGRATION"; then
  fail "hub_relations migration must remove legacy columns and validate absence"
else
  echo "OK  [db] hub_relations migration legacy column removal validated"
fi
if ! rg -q "db/migrations/hub_relations_legacy_to_manifest_scoped.sql" "$REPO_ROOT/db/topology_tables.sql"; then
  fail "db/topology_tables.sql must reference db/migrations/hub_relations_legacy_to_manifest_scoped.sql for legacy DB migration"
else
  echo "OK  [db] topology_tables.sql references hub_relations legacy migration path"
fi
if rg -n "weight.*sequence|sequence.*weight" "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "weight must not remain as sequence authority in topology_tables.sql"
else
  echo "OK  [db] weight is not sequence authority in topology_tables.sql"
fi
if rg -n "hubs\\.hubs|topologys\\." "$REPO_ROOT/db/topology_tables.sql" >/dev/null; then
  fail "non-canonical schema names (hubs.hubs, topologys.*) must not appear in topology_tables.sql"
else
  echo "OK  [db] canonical schema names in topology_tables.sql"
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
echo ""
echo "=== Delegated routing checks ==="
if bash "$REPO_ROOT/.agent/tests/check-worktype-routing.sh"; then
  echo "OK  [subcheck] .agent/tests/check-worktype-routing.sh"
else
  fail "Subcheck failed: .agent/tests/check-worktype-routing.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-docs-ssot-connectivity.sh"; then
  echo "OK  [subcheck] .agent/tests/check-docs-ssot-connectivity.sh"
else
  fail "Subcheck failed: .agent/tests/check-docs-ssot-connectivity.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-ssot-proof-surface-connectivity.sh"; then
  echo "OK  [subcheck] .agent/tests/check-ssot-proof-surface-connectivity.sh"
else
  fail "Subcheck failed: .agent/tests/check-ssot-proof-surface-connectivity.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-no-ruby-dependency.sh"; then
  echo "OK  [subcheck] .agent/tests/check-no-ruby-dependency.sh"
else
  fail "Subcheck failed: .agent/tests/check-no-ruby-dependency.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-system-ci-admin-runtime-callable-ssot.sh"; then
  echo "OK  [subcheck] .agent/tests/check-system-ci-admin-runtime-callable-ssot.sh"
else
  fail "Subcheck failed: .agent/tests/check-system-ci-admin-runtime-callable-ssot.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-runtime-bundle-ssots.sh"; then
  echo "OK  [subcheck] .agent/tests/check-runtime-bundle-ssots.sh"
else
  fail "Subcheck failed: .agent/tests/check-runtime-bundle-ssots.sh"
fi

if bash "$REPO_ROOT/.agent/tests/check-cli-mcp-port-implementation-ssot.sh"; then
  echo "OK  [subcheck] .agent/tests/check-cli-mcp-port-implementation-ssot.sh"
else
  fail "Subcheck failed: .agent/tests/check-cli-mcp-port-implementation-ssot.sh"
fi

check_test_proof_manifest_integrity

if [ "$FAILURES" -eq 0 ]; then
  echo "=== All checks passed ==="
  echo "AGENT_HINT: Final completion summary must use .agent/protocols/completion-summary.md."
  echo "AGENT_HINT: Use .agent/protocols/todo-carry-over.md only when remaining TODO classification is needed."
  echo "AGENT_HINT: PR body or make_pr output does not replace the final completion summary."
  exit 0
else
  echo "=== $FAILURES check(s) failed ===" >&2
  exit 1
fi