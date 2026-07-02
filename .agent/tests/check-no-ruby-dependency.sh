#!/usr/bin/env bash
# check-no-ruby-dependency.sh — repo governance tooling Ruby-dependency exclusion guard.
#
# Responsibility:
#   Repo governance tooling structured processing must use Python3 stdlib
#   only (see docs/framework-policy.yaml
#   repo_governance_tooling_dependency_policy). Ruby is prohibited as a repo
#   governance tooling dependency. This guard detects *.rb files, Gemfile /
#   Gemfile.lock, ruby shebangs, and ruby/gem/bundle command invocation
#   anywhere in the repository.
#
# Allowlist:
#   If a legitimate product-code Ruby usage is ever introduced, this guard
#   must not silently delete it nor silently allow it. Add the exact
#   finding to RUBY_ALLOWLIST below with a mandatory reason comment
#   explaining why it is product code outside repo governance tooling
#   scope; the follow-up design decision on whether that Ruby usage is
#   actually justified is out of scope for this guard.
#
# Run with --self-test to exercise the negative-scenario fixture (a
# throwaway tmp directory with deliberately planted Ruby findings; cleaned
# up via trap, the real repository tree is never mutated).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Reason-commented allowlist for intentional, justified Ruby findings.
# Keep empty unless a finding is explicitly product code requiring a
# follow-up design decision outside this guard's scope.
RUBY_ALLOWLIST=()

is_allowlisted() {
  local p="$1" a
  for a in ${RUBY_ALLOWLIST[@]+"${RUBY_ALLOWLIST[@]}"}; do
    [ "$a" = "$p" ] && return 0
  done
  return 1
}

run_guard() {
  local scan_root="$1"
  cd "$scan_root"

  local failures=0
  fail() { echo "FAIL: $1" >&2; failures=$((failures + 1)); }
  ok()   { :; }

  local prune_dirs=(.git node_modules vendor dist build coverage .cache _fresh bin obj)
  local prune_expr=()
  for d in "${prune_dirs[@]}"; do
    if [ ${#prune_expr[@]} -gt 0 ]; then prune_expr+=(-o); fi
    prune_expr+=(-name "$d")
  done

  # 1. *.rb source files.
  local rb_hits=0
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    rb_hits=$((rb_hits + 1))
    if is_allowlisted "$f"; then ok "[allowlisted] $f"; else fail "Ruby source file present: $f"; fi
  done < <(find . \( -type d \( "${prune_expr[@]}" \) -prune \) -o -type f -name '*.rb' -print | sed 's#^\./##' | sort)
  [ "$rb_hits" -eq 0 ] && ok "[rb] no *.rb source files found"

  # 2. Gemfile / Gemfile.lock.
  local gemfile_hits=0
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    gemfile_hits=$((gemfile_hits + 1))
    if is_allowlisted "$f"; then ok "[allowlisted] $f"; else fail "Ruby Gemfile present: $f"; fi
  done < <(find . \( -type d \( "${prune_expr[@]}" \) -prune \) -o -type f \( -iname 'Gemfile' -o -iname 'Gemfile.lock' \) -print | sed 's#^\./##' | sort)
  [ "$gemfile_hits" -eq 0 ] && ok "[gemfile] no Gemfile/Gemfile.lock found"

  # 3. ruby shebang (first line of any file starts with '#!' + ruby interpreter path).
  local shebang_hits=0
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    shebang_hits=$((shebang_hits + 1))
    if is_allowlisted "$f"; then ok "[allowlisted] $f"; else fail "ruby shebang present: $f"; fi
  done < <(find . \( -type d \( "${prune_expr[@]}" \) -prune \) -o -type f -print \
      | sed 's#^\./##' \
      | while IFS= read -r rel; do
          head -c 64 "$rel" 2>/dev/null | grep -qE '^#!.*ruby' && echo "$rel"
        done \
      | sort)
  [ "$shebang_hits" -eq 0 ] && ok "[shebang] no ruby shebang found"

  # 4. ruby / gem install / bundle command invocation in scripts, workflows, docs.
  # Line-initial `ruby` (actual shell invocation, e.g. `ruby <<'RUBY'`) or explicit
  # `gem install` / `bundle exec` / `bundle install` command phrases anywhere.
  # Prose mentioning "Ruby" (capitalized, mid-sentence) is intentionally not
  # matched here; this guard targets command syntax, not policy prose.
  local invocation_hits=0
  while IFS= read -r hit; do
    [ -n "$hit" ] || continue
    local f="${hit%%:*}"
    invocation_hits=$((invocation_hits + 1))
    if is_allowlisted "$f"; then ok "[allowlisted] $hit"; else fail "ruby/gem/bundle command invocation present: $hit"; fi
  done < <(grep -rnE '^[[:space:]]*ruby\b|\bgem install\b|\bbundle (exec|install)\b' \
      --include='*.sh' --include='*.yml' --include='*.yaml' --include='*.md' . 2>/dev/null \
    | sed 's#^\./##' \
    | grep -vE '^(\.agent/tests/check-no-ruby-dependency\.sh:|node_modules/|\.deno/)' \
    | sort)
  [ "$invocation_hits" -eq 0 ] && ok "[invocation] no ruby/gem/bundle command invocation found"

  echo ""
  if [ "$failures" -eq 0 ]; then
    echo "PASS check-no-ruby-dependency"
    return 0
  else
    echo "=== $failures Ruby-dependency finding(s) ===" >&2
    return 1
  fi
}

self_test() {
  problems=0
  start_dir="$(pwd)"
  fixture_dir="$(mktemp -d)"
  cleanup() { cd "$start_dir"; rm -rf "$fixture_dir"; }
  trap cleanup EXIT

  echo "[self-test] clean tree must PASS"
  mkdir -p "$fixture_dir/clean/.agent/tests"
  echo "no ruby here" > "$fixture_dir/clean/.agent/tests/check-clean.sh"
  if run_guard "$fixture_dir/clean" >/tmp/self_test_clean.out 2>&1; then
    echo "[self-test] OK  clean tree passed as expected"
  else
    echo "[self-test] FAIL clean tree unexpectedly failed"; cat /tmp/self_test_clean.out; problems=$((problems + 1))
  fi

  echo "[self-test] planted *.rb file must FAIL"
  mkdir -p "$fixture_dir/dirty_rb"
  echo "puts 'hi'" > "$fixture_dir/dirty_rb/script.rb"
  if run_guard "$fixture_dir/dirty_rb" >/tmp/self_test_rb.out 2>&1; then
    echo "[self-test] FAIL planted .rb file was not detected"; problems=$((problems + 1))
  else
    echo "[self-test] OK  planted .rb file detected"
  fi

  echo "[self-test] planted Gemfile must FAIL"
  mkdir -p "$fixture_dir/dirty_gemfile"
  echo "source 'https://rubygems.org'" > "$fixture_dir/dirty_gemfile/Gemfile"
  if run_guard "$fixture_dir/dirty_gemfile" >/tmp/self_test_gemfile.out 2>&1; then
    echo "[self-test] FAIL planted Gemfile was not detected"; problems=$((problems + 1))
  else
    echo "[self-test] OK  planted Gemfile detected"
  fi

  echo "[self-test] planted ruby shebang must FAIL"
  mkdir -p "$fixture_dir/dirty_shebang"
  printf '#!/usr/bin/env ruby\nputs 1\n' > "$fixture_dir/dirty_shebang/tool.sh"
  if run_guard "$fixture_dir/dirty_shebang" >/tmp/self_test_shebang.out 2>&1; then
    echo "[self-test] FAIL planted ruby shebang was not detected"; problems=$((problems + 1))
  else
    echo "[self-test] OK  planted ruby shebang detected"
  fi

  echo "[self-test] planted ruby heredoc invocation must FAIL"
  mkdir -p "$fixture_dir/dirty_invocation"
  printf '#!/usr/bin/env bash\nruby <<'"'"'RUBY'"'"'\nputs 1\nRUBY\n' > "$fixture_dir/dirty_invocation/check-x.sh"
  if run_guard "$fixture_dir/dirty_invocation" >/tmp/self_test_invocation.out 2>&1; then
    echo "[self-test] FAIL planted ruby invocation was not detected"; problems=$((problems + 1))
  else
    echo "[self-test] OK  planted ruby invocation detected"
  fi

  rm -f /tmp/self_test_clean.out /tmp/self_test_rb.out /tmp/self_test_gemfile.out /tmp/self_test_shebang.out /tmp/self_test_invocation.out
  if [ "$problems" -eq 0 ]; then
    echo "[self-test] all negative-scenario assertions passed"
    return 0
  fi
  echo "[self-test] $problems assertion(s) failed" >&2
  return 1
}

if [ "${1:-}" = "--self-test" ]; then
  self_test
  exit $?
fi

run_guard "$REPO_ROOT"
