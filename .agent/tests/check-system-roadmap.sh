#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ROADMAP="$REPO_ROOT/docs/system-roadmap.yaml"

ruby - "$REPO_ROOT" "$ROADMAP" <<'RB'
require 'yaml'
repo = ARGV[0]
roadmap = ARGV[1]
failures=[]; warnings=[]
failf=->(m){failures << m}; warnf=->(m){warnings << m}
unless File.exist?(roadmap)
  failf.call('docs/system-roadmap.yaml not found')
  puts "=== system roadmap check ==="; failures.each{|x| puts "FAIL: #{x}"}; exit 1
end
raw = YAML.load_file(roadmap) || {}
root = raw['system_roadmap_ssot'].is_a?(Hash) ? raw['system_roadmap_ssot'] : {}
failf.call('system_roadmap_ssot must exist') if root.empty?
status_terms = root['status_terms'].is_a?(Hash) ? root['status_terms'] : {}
failf.call('system_roadmap_ssot.status_terms must be defined') if status_terms.empty?
allowed = status_terms.keys
impl = root['implementation_registry'].is_a?(Hash) ? root['implementation_registry'] : {}
failf.call('implementation_registry must exist') if impl.empty?
file_to_status={}
impl.each do |k,v|
  unless v.is_a?(Hash)
    failf.call("implementation_registry.#{k} must be a map"); next
  end
  st=v['status']
  failf.call("implementation_registry.#{k}.status '#{st}' is not in status_terms") unless allowed.include?(st)
  pr=!!v['production_ready']; evidence=v['evidence']; cc=v['completion_condition']; kg=v['known_gap_ref']; ps=v['public_summary']
  failf.call("implemented entry #{k} requires evidence or completion_condition") if st=='implemented' && evidence.nil? && cc.nil?
  failf.call("production_ready=true entry #{k} must not keep known_gap_ref") if pr && !kg.nil?
  failf.call("#{k} requires public_summary for implemented/production_ready") if (st=='implemented' || st=='production_ready' || pr) && (ps.nil? || ps.to_s.strip.empty?)
  failf.call("#{k} status #{st} requires known_gap_ref or completion_condition") if ['skeleton','partial'].include?(st) && kg.nil? && cc.nil?
  (v['files'] || []).each { |fp| file_to_status[fp]=st }
end
regex = /(skeleton|stub|dummy|mock|fake|pass-through|passthrough|temporary|not implemented)/i
exclude = %w[/tests/ /fixtures/ /bin/ /obj/ /node_modules/ /.git/]
Dir.glob(File.join(repo,'{backend,frontend}','**','*')).each do |path|
  next unless File.file?(path)
  ext = File.extname(path).downcase
  next unless ['.cs','.ts','.tsx','.js','.jsx'].include?(ext)
  norm = path.sub(repo + '/', '')
  next if exclude.any?{|e| norm.include?(e)}
  txt = File.read(path, encoding: 'UTF-8', invalid: :replace, undef: :replace)
  next unless txt.match?(regex)
  st = file_to_status[norm]
  if ['skeleton','partial'].include?(st)
    warnf.call("marker in registered #{st} file: #{norm}")
  elsif ['implemented','production_ready'].include?(st)
    failf.call("marker found in #{st} file: #{norm}")
  else
    failf.call("marker found in unregistered file: #{norm}")
  end
end
puts '=== system roadmap check ==='
warnings.each{|w| puts "WARN: #{w}"}
failures.each{|f| puts "FAIL: #{f}"}
if failures.any?
  puts "=== #{failures.length} failure(s), #{warnings.length} warning(s) ==="
  exit 1
end
puts "=== PASS (#{warnings.length} warning(s)) ==="
RB
