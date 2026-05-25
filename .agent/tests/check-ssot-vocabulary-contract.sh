#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
ruby <<'RUBY'
require 'yaml'; require 'set'
def fail!(m) warn("FAIL: #{m}"); exit 1 end
def req(h,*ks) ks.reduce(h){|c,k| fail!("missing SSOT path: #{ks.join('/')}") unless c.is_a?(Hash)&&c.key?(k); c[k]} end
root=Dir.pwd
comp=YAML.load_file(File.join(root,'docs/design/component-catalog-classification-ssot.yaml'))
pipe=YAML.load_file(File.join(root,'docs/design/pipeline-continuity-ssot.yaml'))
runtime=YAML.load_file(File.join(root,'docs/design/runtime-orchestration-ssot.yaml'))
sysci=YAML.load_file(File.join(root,'docs/design/system-ci-admin-runtime-callable-surface.yaml'))

catalog=File.read(File.join(root,'frontend/components/catalog.ts'))
cap = catalog.scan(/capabilityTags:\s*\[([^\]]+)\]/m).flat_map{|m| m[0].scan(/"([^"]+)"/).flatten}.to_set
fields={
 'componentFamily'=>catalog.scan(/componentFamily:\s*"([^"]+)"/).flatten.to_set,
 'semanticRole'=>catalog.scan(/semanticRole:\s*"([^"]+)"/).flatten.to_set,
 'visualRole'=>catalog.scan(/visualRole:\s*"([^"]+)"/).flatten.to_set,
 'lifecycleStatus'=>catalog.scan(/lifecycleStatus:\s*"([^"]+)"/).flatten.to_set,
 'capabilityTags'=>cap,
}
allowed={
 'componentFamily'=>req(comp,'component_family').map(&:to_s).to_set,
 'semanticRole'=>req(comp,'semantic_role').map(&:to_s).to_set,
 'visualRole'=>req(comp,'visual_role').map(&:to_s).to_set,
 'lifecycleStatus'=>req(comp,'lifecycle_status').map(&:to_s).to_set,
 'capabilityTags'=>req(comp,'capability_tags').map(&:to_s).to_set,
}
fields.each{|k,v| fail!("empty extraction: catalog #{k}") if v.empty?; bad=v-allowed[k]; fail!("catalog #{k} not in SSOT: #{bad.to_a.sort.join(', ')}") unless bad.empty?}
puts 'OK  component catalog classification vocabulary subset check'

ss=req(sysci,'system_ci_admin_runtime_callable_surface_ssot')
ops=req(ss,'allowed_operations').map{|x| x['operation']}.to_set; fail!('allowed_operations empty') if ops.empty?
admin=File.read(File.join(root,'backend/runtime/AdminRuntime.cs')); ops.each{|op| fail!("AdminRuntime missing allowed operation: #{op}") unless admin.include?("\"#{op}\"")}
targets=req(ss,'initial_target_vocabulary','values').map(&:to_s).to_set; fail!('initial_target_vocabulary empty') if targets.empty?
sysop=File.read(File.join(root,'backend/runtime/SystemOperationCiRuntime.cs')); targets.each{|t| fail!("SystemOperationCiRuntime missing target: #{t}") unless sysop.include?("\"#{t}\"")}
tests=File.read(File.join(root,'backend/tests/Topolactor.Runtime.Tests/SystemCiAdminRuntimeTests.cs'))
fail!('SystemCiAdminRuntimeTests missing list_targets semantic path') unless tests.include?('"system_ci", "list_targets"')
fail!('SystemCiAdminRuntimeTests missing inspect semantic path') unless tests.include?('"system_ci", "inspect"')
statuses=req(ss,'output_contract','current_status_values').map(&:to_s).to_set; fail!('status values empty') if statuses.empty?
contracts=File.read(File.join(root,'backend/schema/SystemCiContracts.cs')); statuses.each{|st| fail!("SystemCiStatus missing value: #{st}") unless contracts.include?(st)}
puts 'OK  system_ci admin callable surface vocabulary subset check'

lanes=req(pipe,'pipeline_continuity_ssot','lanes'); fail!('pipeline lanes empty') if lanes.empty?
required=Set.new; prohibited=Set.new; events=Set.new
lanes.each_value do |lv|
  next unless lv.is_a?(Hash)
  required |= Array(lv['required_identity']).map(&:to_s).to_set
  prohibited |= Array(lv['prohibited']).map(&:to_s).to_set
  events |= Array(lv['event_types']).map(&:to_s).to_set
  %w[persistence_policy transport_selection_policy].each do |sub|
    prohibited |= Array(lv.dig(sub,'prohibited')).map(&:to_s).to_set
  end
end
fail!('pipeline required_identity empty extraction') if required.empty?
fail!('pipeline prohibited empty extraction') if prohibited.empty?
fail!('pipeline event_types empty extraction') if events.empty?
chk=File.read(File.join(root,'.agent/tests/check-pipeline-continuity.sh'))
%w[componentId_drift silent_fallback_for_malformed_component_definition_payload boolean_coercion].each do |tk|
  fail!("check-pipeline-continuity.sh missing token: #{tk}") unless chk.include?(tk)
  fail!("token not in SSOT lane prohibited: #{tk}") unless prohibited.include?(tk)
end
puts 'OK  pipeline required_identity/prohibited vocabulary subset check'

rv=req(runtime,'runtime_orchestration_ssot','runtime_vocabulary_contract')
map_types=Array(req(rv,'mapping_types')).map(&:to_s).to_set
rtd=Array(req(rv,'backend_runtime_destinations')).map(&:to_s).to_set
fail!('runtime mapping_types empty') if map_types.empty?; fail!('runtime destinations empty') if rtd.empty?
seed=File.read(File.join(root,'db/seed_empty.sql'))+"\n"+File.read(File.join(root,'db/demo_seed.sql'))
seed_dests=seed.scan(/"runtime_destination"\s*:\s*"([a-z_]+)"/).flatten.to_set
seed_types=seed.scan(/"type"\s*:\s*"([a-z_]+)"\s*,\s*"runtime_destination"/).flatten.to_set
fail!('seed runtime_destination extraction empty') if seed_dests.empty?
fail!('seed mapping type extraction empty') if seed_types.empty?
fail!("seed runtime_destination not in SSOT: #{(seed_dests-rtd).to_a.sort.join(', ')}") unless (seed_dests-rtd).empty?
fail!("seed mapping type not in SSOT: #{(seed_types-map_types).to_a.sort.join(', ')}") unless (seed_types-map_types).empty?
puts 'OK  DB seed runtime_destination/type vocabulary subset check'
RUBY

echo "=== SSOT vocabulary contract check passed ==="
