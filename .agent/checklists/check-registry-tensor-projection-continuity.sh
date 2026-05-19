#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/registry-tensor-projection-continuity"

if [ "${1:-}" = "--self-test" ]; then
  echo "=== Registry Tensor Projection Continuity Gate — Self-test ==="
  fails=0
  for spec in "pass.md:pass" "fail-missing-metadata.md:fail" "fail-invalid-answer.md:fail"; do
    f="${spec%%:*}"; expect="${spec##*:}"; path="${FIXTURES_DIR}/${f}"
    if bash "$0" "$path" >/dev/null 2>&1; then actual="pass"; else actual="fail"; fi
    if [ "$actual" = "$expect" ]; then
      echo "OK  ${f} -> ${actual}"
    else
      echo "FAIL: ${f} -> ${actual} (expected ${expect})"
      fails=$((fails + 1))
    fi
  done
  [ "$fails" -eq 0 ] && echo "=== Self-test: PASS ===" && exit 0
  echo "=== Self-test: FAIL (${fails}) ===" >&2
  exit 1
fi

CHECKLIST="${1:-}"
if [ -z "$CHECKLIST" ] || [ ! -f "$CHECKLIST" ]; then
  echo "Usage: check-registry-tensor-projection-continuity.sh <checklist-file> | --self-test" >&2
  exit 1
fi

fails=0
vfail(){ echo "FAIL: $1" >&2; fails=$((fails+1)); }

for key in Audit-Date Audit-Target Audit-Evidence; do
  line="$(grep -m1 "^${key}:" "$CHECKLIST" || true)"
  val="${line#${key}:}"
  val="$(echo "$val" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
  [ -z "$line" ] && vfail "${key} is missing"
  [ -z "$val" ] && vfail "${key} is empty"
done

count=0
while IFS= read -r line; do
  ans="${line#- Answer:}"
  ans="$(echo "$ans" | tr '[:upper:]' '[:lower:]' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
  count=$((count+1))
  case "$ans" in yes|no|n/a) : ;; *) vfail "invalid answer '${ans}' at item ${count}" ;; esac
done < <(grep '^- Answer:' "$CHECKLIST" || true)

[ "$count" -ne 21 ] && vfail "expected exactly 21 '- Answer:' lines, found ${count}"

if [ "$fails" -eq 0 ]; then
  echo "PASS: Registry Tensor Projection Continuity Gate"
  exit 0
fi

echo "FAIL: ${fails} violation(s)" >&2
exit 1
