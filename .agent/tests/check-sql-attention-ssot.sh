#!/usr/bin/env bash
set -euo pipefail
PASS_COUNT=0

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

SSOT_YAML="docs/design/sql-attention-logs-ssot.yaml"
SSOT_MD="docs/design/sql-attention-logs-ssot.md"
ROADMAP="docs/system-roadmap.yaml"
TODO_FILE=".agent/tasks/todo.md"

# Structured processing (YAML load + structural assertions) is delegated to a
# Python3 stdlib script; repo governance tooling structured processing must
# use Python3 stdlib only (Ruby is prohibited — see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
python3 "$REPO_ROOT/.agent/scripts/check_sql_attention_ssot_yaml.py"

for f in "$SSOT_MD" "$TODO_FILE"; do
  [ -f "$f" ] || { echo "FAIL: missing file $f" >&2; exit 1; }
done



find_pattern_matches() {
  local pattern="$1"
  local target="$2"

  if command -v rg >/dev/null 2>&1; then
    rg -n -i -e "$pattern" "$target"
  else
    grep -n -i -E "$pattern" "$target"
  fi
}

for ssot_file in "$SSOT_YAML" "$SSOT_MD"; do
  pattern='out_of_scope_not_implemented|future_migration_task|this[ _-]?pr|this PR|not implemented|already implemented|current implementation|\bimplemented\b|\bpartial\b|\bskeleton\b|\bTODO\b|known_gap|roadmap'
  if find_pattern_matches "$pattern" "$ssot_file" >/dev/null; then
    echo "FAIL: progress/status vocabulary detected in SSOT file: $ssot_file" >&2
    find_pattern_matches "$pattern" "$ssot_file" >&2
    exit 1
  fi
done
PASS_COUNT=$((PASS_COUNT + 1))

logs_repo_boundary_present=0
npgsql_boundary_present=0
scheduler_empty_hits_guard_present=0
scheduler_write_call_present=0
insert_boundary_present=0

grep -qF "public virtual Task<int> WriteLogsAttentionAsync(" backend/repository/SqlAttentionLogsRepository.cs && logs_repo_boundary_present=1
grep -qF "public override async Task<int> WriteLogsAttentionAsync(" backend/repository/NpgsqlSqlAttentionLogsRepository.cs && npgsql_boundary_present=1
grep -qF "explorationResult.Hits.Count == 0" backend/scheduler/SqlAttentionScheduler.cs && scheduler_empty_hits_guard_present=1
grep -qF "_sqlAttentionLogsRepository.AppendAttentionGenerationAsync(" backend/scheduler/SqlAttentionScheduler.cs && scheduler_write_call_present=1
grep -qF "INSERT INTO logs.attention" backend/repository/NpgsqlSqlAttentionLogsRepository.cs && insert_boundary_present=1

[[ "$logs_repo_boundary_present" -eq 1 ]] || { echo "FAIL: missing SqlAttentionLogsRepository.WriteLogsAttentionAsync boundary" >&2; exit 1; }
[[ "$npgsql_boundary_present" -eq 1 ]] || { echo "FAIL: missing NpgsqlSqlAttentionLogsRepository.WriteLogsAttentionAsync boundary" >&2; exit 1; }
[[ "$scheduler_empty_hits_guard_present" -eq 1 ]] || { echo "FAIL: scheduler missing empty-hits guard before write" >&2; exit 1; }
[[ "$scheduler_write_call_present" -eq 1 ]] || { echo "FAIL: scheduler missing append SQLAT -> phaseAT generation call" >&2; exit 1; }
[[ "$insert_boundary_present" -eq 1 ]] || { echo "FAIL: logs.attention INSERT boundary missing" >&2; exit 1; }

if find_pattern_matches "UPDATE\\s+logs\\.attention|DELETE\\s+FROM\\s+logs\\.attention" backend/repository/NpgsqlSqlAttentionLogsRepository.cs >/dev/null; then
  echo "FAIL: logs.attention append-only violated by UPDATE/DELETE" >&2
  exit 1
fi
grep -qF "ArchivePolicy must be 'required'" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: archive_policy required enforcement missing" >&2; exit 1; }
grep -qF "ArchivePolicy must be 'required'" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: archive_policy required enforcement missing in Npgsql repo" >&2; exit 1; }
grep -qF "CurrentId must not be empty" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: current_id required boundary missing" >&2; exit 1; }
grep -qF "HubCurrentId must not be empty" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: hub_current_id required boundary missing" >&2; exit 1; }
grep -qF "resolve_related_topology_manifests" db/sql_attention_logs_tables.sql || { echo "FAIL: explicit related topology manifest resolver missing" >&2; exit 1; }
grep -qF "topology.physical_table_manifest_bindings" db/topology_tables.sql || { echo "FAIL: explicit physical table manifest binding missing" >&2; exit 1; }
grep -qF "LoadHubRelationExplorationCandidatesAsync" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: canonical hubs.hub_relations candidate loader missing" >&2; exit 1; }
grep -qF "source_attention_id" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: append-only generation lineage source_attention_id missing" >&2; exit 1; }
grep -qF "draft_projection" backend/runtime/SqlAttentionEvidencePromotionRuntime.cs || { echo "FAIL: explicit Draft promotion runtime missing" >&2; exit 1; }
PASS_COUNT=$((PASS_COUNT + 1)) # OK: write_logs_attention and canonical generation-line implementation boundary checks passed

# ---------------------------------------------------------------------------
# manifest_topology_key_expansion_draft_lane guards
# SQL-only evidence consumer: SQLAT evidence -> Key extraction (neighborhood only)
# -> full-space manifest topology expansion -> insert-only draft candidate JSONB
# + Markdown projection -> structured DB NOTIFY. C# is orchestration / bridge only.
# ---------------------------------------------------------------------------
DRAFT_SQL="db/sql_attention_logs_tables.sql"

# SSOT contains the draft lane definition
grep -qF "manifest_topology_key_expansion_draft_lane" "$SSOT_YAML" || { echo "FAIL: SSOT yaml missing manifest_topology_key_expansion_draft_lane" >&2; exit 1; }
grep -qF "SQL Attention manifest topology key expansion draft lane" "$SSOT_MD" || { echo "FAIL: SSOT md missing draft lane section" >&2; exit 1; }

# Insert-only authority + markdown projection tables exist
grep -qF "CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_candidate" "$DRAFT_SQL" || { echo "FAIL: draft candidate authority table missing" >&2; exit 1; }
grep -qF "CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_markdown_projection" "$DRAFT_SQL" || { echo "FAIL: draft markdown projection table missing" >&2; exit 1; }

# Authority constants enforced
grep -qF "CHECK (source = 'sql_attention')" "$DRAFT_SQL" || { echo "FAIL: source=sql_attention CHECK missing" >&2; exit 1; }
grep -qF "CHECK (candidate_lane = 'manifest_topology_key_expansion_draft_lane')" "$DRAFT_SQL" || { echo "FAIL: candidate_lane CHECK missing" >&2; exit 1; }
grep -qF "CHECK (status = 'draft')" "$DRAFT_SQL" || { echo "FAIL: status=draft CHECK missing" >&2; exit 1; }

# Required draft payload fields
for field in source_evidence_refs high_pressure_key hit_manifest_refs hit_table_refs common_axis_candidates candidate_columns candidate_payload_json; do
  grep -qF "$field" "$DRAFT_SQL" || { echo "FAIL: draft payload field missing: $field" >&2; exit 1; }
done

# SQL function family present
for fn in extract_sql_attention_high_pressure_keys compile_sql_attention_manifest_topology_draft_candidates insert_sql_attention_draft_candidate run_sql_attention_manifest_topology_key_expansion_draft_lane notify_sql_attention_draft_candidate_created render_sql_attention_draft_candidate_markdown; do
  grep -qF "logs.$fn" "$DRAFT_SQL" || { echo "FAIL: SQL draft lane function missing: logs.$fn" >&2; exit 1; }
done

# Key extraction depends on SQLAT evidence (no schema mining without evidence)
grep -qF "a.evidence_kind = 'sql_attention_hit'" "$DRAFT_SQL" || { echo "FAIL: key extraction not bound to SQLAT evidence" >&2; exit 1; }

# Full-space expansion: compile must search the full active manifest topology space (not neighborhood-only completion)
grep -qF "FROM hubs.topology_manifests tm" "$DRAFT_SQL" || { echo "FAIL: compile must search full manifest topology space" >&2; exit 1; }
grep -qF "topology.schema_registry" "$DRAFT_SQL" || { echo "FAIL: compile must search logical table/column space (schema_registry)" >&2; exit 1; }
grep -qF "enum.groups" "$DRAFT_SQL" || { echo "FAIL: compile must search enum group/discrete metadata" >&2; exit 1; }

# Scoring is not raw count: dampening + lift + id-axis handling are present and data-defined
grep -qF "logs.resolve_sql_attention_key_expansion_policy" "$DRAFT_SQL" || { echo "FAIL: draft lane scoring policy resolver missing (data-defined scoring)" >&2; exit 1; }
grep -qF "routine_value_dampen_factor" "$DRAFT_SQL" || { echo "FAIL: routine high-frequency value dampening missing" >&2; exit 1; }
grep -qF "generic_column_dampen_factor" "$DRAFT_SQL" || { echo "FAIL: generic column dampening missing" >&2; exit 1; }
grep -qF "'raw_count_only', false" "$DRAFT_SQL" || { echo "FAIL: scoring must not be raw count only" >&2; exit 1; }
grep -qF "id_axis_treated_as_dimension" "$DRAFT_SQL" || { echo "FAIL: ID column axis/dimension handling missing" >&2; exit 1; }
grep -qF "SQL Attention key expansion policy missing" "$DRAFT_SQL" || { echo "FAIL: draft lane policy fail-close missing" >&2; exit 1; }
grep -qF "'sql_attention_manifest_topology_key_expansion'" db/seed_empty.sql || { echo "FAIL: draft lane scoring policy seed missing" >&2; exit 1; }

# Insert-only: no UPDATE/DELETE on draft tables, no UPDATE/DELETE of consumed logs.attention
if find_pattern_matches "UPDATE\\s+logs\\.sql_attention_draft|DELETE\\s+FROM\\s+logs\\.sql_attention_draft" "$DRAFT_SQL" >/dev/null; then
  echo "FAIL: draft candidate / markdown projection tables must be insert-only" >&2; exit 1
fi

# No auto-apply / auto-promote / active topology mutation from the draft lane
if find_pattern_matches "UPDATE\\s+hubs\\.topology_manifests|UPDATE\\s+hubs\\.hub_relations|INSERT\\s+INTO\\s+topology\\.structure_maps|UPDATE\\s+topology\\.structure_maps" "$DRAFT_SQL" >/dev/null; then
  echo "FAIL: draft lane must not mutate active manifest / hub_relation / structure_map (no auto-apply/auto-promote)" >&2; exit 1
fi

# Markdown is human-readable only: SQL must not generate HTML / island markup / CSS class authority / script
if find_pattern_matches "<script|<div|<span|<iframe|class=" "$DRAFT_SQL" >/dev/null; then
  echo "FAIL: SQL must not generate HTML / island markup / CSS class authority" >&2; exit 1
fi

# Structured NOTIFY payload + dedicated channel
grep -qF "'event_type', 'sql_attention_draft_candidate_created'" "$DRAFT_SQL" || { echo "FAIL: structured notify event_type missing" >&2; exit 1; }
grep -qF "pg_notify('sql_attention_draft_candidate'" "$DRAFT_SQL" || { echo "FAIL: dedicated NOTIFY channel missing" >&2; exit 1; }
grep -qF "'markdown_projection_id', NEW.markdown_projection_id" "$DRAFT_SQL" || { echo "FAIL: notify payload missing markdown_projection_id" >&2; exit 1; }

# C# is orchestration / bridge only — candidate inference is SQL (no scoring/heuristic in scheduler)
grep -qF "CompileManifestTopologyDraftCandidatesAsync" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: repository draft lane bridge missing" >&2; exit 1; }
grep -qF "run_sql_attention_manifest_topology_key_expansion_draft_lane" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: Npgsql draft lane invocation missing" >&2; exit 1; }
grep -qF "RunDraftLaneAsync" backend/scheduler/SqlAttentionScheduler.cs || { echo "FAIL: scheduler draft lane orchestration missing" >&2; exit 1; }
# scheduler must not contain candidate scoring heuristics
if grep -qiE "score_weights|routine_value_dampen|high_pressure_key\s*=" backend/scheduler/SqlAttentionScheduler.cs; then
  echo "FAIL: candidate inference / scoring must not be hardcoded in the C# scheduler" >&2; exit 1
fi

# Notification bridge wires structured channel to SSE (render-only on frontend)
grep -qF "sql_attention_draft_candidate" backend/scheduler/DbNotifyListener.cs || { echo "FAIL: DbNotifyListener missing sql_attention_draft_candidate channel bridge" >&2; exit 1; }
grep -qF "extractSqlAttentionDraftCandidatePayload" frontend/runtime/sseReceiver.ts || { echo "FAIL: frontend sse receiver missing draft candidate payload extractor" >&2; exit 1; }
PASS_COUNT=$((PASS_COUNT + 1)) # OK: manifest_topology_key_expansion_draft_lane boundary checks passed

# phase_vector TODO requirement is conditional:
# - implementation boundary complete -> TODO is not required
# - implementation boundary incomplete -> TODO entry is required
phase_vector_impl_ready=1
grep -qF "CREATE OR REPLACE FUNCTION logs.generate_attention_phase_vector(" db/sql_attention_logs_tables.sql || phase_vector_impl_ready=0
grep -qF "CREATE OR REPLACE FUNCTION logs.refresh_hub_current(" db/sql_attention_logs_tables.sql || phase_vector_impl_ready=0
grep -qF "public static string GeneratePhaseVector(" backend/runtime/SqlAttentionPhaseVectorRuntime.cs || phase_vector_impl_ready=0
grep -qF "SqlAttentionPhaseVectorRuntime.GeneratePhaseVector(" backend/runtime/HubAttractorExplorationRuntime.cs || phase_vector_impl_ready=0
grep -qF "public static string BuildPhaseVectorJson(" backend/runtime/SqlAttentionPhaseVectorRuntime.cs || phase_vector_impl_ready=0
grep -qF "PhaseVectorJson: phaseJson" backend/runtime/HubAttractorExplorationRuntime.cs || phase_vector_impl_ready=0
grep -qF "string PhaseVectorJson" backend/schema/SqlAttentionContracts.cs || phase_vector_impl_ready=0
grep -qF "phase_vector_json" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || phase_vector_impl_ready=0

if [ "$phase_vector_impl_ready" -eq 0 ]; then
  grep -qF "phase_vector generation implementation" "$TODO_FILE" || {
    echo "FAIL: TODO missing phase_vector generation item while phase_vector implementation boundary is incomplete" >&2
    exit 1
  }
  PASS_COUNT=$((PASS_COUNT + 1))
else
  PASS_COUNT=$((PASS_COUNT + 1))
fi

if grep -qF "policy caps を用いた phase_vector" "$TODO_FILE"; then
  echo "FAIL: dangerous phrase remains: policy caps を用いた phase_vector" >&2
  exit 1
fi
if grep -qF "policy caps を用いた phase_vector 生成" "$TODO_FILE"; then
  echo "FAIL: dangerous phrase remains: policy caps を用いた phase_vector 生成" >&2
  exit 1
fi

while IFS= read -r line; do
  if [[ "$line" == *"manifest / policy cap 由来"* ]] && [[ "$line" != *"ではない"* ]]; then
    echo "FAIL: affirmative manifest/policy-cap origin context found in TODO" >&2
    echo "LINE: $line" >&2
    exit 1
  fi
done < "$TODO_FILE"

for bad in "phase movement is manifest" "phase movement is policy" "phase_movement_is_manifest" "phase_movement_is_policy"; do
  if grep -qi "$bad" "$TODO_FILE"; then
    echo "FAIL: dangerous phrase remains: $bad" >&2
    exit 1
  fi
done

PASS_COUNT=$((PASS_COUNT + 1))

HUBS_HIERARCHY_MD="$REPO_ROOT/docs/design/sql-attention-logs-ssot.md"
if ! grep -qF "hubs.topology_manifests" "$HUBS_HIERARCHY_MD" || ! grep -qF "hubs.hub_relations" "$HUBS_HIERARCHY_MD"; then
  echo "FAIL: SQL Attention SSOT md must document hubs.hub -> topology_manifests -> hub_relations hierarchy" >&2
  exit 1
fi
if ! grep -qi "manifest-scoped" "$HUBS_HIERARCHY_MD"; then
  echo "FAIL: SQL Attention SSOT md must describe manifest-scoped hub sequence exploration" >&2
  exit 1
fi
PASS_COUNT=$((PASS_COUNT + 1))

SQL_ATTENTION_SQL="$REPO_ROOT/db/sql_attention_logs_tables.sql"
if ! grep -qF "JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id" "$SQL_ATTENTION_SQL"; then
  echo "FAIL: refresh_hub_current deprecated support-cache hub_relations count must JOIN hubs.topology_manifests" >&2
  exit 1
fi
if grep -qE "hub_relations hr WHERE hr\.hub_id|WHERE hr\.hub_id = h\.hub_id" "$SQL_ATTENTION_SQL"; then
  echo "FAIL: refresh_hub_current must not count hub_relations via hr.hub_id source authority" >&2
  exit 1
fi
PASS_COUNT=$((PASS_COUNT + 1))

CONTENT_BUNDLE_REPO="$REPO_ROOT/backend/repository/NpgsqlContentBundleRepository.cs"
if grep -qE "hub_relations WHERE hub_id|hr\.hub_id::text" "$CONTENT_BUNDLE_REPO"; then
  echo "FAIL: NpgsqlContentBundleRepository must not query hub_relations via hub_id source authority" >&2
  exit 1
fi
if ! grep -qF "JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id" "$CONTENT_BUNDLE_REPO"; then
  echo "FAIL: NpgsqlContentBundleRepository must JOIN topology_manifests for hub_relations" >&2
  exit 1
fi
PASS_COUNT=$((PASS_COUNT + 1))

echo "PASS check-sql-attention-ssot.sh assertions=${PASS_COUNT}"
