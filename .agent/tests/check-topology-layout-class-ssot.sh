#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/topology-layout-class-ssot.yaml"
CSS="$REPO_ROOT/frontend/styles/topology-layout.css"
TS="$REPO_ROOT/frontend/runtime/topologyLayoutClassDictionary.ts"
FAIL=0

fail(){ echo "FAIL: $1" >&2; FAIL=$((FAIL+1)); }
ok(){ echo "OK: $1"; }

[ -f "$SSOT" ] || { echo "FAIL: missing $SSOT" >&2; exit 1; }
ok "topology layout class ssot file exists"

[ -f "$CSS" ] || { echo "FAIL: missing $CSS" >&2; exit 1; }
ok "concrete css file exists"

[ -f "$TS" ] || { echo "FAIL: missing $TS" >&2; exit 1; }
ok "topologyLayoutClassDictionary.ts exists"

python3 - "$SSOT" "$CSS" "$TS" "$REPO_ROOT/frontend" <<'PY'
import re, sys, os
ssot_path, css_path, ts_path, frontend_root = sys.argv[1:5]
lines = open(ssot_path, encoding='utf-8').read().splitlines()
css_text = open(css_path, encoding='utf-8').read()
ts_text = open(ts_path, encoding='utf-8').read()

content = open(ssot_path, encoding='utf-8').read()
km = re.search(r'class_key_rule:[\s\S]*?regex:\s*"([^"]+)"', content)
nm = re.search(r'class_name_rule:[\s\S]*?regex:\s*"([^"]+)"', content)
key_regex = bytes(km.group(1), 'utf-8').decode('unicode_escape') if km else None
name_regex = bytes(nm.group(1), 'utf-8').decode('unicode_escape') if nm else None

in_classes = False
cur_key = None
cur = {}
classes = {}
in_scope = in_allowed = False
for ln in lines:
    if re.match(r'^  classes:\s*$', ln):
        in_classes = True
        continue
    if not in_classes:
        continue
    if re.match(r'^  (generated_artifacts|concrete_css|ci_check_contract):', ln):
        break
    m = re.match(r'^    ([a-z][a-z0-9_.]+):\s*$', ln)
    if m:
        if cur_key:
            classes[cur_key] = cur
        cur_key = m.group(1)
        cur = {"projectionScope": [], "allowedFor": []}
        in_scope = in_allowed = False
        continue
    if not cur_key:
        continue
    m = re.match(r'^      class_name:\s+(.+)$', ln)
    if m:
        cur['className'] = m.group(1).strip()
        continue
    m = re.match(r'^      category:\s+(.+)$', ln)
    if m:
        cur['category'] = m.group(1).strip()
        continue
    m = re.match(r'^      semantic_role:\s+(.+)$', ln)
    if m:
        cur['semanticRole'] = m.group(1).strip()
        continue
    if re.match(r'^      projection_scope:\s*$', ln):
        in_scope = True
        in_allowed = False
        continue
    if re.match(r'^      allowed_for:\s*$', ln):
        in_allowed = True
        in_scope = False
        continue
    m = re.match(r'^        -\s+(.+)$', ln)
    if m and in_scope:
        cur['projectionScope'].append(m.group(1).strip())
        continue
    if m and in_allowed:
        cur['allowedFor'].append(m.group(1).strip())
        continue
    if re.match(r'^      [a-z_]+:\s*', ln):
        in_scope = in_allowed = False
if cur_key:
    classes[cur_key] = cur

errors = []
if not key_regex:
    errors.append('class_key_rule.regex not found')
if not name_regex:
    errors.append('class_name_rule.regex not found')

keys = list(classes.keys())
names = [v.get('className') for v in classes.values()]
if len(keys) != len(set(keys)):
    errors.append('duplicate class keys')
if len(names) != len(set(names)):
    errors.append('duplicate class names')

key_cre = re.compile(key_regex) if key_regex else None
name_cre = re.compile(name_regex) if name_regex else None

for k, v in classes.items():
    if key_cre and not key_cre.match(k):
        errors.append(f'class key regex mismatch: {k}')
    cn = v.get('className', '')
    if name_cre and not name_cre.match(cn):
        errors.append(f'class name regex mismatch: {cn}')
    if f'.{cn}' not in css_text:
        errors.append(f'ssot class missing in css: {cn}')

css_classes = set(re.findall(r'\.(topolactor-topology-[a-z0-9-]+)', css_text))
ssot_names = set(names)
for cn in css_classes - ssot_names:
    errors.append(f'unknown topolactor-topology class in css: {cn}')
for cn in ssot_names - css_classes:
    errors.append(f'ssot class missing in css: {cn}')

ts_entries = {}
for m in re.finditer(
    r'classKey:\s*"([^"]+)"[\s\S]*?className:\s*"([^"]+)"[\s\S]*?category:\s*"([^"]+)"[\s\S]*?projectionScope:\s*\[([^\]]*)\][\s\S]*?semanticRole:\s*"([^"]+)"[\s\S]*?allowedFor:\s*\[([^\]]*)\]',
    ts_text,
):
    key, cname, cat, scopes, role, allowed = m.groups()
    scope = [x.strip().strip('"') for x in scopes.split(',') if x.strip()]
    allow = [x.strip().strip('"') for x in allowed.split(',') if x.strip()]
    ts_entries[key] = {
        'className': cname,
        'category': cat,
        'projectionScope': scope,
        'semanticRole': role,
        'allowedFor': allow,
    }

for k, v in classes.items():
    if k not in ts_entries:
        errors.append(f'projection drift: missing class in TS: {k}')
        continue
    tv = ts_entries[k]
    for field in ('className', 'category', 'semanticRole'):
        if tv[field] != v.get(field):
            errors.append(f'projection drift {field}: {k}')
    if sorted(tv['projectionScope']) != sorted(v.get('projectionScope', [])):
        errors.append(f'projection drift projectionScope: {k}')
    if sorted(tv['allowedFor']) != sorted(v.get('allowedFor', [])):
        errors.append(f'projection drift allowedFor: {k}')

for k in ts_entries:
    if k not in classes:
        errors.append(f'projection drift: extra class in TS: {k}')

allowed_files = {
    os.path.normpath(css_path),
    os.path.normpath(ts_path),
    os.path.normpath(os.path.join(frontend_root, 'runtime', 'topologyLayoutClassResolver.ts')),
    os.path.normpath(os.path.join(frontend_root, 'islands', 'UiBuilderAdmin.tsx')),
}
for root, _, files in os.walk(frontend_root):
    for fn in files:
        if not fn.endswith(('.ts', '.tsx', '.css')):
            continue
        fp = os.path.normpath(os.path.join(root, fn))
        rel = fp.replace('\\', '/')
        if fp in allowed_files or '/tests/' in rel or '/runtime/topologyLayoutClassDictionary.ts' in rel:
            continue
        text = open(fp, encoding='utf-8', errors='ignore').read()
        for m in re.finditer(r'topolactor-topology-[a-z0-9-]+', text):
            errors.append(f'unknown topolactor-topology class literal in {fp}: {m.group(0)}')

if errors:
    print('Topology layout class SSOT check failed:')
    for e in errors:
        print(' -', e)
    sys.exit(1)

print(f'Parsed classes={len(classes)} css_classes={len(css_classes)} ts_entries={len(ts_entries)}')
PY

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-topology-layout-class-ssot passed ==="
else
  echo "=== check-topology-layout-class-ssot failed: $FAIL ===" >&2
  exit 1
fi
