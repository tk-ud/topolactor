#!/usr/bin/env bash
set -euo pipefail

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
echo "OK: SQL Attention SSOT files contain no progress/status vocabulary"

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
echo "OK: write_logs_attention and canonical generation-line implementation boundary checks passed"
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
  echo "OK: phase_vector implementation boundary is incomplete; TODO carry-over requirement is satisfied"
else
  echo "OK: phase_vector implementation boundary is present; TODO carry-over is not required"
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

echo "OK: TODO alignment and dangerous phrase checks passed"

HUBS_HIERARCHY_MD="$REPO_ROOT/docs/design/sql-attention-logs-ssot.md"
if ! grep -qF "hubs.topology_manifests" "$HUBS_HIERARCHY_MD" || ! grep -qF "hubs.hub_relations" "$HUBS_HIERARCHY_MD"; then
  echo "FAIL: SQL Attention SSOT md must document hubs.hub -> topology_manifests -> hub_relations hierarchy" >&2
  exit 1
fi
if ! grep -qi "manifest-scoped" "$HUBS_HIERARCHY_MD"; then
  echo "FAIL: SQL Attention SSOT md must describe manifest-scoped hub sequence exploration" >&2
  exit 1
fi
echo "OK: hubs space hierarchy and manifest-scoped exploration documented in SSOT md"

SQL_ATTENTION_SQL="$REPO_ROOT/db/sql_attention_logs_tables.sql"
if ! grep -qF "JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id" "$SQL_ATTENTION_SQL"; then
  echo "FAIL: refresh_hub_current deprecated support-cache hub_relations count must JOIN hubs.topology_manifests" >&2
  exit 1
fi
if grep -qE "hub_relations hr WHERE hr\.hub_id|WHERE hr\.hub_id = h\.hub_id" "$SQL_ATTENTION_SQL"; then
  echo "FAIL: refresh_hub_current must not count hub_relations via hr.hub_id source authority" >&2
  exit 1
fi
echo "OK: refresh_hub_current deprecated support cache uses manifest-scoped hub_relations count"

CONTENT_BUNDLE_REPO="$REPO_ROOT/backend/repository/NpgsqlContentBundleRepository.cs"
if grep -qE "hub_relations WHERE hub_id|hr\.hub_id::text" "$CONTENT_BUNDLE_REPO"; then
  echo "FAIL: NpgsqlContentBundleRepository must not query hub_relations via hub_id source authority" >&2
  exit 1
fi
if ! grep -qF "JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id" "$CONTENT_BUNDLE_REPO"; then
  echo "FAIL: NpgsqlContentBundleRepository must JOIN topology_manifests for hub_relations" >&2
  exit 1
fi
echo "OK: content bundle repository uses manifest-scoped hub_relations queries"
