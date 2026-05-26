#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROADMAP="$REPO_ROOT/docs/system-roadmap.yaml"
TODO_FILE="$REPO_ROOT/.agent/tasks/todo.md"
CATALOG="$REPO_ROOT/frontend/components/catalog.ts"
FAIL=0

fail(){ echo "FAIL: $1" >&2; FAIL=$((FAIL+1)); }
ok(){ echo "OK: $1"; }

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

rg -n "frontend\.ui_ux_executable_component_slice:" "$ROADMAP" >/dev/null || fail "roadmap entry missing: frontend.ui_ux_executable_component_slice"
rg -n "## UI/UX Primitive Executable Component Slice" "$TODO_FILE" >/dev/null || fail "todo bundle missing"
rg -n "UI_UX_PRIMITIVE_CATALOG_IDENTITIES" "$CATALOG" >/dev/null || fail "catalog constant missing: UI_UX_PRIMITIVE_CATALOG_IDENTITIES"

for name in AutoCompleteInput SearchCombobox CandidateConfidenceBadge InlineEditableField PatchPreviewPanel ApplyConfirmDialog FacetedFilterBar VirtualizedDataTable LayoutDropZone ComponentPlacementHandle SnapGridOverlay StyleTokenPicker ThemePreviewPanel DryRunResultPanel ValidationErrorPanel; do
  rg -n "$name" "$TODO_FILE" "$ROADMAP" >/dev/null || fail "representative slice name missing in roadmap/todo: $name"
done

# registrationRequired:false must be alias_maintained only
python3 - "$CATALOG" <<'PY' || FAIL=$((FAIL+1))
import re,sys
s=open(sys.argv[1],encoding='utf-8').read()
entries=re.findall(r'\{ componentKey:.*?\}',s,flags=re.S)
errs=[]
for e in entries:
    rc='runtimeConnected: true' in e
    rr_false='registrationRequired: false' in e
    if rr_false and 'lifecycleStatus: "alias_maintained"' not in e:
        errs.append('registrationRequired:false non-alias entry: '+e.split('\n')[0][:120])
    if 'notes: "catalog_definition_only"' in e:
        if rc: errs.append('catalog_definition_only must keep runtimeConnected:false')
        if 'registrationRequired: true' not in e: errs.append('catalog_definition_only must keep registrationRequired:true')
    if 'runtimeConnected: true' in e and 'sourcePath: "CATALOG_SSOT"' in e:
        errs.append('runtimeConnected:true with CATALOG_SSOT sourcePath')
if errs:
    print('\n'.join(errs))
    raise SystemExit(1)
print('catalog audit checks passed')
PY

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-ui-ux-executable-component-slice passed ==="
  exit 0
fi
echo "=== check-ui-ux-executable-component-slice failed: $FAIL ===" >&2
exit 1
