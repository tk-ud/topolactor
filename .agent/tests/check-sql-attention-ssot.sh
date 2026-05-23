#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

SSOT_YAML="docs/design/sql-attention-logs-ssot.yaml"
SSOT_MD="docs/design/sql-attention-logs-ssot.md"
ROADMAP="docs/system-roadmap.yaml"
TODO_FILE=".agent/tasks/todo.md"

ruby <<'RUBY'
require 'yaml'

ssot = YAML.load_file('docs/design/sql-attention-logs-ssot.yaml')
roadmap = YAML.load_file('docs/system-roadmap.yaml')

def fail!(msg)
  warn "FAIL: #{msg}"
  exit 1
end

def assert_eq(actual, expected, label)
  fail!("#{label} expected=#{expected.inspect} actual=#{actual.inspect}") unless actual == expected
end

def assert_includes(arr, value, label)
  fail!("#{label} missing #{value.inspect}") unless arr.is_a?(Array) && arr.include?(value)
end

assert_eq(ssot['id'], 'sql_attention_logs_ssot', 'ssot.id')
assert_eq(ssot['status'], 'structural_ssot', 'ssot.status')
assert_eq(ssot['semantic_markdown'], 'docs/design/sql-attention-logs-ssot.md', 'ssot.semantic_markdown')
assert_eq(ssot.dig('ssot_roles', 'markdown'), 'semantic_formula_and_route_ssot', 'ssot_roles.markdown')
assert_eq(ssot.dig('ssot_roles', 'yaml'), 'structural_policy_schema_runtime_contract_ssot', 'ssot_roles.yaml')

assert_eq(ssot.dig('sql_attention_target', 'target_schema'), 'hubs', 'sql_attention_target.target_schema')
%w[tensor attractor collapse_point].each { |v| assert_includes(ssot.dig('sql_attention_target', 'target_semantics'), v, 'sql_attention_target.target_semantics') }
%w[topologys registry].each { |v| assert_includes(ssot.dig('sql_attention_target', 'not_target'), v, 'sql_attention_target.not_target') }
%w[logs_current logs_attention phase_attention].each do |k|
  fail!("layers.#{k} missing") unless ssot.dig('layers', k).is_a?(Hash)
end
fail!('scheduler_runtime_boundary.owns missing') unless ssot.dig('scheduler_runtime_boundary', 'owns').is_a?(Array)
assert_eq(ssot.dig('evidence_layer_separation', 'invariant'), 'no_single_score_collapse', 'evidence_layer_separation.invariant')

fields = ssot.dig('layers', 'logs_attention', 'schema_contract', 'suggested_fields')
%w[current_id statistics_json ema_score l2_norm vector_json phase_vector_json neighbor_score evidence_json archive_policy].each do |f|
  assert_includes(fields, f, 'logs_attention.schema_contract.suggested_fields')
end

semantics = ssot.dig('layers', 'logs_attention', 'schema_contract', 'persistence_semantics')
%w[
  append_only
  archive_required
  must_reference_logs_current_current_id
  statistics_json_and_ema_score_store_stable_confidence_layer
  vector_json_stores_convergent_neighbor_hit_vector
  phase_vector_json_stores_exploratory_phase_shifted_candidate_vector
].each { |v| assert_includes(semantics, v, 'logs_attention.schema_contract.persistence_semantics') }

phase_invariants = (ssot.dig('layers', 'phase_attention', 'invariants') || []) + (ssot.dig('phase_attention_function_contract', 'invariants') || [])
%w[
  phase_attention_is_post_main_auxiliary_route
  phase_vector_is_candidate_evidence_not_adopted_topology_state
  no_direct_registry_mutation
  no_automatic_migration_execution
  no_automatic_column_promotion
  phase_movement_is_not_manifest_or_policy_cap_derived
].each { |v| fail!("phase invariants missing #{v}") unless phase_invariants.include?(v) }

detailed = roadmap.dig('system_roadmap_ssot', 'related_surfaces', 'detailed_design_ssot')
%w[docs/design/sql-attention-logs-ssot.md docs/design/sql-attention-logs-ssot.yaml].each { |v| assert_includes(detailed, v, 'roadmap.related_surfaces.detailed_design_ssot') }

milestones = roadmap.dig('system_roadmap_ssot', 'milestones')
fail!('milestones.M7_sql_attention_observation_runtime missing') unless milestones&.key?('M7_sql_attention_observation_runtime')

registry = roadmap.dig('system_roadmap_ssot', 'implementation_registry')
%w[
  backend.sql_attention_logs_current
  backend.sql_attention_hub_current
  backend.sql_attention_scheduler_exploration
  backend.sql_attention_logs_attention_persistence
  backend.sql_attention_phase_vector
  backend.sql_attention_topology_projection
].each { |k| fail!("implementation_registry missing #{k}") unless registry&.key?(k) }

puts 'OK: SQL Attention SSOT YAML and roadmap alignment checks passed'
RUBY

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

grep -qF "public virtual Task<int> WriteLogsAttentionAsync(" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: missing SqlAttentionLogsRepository.WriteLogsAttentionAsync boundary" >&2; exit 1; }
grep -qF "public override async Task<int> WriteLogsAttentionAsync(" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: missing NpgsqlSqlAttentionLogsRepository.WriteLogsAttentionAsync boundary" >&2; exit 1; }
grep -qF "explorationResult.Hits.Count == 0" backend/scheduler/SqlAttentionScheduler.cs || { echo "FAIL: scheduler missing empty-hits guard before write" >&2; exit 1; }
grep -qF "_sqlAttentionLogsRepository.WriteLogsAttentionAsync(" backend/scheduler/SqlAttentionScheduler.cs || { echo "FAIL: scheduler missing write_logs_attention call" >&2; exit 1; }
grep -qF "INSERT INTO logs.attention" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: logs.attention INSERT boundary missing" >&2; exit 1; }
if find_pattern_matches "UPDATE\\s+logs\\.attention|DELETE\\s+FROM\\s+logs\\.attention" backend/repository/NpgsqlSqlAttentionLogsRepository.cs >/dev/null; then
  echo "FAIL: logs.attention append-only violated by UPDATE/DELETE" >&2
  exit 1
fi
grep -qF "ArchivePolicy must be 'required'" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: archive_policy required enforcement missing" >&2; exit 1; }
grep -qF "ArchivePolicy must be 'required'" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || { echo "FAIL: archive_policy required enforcement missing in Npgsql repo" >&2; exit 1; }
grep -qF "CurrentId must not be empty" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: current_id required boundary missing" >&2; exit 1; }
grep -qF "HubCurrentId must not be empty" backend/repository/SqlAttentionLogsRepository.cs || { echo "FAIL: hub_current_id required boundary missing" >&2; exit 1; }
echo "OK: write_logs_attention implementation boundary checks passed"
# phase_vector TODO requirement is conditional:
# - if implementation boundary is missing -> TODO entry is required
# - if implementation boundary exists -> TODO entry is not required and must not be forced back
phase_vector_impl_ready=1
grep -qF "phase_vector_json" backend/repository/NpgsqlSqlAttentionLogsRepository.cs || phase_vector_impl_ready=0
grep -qF "PhaseVectorJson" backend/repository/SqlAttentionLogsRepository.cs || phase_vector_impl_ready=0

if [ "$phase_vector_impl_ready" -eq 0 ]; then
  grep -qF "phase_vector generation implementation" "$TODO_FILE" || {
    echo "FAIL: TODO missing phase_vector generation item while phase_vector implementation boundary is incomplete" >&2
    exit 1
  }
  echo "OK: phase_vector implementation is incomplete; TODO carry-over requirement is satisfied"
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
