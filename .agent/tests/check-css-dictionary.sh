#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FILE="$REPO_ROOT/docs/design/css-dictionary-ssot.yaml"
FAIL=0

fail(){ echo "FAIL: $1" >&2; FAIL=$((FAIL+1)); }
ok(){ echo "OK: $1"; }

[ -f "$FILE" ] || { echo "FAIL: missing $FILE" >&2; exit 1; }
ok "css dictionary file exists"

python3 - "$FILE" "$REPO_ROOT/frontend/runtime/cssDictionary.ts" <<'PY'
import re,sys
p=sys.argv[1]
ts=sys.argv[2]
lines=open(p,encoding='utf-8').read().splitlines()
ts_text=open(ts,encoding='utf-8').read()

in_allow=False; in_scales=False; in_tokens=False; in_blocked=False; in_status=False
allow=set(); scales=set(); blocked=set(); tokens=[]; statuses={}
cur_token=None; cur_prop=None; cur_vref=None
regex=None

for ln in lines:
    s=ln.rstrip('\n')
    if s.strip().startswith('regex:') and regex is None:
        m=re.search(r'regex:\s*"(.*)"',s)
        if m: regex=bytes(m.group(1),"utf-8").decode("unicode_escape")
    if re.match(r'^  css_property_allowlist:\s*$',s): in_allow=True; in_scales=in_tokens=in_blocked=False; continue
    if re.match(r'^  value_scales:\s*$',s): in_scales=True; in_allow=in_tokens=in_blocked=False; continue
    if re.match(r'^  blocked_property_policy:\s*$',s): in_blocked=True; in_allow=in_scales=in_tokens=False; continue
    if re.match(r'^  tokens:\s*$',s): in_tokens=True; in_allow=in_scales=in_blocked=False; continue
    if re.match(r'^  integration_status:\s*$',s): in_status=True; continue
    if in_status:
        m=re.match(r'^    (ui_binding|ci_check|db_token_ref_validation):\s*(\S+)',s)
        if m: statuses[m.group(1)]=m.group(2)
        if re.match(r'^\S',s): in_status=False

    if in_allow:
        m=re.match(r'^      -\s+(.+)$',s)
        if m: allow.add(m.group(1).strip())
        if re.match(r'^  [a-z_]+:\s*$',s) and 'css_property_allowlist' not in s and not s.startswith('  css_property_allowlist'): pass
        if re.match(r'^  [a-z_]+:\s*$',s) and not s.startswith('  css_property_allowlist') and s!='  css_property_allowlist:':
            if not s.startswith('    '): in_allow=False
    if in_scales:
        mcat=re.match(r'^    ([a-z_]+):\s*$',s)
        if mcat: current=mcat.group(1)
        mv=re.match(r'^      ([a-zA-Z0-9_]+):\s*.+$',s)
        if mv:
            scales.add(f"{current}.{mv.group(1)}")
    if in_blocked:
        m=re.match(r'^      -\s+(.+)$',s)
        if m: blocked.add(m.group(1).strip())
    if in_tokens and re.match(r'^  [a-z_]+:\s*$',s):
        in_tokens=False
    if in_tokens:
        mt=re.match(r'^    ([a-z0-9._-]+):\s*$',s)
        if mt:
            key=mt.group(1)
            if key in ("selector_grouping","draft_shape","raw_css_policy","required_checks"): continue
            if cur_token is not None: tokens.append((cur_token,cur_prop,cur_vref))
            cur_token,cur_prop,cur_vref=key,None,None
            continue
        mp=re.match(r'^      property:\s+(.+)$',s)
        if mp and cur_token: cur_prop=mp.group(1).strip()
        mv=re.match(r'^      value_ref:\s+(.+)$',s)
        if mv and cur_token: cur_vref=mv.group(1).strip()
if cur_token is not None: tokens.append((cur_token,cur_prop,cur_vref))

errors=[]
if not regex: errors.append('token_key_rule.regex not found')
else:
    cre=re.compile(regex)

seen=set(); dup=[]
for t,prop,vref in tokens:
    if t in seen: dup.append(t)
    seen.add(t)
    if regex and not cre.match(t): errors.append(f'token key regex mismatch: {t}')
    if prop and prop not in allow: errors.append(f'property not in allowlist: {t} -> {prop}')
    if prop and prop in blocked: errors.append(f'blocked property used in token: {t} -> {prop}')
    if vref and vref not in scales: errors.append(f'value_ref unresolved: {t} -> {vref}')
if dup: errors.append('duplicate token keys: ' + ', '.join(sorted(set(dup))))

for key in ('ui_binding','ci_check','db_token_ref_validation'):
    if key not in statuses: errors.append(f'integration_status missing: {key}')

prev=''

# projection drift check: docs/design/css-dictionary-ssot.yaml -> frontend/runtime/cssDictionary.ts
yaml_proj=[]
for t,prop,vref in tokens:
    pass

# re-parse YAML for projection fields
in_tokens=False
cur=None
proj={}
in_component_scope=False
for ln in lines:
    if re.match(r'^  tokens:\s*$',ln):
        in_tokens=True
        continue
    if in_tokens and re.match(r'^  [a-z_]+:\s*$',ln):
        in_tokens=False
    if not in_tokens:
        continue
    m=re.match(r'^    ([a-z0-9._-]+):\s*$',ln)
    if m:
        cur=m.group(1)
        proj[cur]={"componentScope":[]}
        in_component_scope=False
        continue
    if not cur:
        continue
    m=re.match(r'^      category:\s+(.+)$',ln)
    if m: proj[cur]["category"]=m.group(1).strip(); in_component_scope=False; continue
    m=re.match(r'^      property:\s+(.+)$',ln)
    if m: proj[cur]["property"]=m.group(1).strip(); in_component_scope=False; continue
    m=re.match(r'^      semantic_role:\s+(.+)$',ln)
    if m: proj[cur]["semanticRole"]=m.group(1).strip(); in_component_scope=False; continue
    if re.match(r'^      component_scope:\s*$',ln):
        in_component_scope=True
        continue
    m=re.match(r'^        -\s+(.+)$',ln)
    if m and in_component_scope:
        proj[cur]["componentScope"].append(m.group(1).strip())
        continue
    if re.match(r'^      [a-z_]+:\s*',ln):
        in_component_scope=False

ts_entries={}
for m in re.finditer(r'\{\s*tokenKey:\s*"([^"]+)"\s*,\s*category:\s*"([^"]+)"\s*,\s*property:\s*"([^"]+)"\s*,\s*componentScope:\s*\[([^\]]*)\]\s*,\s*semanticRole:\s*"([^"]+)"[^}]*\}', ts_text):
    key,cat,prop,scopes,role=m.groups()
    scope=[x.strip().strip('"') for x in scopes.split(',') if x.strip()]
    ts_entries[key]={"category":cat,"property":prop,"componentScope":scope,"semanticRole":role}

for k,v in proj.items():
    if k not in ts_entries:
        errors.append(f'projection drift: missing token in cssDictionary.ts: {k}')
        continue
    tv=ts_entries[k]
    for field in ("category","property","semanticRole"):
        if v.get(field)!=tv.get(field):
            errors.append(f'projection drift: {k} field {field} yaml={v.get(field)} ts={tv.get(field)}')
    if sorted(v.get("componentScope",[]))!=sorted(tv.get("componentScope",[])):
        errors.append(f'projection drift: {k} componentScope yaml={v.get("componentScope",[])} ts={tv.get("componentScope",[])}')
for k in ts_entries:
    if k not in proj:
        errors.append(f'projection drift: extra token in cssDictionary.ts: {k}')

if errors:
    print('CSS dictionary check failed:')
    for e in errors: print(' -',e)
    sys.exit(1)
print(f'Parsed tokens={len(tokens)} allowlist={len(allow)} scales={len(scales)}')
print('integration_status:', statuses)
PY

if [ "$FAIL" -eq 0 ]; then
  echo "=== check-css-dictionary passed ==="
else
  echo "=== check-css-dictionary failed: $FAIL ===" >&2
  exit 1
fi
