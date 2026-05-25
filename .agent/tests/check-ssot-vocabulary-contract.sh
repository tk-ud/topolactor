#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

require_tool() { command -v "$1" >/dev/null 2>&1 || { echo "ERROR: required tool not found: $1" >&2; exit 1; }; }
require_tool ruby
require_tool python3

ruby <<'RUBY'
require 'yaml'
require 'set'

ROOT = Dir.pwd

def fail!(msg)
  warn "FAIL: #{msg}"
  exit 1
end

def set_from_yaml_list(v)
  Array(v).map(&:to_s).to_set
end

component = YAML.load_file(File.join(ROOT, 'docs/design/component-catalog-classification-ssot.yaml'))
pipeline = YAML.load_file(File.join(ROOT, 'docs/design/pipeline-continuity-ssot.yaml'))

allowed = {
  'componentFamily' => set_from_yaml_list(component['component_family']),
  'semanticRole' => set_from_yaml_list(component['semantic_role']),
  'visualRole' => set_from_yaml_list(component['visual_role']),
  'lifecycleStatus' => set_from_yaml_list(component['lifecycle_status']),
  'capabilityTags' => set_from_yaml_list(component['capability_tags'])
}

catalog = File.read(File.join(ROOT, 'frontend/components/catalog.ts'))
impl = {
  'componentFamily' => catalog.scan(/componentFamily:\s*"([^"]+)"/).flatten.to_set,
  'semanticRole' => catalog.scan(/semanticRole:\s*"([^"]+)"/).flatten.to_set,
  'visualRole' => catalog.scan(/visualRole:\s*"([^"]+)"/).flatten.to_set,
  'lifecycleStatus' => catalog.scan(/lifecycleStatus:\s*"([^"]+)"/).flatten.to_set,
  'capabilityTags' => catalog.scan(/capabilityTags:\s*\[([^\]]+)\]/m).flat_map{|m| m[0].scan(/"([^"]+)"/).flatten}.to_set
}

impl.each do |k, vals|
  unknown = vals - allowed[k]
  fail!("component catalog #{k} has values not in SSOT: #{unknown.to_a.sort.join(', ')}") unless unknown.empty?
end
puts 'OK  component catalog classification vocabulary subset check'

# Pipeline event types / required identity / prohibited terms from SSOT
lanes = pipeline.dig('pipeline_continuity_ssot', 'lane_contracts') || {}
api_lane = lanes['api_command_lane'] || {}
sse_lane = lanes['sse_projection_lane'] || {}
component_event_lane = lanes['frontend_component_event_log_lane'] || {}

event_allowed = set_from_yaml_list(api_lane['event_types']) | set_from_yaml_list(sse_lane['event_types']) | set_from_yaml_list(component_event_lane['event_types'])
required_identity_allowed = set_from_yaml_list(api_lane['required_identity']) | set_from_yaml_list(sse_lane['required_identity'])
prohibited_allowed = Set.new
[api_lane, sse_lane, component_event_lane].each do |lane|
  prohibited_allowed |= set_from_yaml_list(lane['prohibited'])
  if lane['persistence_policy'].is_a?(Hash)
    prohibited_allowed |= set_from_yaml_list(lane['persistence_policy']['prohibited'])
  end
  if lane['transport_selection_policy'].is_a?(Hash)
    prohibited_allowed |= set_from_yaml_list(lane['transport_selection_policy']['prohibited'])
  end
end

check_script = File.read(File.join(ROOT, '.agent/tests/check-pipeline-continuity.sh'))
# extract quoted expected tokens from checks; ensure these are from SSOT sets
script_tokens = check_script.scan(/check_content\s+"[^"]+"\s+"([^"]+)"/).flatten.to_set

# event/runtime-related tokens that are discrete vocab claims
event_like = script_tokens.select { |t| t =~ /\A(projection|Attention|click|change|select|toggle|expand|collapse|submit|focus|blur|drag|drop|event_type|runtime_destination|validation_class|manifest_id|component_ids|attention_score|target|layer|action|trigger_kind|role|context|idempotency_key|recorded_at)\z/ }.to_set
unknown_event = event_like - (event_allowed | required_identity_allowed)
fail!("pipeline check has event/identity expected tokens not in SSOT allowed vocab: #{unknown_event.to_a.sort.join(', ')}") unless unknown_event.empty?

# prohibited vocabulary checks in script must be subset of SSOT prohibited vocabulary
prohibited_like = script_tokens.select { |t| t.include?('silent') || t.include?('fallback') || t.include?('frontend_') || t.include?('component_direct') || t.include?('websocket') || t.include?('drop') }
unknown_prohibited = prohibited_like.to_set - prohibited_allowed
fail!("pipeline check has prohibited-vocabulary tokens not in SSOT prohibited set: #{unknown_prohibited.to_a.sort.join(', ')}") unless unknown_prohibited.empty?
puts 'OK  pipeline required_identity/prohibited vocabulary subset check'
RUBY

python3 - <<'PY'
import pathlib, re, sys
root = pathlib.Path('.')
ssot_docs = [
    root/'docs/design/runtime-orchestration-ssot.yaml',
    root/'docs/design/pipeline-continuity-ssot.yaml',
]
allowed_runtime_dest = set()
allowed_mapping_type = set()
for p in ssot_docs:
    s = p.read_text()
    allowed_runtime_dest.update(re.findall(r'\b(topology_transform_runtime|registry_attractor_runtime|admin_runtime|sse_projection_runtime)\b', s))
    allowed_mapping_type.update(re.findall(r'\b(runtime_mapping|db_notify_projection_mapping|ui_projection_mapping|topology_function_binding_mapping|projection_constructor_mapping|dispatcher_mapping)\b', s))

seed_text = (root/'db/seed_empty.sql').read_text()
seed_text += '\n' + (root/'db/demo_seed.sql').read_text()
impl_runtime_dest = set(re.findall(r'"runtime_destination"\s*:\s*"([a-z_]+)"', seed_text))
impl_mapping_type = set(re.findall(r"\"type\"\s*:\s*\"([a-z_]+)\"\s*,\s*\"runtime_destination\"", seed_text))

unknown_dest = sorted(impl_runtime_dest - allowed_runtime_dest)
unknown_type = sorted(impl_mapping_type - allowed_mapping_type)
if unknown_dest:
    print('FAIL: db seed runtime_destination not in SSOT:', ', '.join(unknown_dest), file=sys.stderr)
    sys.exit(1)
if unknown_type:
    print('FAIL: db seed mapping type not in SSOT:', ', '.join(unknown_type), file=sys.stderr)
    sys.exit(1)
print('OK  DB seed runtime_destination/type vocabulary subset check')
PY

echo "=== SSOT vocabulary contract check passed ==="
