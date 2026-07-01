#!/usr/bin/env bash
# check-test-proof-manifest-integrity.sh — audits the system-wide test proof manifest SSOT.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

ruby <<'RUBY'
require 'yaml'

manifest_path = 'docs/design/test-proof-manifest-ssot.yaml'
reverse_lookup_path = '.agent/docs/test-bundles.yaml'
ssot_map_path = '.agent/docs/ssot-map.yaml'

manifest = YAML.load_file(manifest_path)
reverse_lookup = YAML.load_file(reverse_lookup_path)
ssot_map = YAML.load_file(ssot_map_path)
failures = []

proofs = manifest.fetch('chronological_scope_index') { failures << 'missing chronological_scope_index'; [] }
required = manifest.dig('proof_model', 'required_fields') || []
proof_types = manifest.dig('proof_model', 'proof_types') || []
ids = {}
orders = []
existing_paths = ->(path) { File.exist?(path) || Dir.exist?(path) }
workflow_file_for = ->(workflow_job) { workflow_job.to_s.split('::', 2).first }

proofs.each_with_index do |proof, index|
  label = proof['proof_id'] || "index #{index}"
  required.each do |field|
    failures << "#{label}: missing required field #{field}" unless proof.key?(field)
  end

  id = proof['proof_id']
  if id
    failures << "duplicate proof_id #{id}" if ids.key?(id)
    ids[id] = proof
  end

  order = proof['proof_order']
  if !order.is_a?(Integer)
    failures << "#{label}: proof_order must be integer"
  else
    orders << [order, label]
  end

  unless proof_types.include?(proof['proof_type'])
    failures << "#{label}: invalid proof_type #{proof['proof_type'].inspect}"
  end

  %w[ssot_refs implementation_files test_files runner_surfaces evidence_inputs workflow_jobs depends_on unblocks proves does_not_prove].each do |field|
    failures << "#{label}: #{field} must be a list" unless proof[field].is_a?(Array)
  end

  changed = proof.dig('required_when', 'changed_files')
  failures << "#{label}: required_when.changed_files must be a non-empty list" unless changed.is_a?(Array) && !changed.empty?

  Array(proof['implementation_files']).each do |path|
    failures << "#{label}: implementation_files path does not exist: #{path}" unless existing_paths.call(path)
  end

  Array(proof['runner_surfaces']).each do |path|
    failures << "#{label}: runner_surfaces path does not exist: #{path}" unless File.exist?(path)
  end

  Array(proof['evidence_inputs']).each do |path|
    failures << "#{label}: evidence_inputs path does not exist: #{path}" unless existing_paths.call(path)
  end

  Array(proof['workflow_jobs']).each do |workflow_job|
    workflow_file = workflow_file_for.call(workflow_job)
    failures << "#{label}: workflow_jobs workflow file does not exist: #{workflow_job}" unless File.exist?(workflow_file)
  end

  Array(proof['test_files']).each do |path|
    if path.start_with?('docs/design/') || path.start_with?('db/') || path.start_with?('.agent/tests/') || path.start_with?('.github/')
      failures << "#{label}: test_files contains non-test/evidence/runner path #{path}"
    end
    unless path.include?('/tests/') || path.end_with?('.test.ts') || path.end_with?('.csproj')
      failures << "#{label}: test_files must contain executable test files/projects only: #{path}"
    end
    failures << "#{label}: test_files path does not exist: #{path}" unless existing_paths.call(path)
  end

  Array(proof['ssot_refs']).each do |path|
    failures << "#{label}: ssot_refs path does not exist; move to missing_ssot_blocking: #{path}" unless File.exist?(path)
  end

  Array(proof['missing_ssot_blocking']).each do |path|
    failures << "#{label}: missing_ssot_blocking path exists and should be moved to ssot_refs: #{path}" if File.exist?(path)
  end

  if proof['proof_type'] == 'known_gap'
    failures << "#{label}: known_gap must have known_gap_reason" if proof['known_gap_reason'].to_s.strip.empty?
    failures << "#{label}: known_gap must not claim proves entries" unless Array(proof['proves']).empty?
  else
    failures << "#{label}: implemented/design proof must have at least one proves entry" if Array(proof['proves']).empty?
    failures << "#{label}: non-gap proof must not carry missing_ssot_blocking" if proof.key?('missing_ssot_blocking')
  end
end

orders.each_cons(2) do |(prev_order, prev_id), (order, id)|
  failures << "proof_order must be strictly increasing: #{prev_id}(#{prev_order}) before #{id}(#{order})" unless order > prev_order
end

proofs.each do |proof|
  label = proof['proof_id']
  Array(proof['depends_on']).each do |dep|
    failures << "#{label}: depends_on unknown proof_id #{dep}" unless ids.key?(dep)
    if ids[dep] && ids[dep]['proof_order'].to_i >= proof['proof_order'].to_i
      failures << "#{label}: depends_on #{dep} must have lower proof_order"
    end
    if ids[dep] && proof['proof_type'] != 'known_gap' && ids[dep]['proof_type'] == 'known_gap'
      unless proof['depends_on_known_gap_allowed'] == true && !proof['depends_on_known_gap_reason'].to_s.strip.empty?
        failures << "#{label}: non-gap proof depends_on known_gap #{dep}; remove dependency or set depends_on_known_gap_allowed with reason"
      end
    end
  end
  Array(proof['unblocks']).each do |target|
    next if target == 'frontend-admin-projection-expression-e2e-completion'
    failures << "#{label}: unblocks unknown proof_id #{target}" unless ids.key?(target)
  end
end

Array(reverse_lookup['reverse_lookup']).each_with_index do |bundle, index|
  label = bundle['bundle_id'] || "reverse_lookup index #{index}"
  Array(bundle['proof_refs']).each do |proof_ref|
    failures << "#{reverse_lookup_path}: #{label}: proof_refs unknown proof_id #{proof_ref}" unless ids.key?(proof_ref)
  end
end

ssot_map_entries = Array(ssot_map['mapping'])
test_proof_entries = ssot_map_entries.select { |entry| Array(entry['required_docs']).include?(manifest_path) }
if test_proof_entries.empty?
  failures << "#{ssot_map_path}: #{manifest_path} must be discoverable via required_docs"
else
  required_surface_terms = ['test-proof-manifest-ci-gate', 'proof graph', 'manifest integrity', 'CI proof audit surface']
  surface_terms = test_proof_entries.flat_map { |entry| Array(entry['change_surfaces']) }
  required_surface_terms.each do |term|
    failures << "#{ssot_map_path}: missing discovery change surface #{term}" unless surface_terms.include?(term)
  end
end

if failures.empty?
  puts "OK  [test-proof-manifest] #{proofs.length} proof edges and #{Array(reverse_lookup['reverse_lookup']).length} reverse lookup bundles passed integrity checks"
else
  failures.each { |f| warn "FAIL [test-proof-manifest] #{f}" }
  exit 1
end
RUBY
