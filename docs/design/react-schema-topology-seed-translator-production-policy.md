# React Schema Topology Seed Translator Production Policy

Status: experimental production policy
Authority: non-SSOT implementation guidance
Canonical exchange contract: `docs/design/react-schema-topology-seed-translator-ssot.yaml`

## Purpose

Build a serverless agent-facing translator tool that can run without starting backend, frontend, nginx, or DB.

Primary direction:

```text
existing YAML SSOT/catalog references
-> generated React schema catalog
-> reviewed React schema
-> topology UI seed JSON/YAML
```

First target is not active UI implementation. First target is generated schema inspection.

## Work boundary

### OK

- Python3 tool runs from repo checkout without server startup.
- Tool reads existing YAML SSOT/catalog files.
- Tool generates React-like schema JSON/YAML with source evidence.
- Tool validates schema/seed exchange using the SSOT contract.
- Tool can later emit topology UI seed JSON/YAML.
- C# can call the tool through a bounded subprocess or local compose service.
- docker compose may add a local translator service.
- nginx may add a local tool route for compose mode.

### NG

- Do not expand existing `SeedRuntime` / `SeedAdmin` into this compiler.
- Do not make generated schema active topology.
- Do not write DB from the translator.
- Do not make translator a runtime policy authority.
- Do not create arbitrary React execution.
- Do not bypass UIBuilder preview / validate / apply.
- Do not route around backend authority for apply/approval.

## Existing scope lock

Existing seed runtime/UI stays as validation surface:

```text
SeedAdmin / SeedRuntime:
- /storage/seed.json save/load
- version + runtimes[] validation
- runtime declaration preview
- runtime declaration import boundary
```

The React schema translator is a separate tool surface.

## Candidate files

```text
.agent/tools/react-schema-topology-seed-translator
.agent/scripts/react_schema_topology_seed_translator.py
docs/design/react-schema-topology-seed-translator-ssot.yaml
infra/docker-compose.yml
infra/nginx.conf
```

Implementation may choose different names if they fit repo conventions better.

## CLI shape

```text
react-schema-topology-seed-translator collect-yaml
react-schema-topology-seed-translator generate-react-schema
react-schema-topology-seed-translator validate-react-schema
react-schema-topology-seed-translator emit-seed
```

## Input / output

Input:

```text
repo_root
reference_yaml_catalog
optional_surface_key
optional_fixture_key
```

Output candidates:

```text
.agent/generated/react-schema-topology-seed/react-schema-catalog.json
.agent/generated/react-schema-topology-seed/react-schema-catalog.yaml
.agent/generated/react-schema-topology-seed/topology-ui-seed.preview.json
.agent/generated/react-schema-topology-seed/translator-report.json
```

Generated outputs are evidence artifacts. Do not commit them unless task scope explicitly asks for evidence snapshots.

## Compose / C# / nginx production direction

### docker compose

Add a local Python3 translator service only after the offline CLI works.

Constraints:

```text
- read-only docs/design mount
- generated output mount
- no DB credentials required for collect/generate
- not required for backend healthcheck
- not required for frontend healthcheck
```

### C# call boundary

Allowed call shapes:

```text
- subprocess with explicit input/output paths
- local compose HTTP call to translator service
```

C# must treat translator output as candidate/evidence, not semantic authority.

### nginx

If compose service exists, nginx may expose a local tool route:

```text
/tools/react-schema-translator/
```

Route is local/demo tool surface only. It must not become public runtime authority.

## First implementation target

Generate schema from YAML only.

Minimum output must include:

```text
- component schema candidates
- form schema candidates
- field/control schema candidates
- action schema candidates
- workflow/step schema candidates
- propBinding candidates
- payloadFrom candidates
- style token refs
- source_yaml_refs per node
- known_gap_refs for unresolved paths
```

## First fixture recommendation

Start with existing preset fixtures before instance_settings if that reduces ambiguity:

```text
1. hub_search.readonly.v1
2. physical_search_crud_aggregate.v1
3. auth.external.credential_management.projection / instance_settings
```

Reason:

```text
hub_search and physical CRUD already have layout_tree examples.
instance_settings then proves form/workflow/approval coverage.
```

## Completion check for first experiment

OK:

```text
- tool runs without server startup
- reads declared YAML references
- emits generated React schema catalog
- every generated node has source_yaml_refs
- unresolved mappings are explicit
- no seed emission required yet
```

NG:

```text
- generated schema lacks form nodes
- generated schema loses source evidence
- generated schema silently invents component keys
- tool requires backend/frontend/nginx/DB for generation
- output is treated as active topology
```
