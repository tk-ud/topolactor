# frontend

Physical interaction projection layer for topolactor.

## Architecture role

Frontend is the physical interaction space. Ordinary UI operations (buttons, forms,
selects, table clicks, route transitions) become operation inputs. These are converted
into operation vectors internally. UI is projected from component packages, schemas,
and resume context.

## Tech stack

- **Framework**: Fresh (Deno)
- **UI**: Preact
- **Runtime**: Deno

## Canonical flow

```
user_operation
  → resolveOperationVector  (frontend/runtime/)
  → structure_map.lookup    (frontend/structure_map.ts)
  → package.resolve         (frontend/package/)
  → schema.resolve          (frontend/schema/)
  → components.expand       (frontend/components/, frontend/registry/)
  → api.dispatch            (frontend/api/)  ← if backend required
  → renderEmission          (frontend/runtime/)
  → UI projection
```

## Directory structure

| Path | Role |
|------|------|
| `routes/` | Fresh route projection entrypoints |
| `islands/` | Client-side interactive components (Fresh islands) |
| `components/` | Atomic UI components |
| `package/` | UI component package groups |
| `schema/` | UI element and wiring schemas |
| `registry/` | Frontend runtime registry |
| `runtime/` | Frontend runtime executor |
| `api/` | Backend contract client |
| `structure_map.ts` | Data topology to package/schema/component map |

## How to run

Local dev (Route A — hot reload, backend via Docker or host):

```sh
cp frontend/.env.example frontend/.env   # first time only
deno task dev                            # repo root; watches routes/ and islands/
```

Open http://localhost:8000. Requires backend on http://localhost:5000 (e.g. `docker compose -f infra/docker-compose.yml up -d postgres backend`).

Production-like demo (Route B — full Docker + nginx on port 80): see `docs/demo-walkthrough.md`.

Requires backend running for dispatch operations.

## Implementation Status

Routes, islands, components, and runtime files are substantially implemented. Admin routes (`/admin/*`) are wired to backend registry flows for seed, bucket, and package operations; SSE receiver / dispatcher / projection runtime are partial (see `docs/system-roadmap.yaml` M4). Visual layout builder is implemented in `/admin/ui-builder` with lifecycle state visibility, undo/redo history, actionable validation errors, keyboard/non-pointer operation, and CSS token preview; manual accessibility first-run check is pending.

## Local type check

Run the local frontend type check wrapper (Deno-only, no Node/npm, no backend build):

```sh
bash .agent/tests/check-frontend-types.sh
```

This validates the Fresh/Deno/Preact skeleton entrypoints with `deno check` only.
