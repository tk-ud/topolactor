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

```sh
cd frontend
deno task start
```

Requires backend running for dispatch operations.

## Note

This is an empty skeleton. Real business screens are out of scope for this skeleton issue.

## Local type check

Run the local frontend type check wrapper (Deno-only, no Node/npm, no backend build):

```sh
bash .agent/tests/check-frontend-types.sh
```

This validates the Fresh/Deno/Preact skeleton entrypoints with `deno check` only.
