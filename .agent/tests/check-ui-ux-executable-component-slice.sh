#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROADMAP="$REPO_ROOT/docs/system-roadmap.yaml"
TODO_FILE="$REPO_ROOT/.agent/tasks/todo.md"
PRIMITIVE_SSOT="$REPO_ROOT/docs/design/ui-ux-primitive-catalog-ssot.yaml"
CATALOG="$REPO_ROOT/frontend/components/catalog.ts"
FACTORY="$REPO_ROOT/frontend/runtime/runtimeComponentFactory.ts"
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
rg -n "UI_UX_PRIMITIVE_CATALOG_IDENTITIES" "$CATALOG" >/dev/null || fail "catalog constant missing: UI_UX_PRIMITIVE_CATALOG_IDENTITIES"

for name in AutoCompleteInput SearchCombobox CandidateConfidenceBadge InlineEditableField PatchPreviewPanel ApplyConfirmDialog FacetedFilterBar VirtualizedDataTable LayoutDropZone ComponentPlacementHandle SnapGridOverlay StyleTokenPicker ThemePreviewPanel DryRunResultPanel ValidationErrorPanel; do
  if ! rg -n "$name" "$ROADMAP" "$PRIMITIVE_SSOT" >/dev/null; then
    fail "representative slice name missing from roadmap/design SSOT references: $name"
  fi
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

# factory/catalog bidirectional consistency
python3 - "$CATALOG" "$FACTORY" <<'PY' || FAIL=$((FAIL+1))
import re, sys

catalog_content = open(sys.argv[1], encoding='utf-8').read()
factory_content = open(sys.argv[2], encoding='utf-8').read()

# Extract all componentKinds registered in the factory
factory_kinds = set()
for m in re.finditer(r'componentKinds:\s*\[(.*?)\]', factory_content, re.S):
    for km in re.finditer(r'"([^"]+)"', m.group(1)):
        factory_kinds.add(km.group(1))

def parse_array_entries(content, array_name):
    idx = content.find('const ' + array_name)
    if idx == -1:
        return []
    # Skip past the type annotation to find the initializer '= ['
    eq = content.find('= [', idx)
    if eq == -1:
        return []
    pos = eq + 3  # skip past '= ['
    entries = []
    while pos < len(content):
        c = content[pos]
        if c == ']':
            break
        if c == '{':
            depth = 1
            j = pos + 1
            while j < len(content) and depth:
                if content[j] == '{':
                    depth += 1
                elif content[j] == '}':
                    depth -= 1
                j += 1
            entries.append(content[pos:j])
            pos = j
        elif c == '/' and pos + 1 < len(content) and content[pos+1] == '/':
            eol = content.find('\n', pos)
            pos = eol + 1 if eol != -1 else len(content)
        else:
            pos += 1
    return entries

component_entries = parse_array_entries(catalog_content, 'COMPONENT_CATALOG_ENTRIES')
alias_entries = parse_array_entries(catalog_content, 'RUNTIME_ALIAS_CATALOG_IDENTITIES')

catalog_rc = {}
for e in component_entries:
    m = re.search(r'componentKind:\s*"([^"]+)"', e)
    if m:
        catalog_rc[m.group(1)] = 'runtimeConnected: true' in e

alias_kinds = set()
for e in alias_entries:
    m = re.search(r'componentKind:\s*"([^"]+)"', e)
    if m:
        alias_kinds.add(m.group(1))

errs = []
# runtimeConnected:true → must have factory registration
for kind, rc in sorted(catalog_rc.items()):
    if rc and kind not in factory_kinds:
        errs.append('runtimeConnected:true but no factory registration: ' + kind)
# factory registered → must be in COMPONENT_CATALOG_ENTRIES (rc:true) or RUNTIME_ALIAS_CATALOG_IDENTITIES
for kind in sorted(factory_kinds):
    if kind in alias_kinds:
        continue
    if kind not in catalog_rc:
        errs.append('factory registered but no catalog entry: ' + kind)
    elif not catalog_rc[kind]:
        errs.append('factory registered but catalog runtimeConnected:false: ' + kind)
# alias entry → must have factory registration
for kind in sorted(alias_kinds):
    if kind not in factory_kinds:
        errs.append('alias catalog entry but no factory registration: ' + kind)

if errs:
    print('\n'.join(errs))
    raise SystemExit(1)
print('factory/catalog consistency OK: %d connected, %d factory kinds, %d aliases' % (
    sum(1 for v in catalog_rc.values() if v), len(factory_kinds), len(alias_kinds)))
PY

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-ui-ux-executable-component-slice passed ==="
  exit 0
fi
echo "=== check-ui-ux-executable-component-slice failed: $FAIL ===" >&2
exit 1
