# Topolactor Backend

## Overview

The backend is the **abstract runtime layer**. It receives user operations from the frontend (or any caller), resolves them through a strict data-driven pipeline, and emits validated responses. There are **no fallback paths** that bypass runtime resolution. Any broken reference — missing attractor, missing package, missing schema — produces an explicit `ValidationError` in the response rather than a silent default or partial result.

## Canonical Execution Path

```
stored_topology_data
  → user_operation (EndpointRequestDto)
    → operation_vector (OperationVectorResolver)
      → guard_validation (RuntimeGuard)
        → attractor_resolve (AttractorResolver)
          → structure_map_resolve (StructureMapResolver)
            → package_resolve (PackageResolver)
              → schema_resolve (SchemaResolver)
                → semantic_map (SemanticMapper)
                  → diff_log (DiffLogRepository)
                    → emission_build (EmissionBuilder)
                      → EndpointResponseDto
```

Every step either succeeds and passes its output to the next step, or returns an error response immediately. No step is skipped; no step has a silent fallback.

## Class Roles

| Class | Namespace | Role |
|---|---|---|
| `DispatchEndpoint` | `Topolactor.Endpoint` | Thin boundary. Validates the request is not null, delegates entirely to `RuntimeExecutor`. No business logic. |
| `RuntimeExecutor` | `Topolactor.Runtime` | Single canonical orchestrator of the full pipeline. Calls each resolver in order. Catches exceptions from each stage and converts them to `ValidationError` responses. |
| `OperationVectorResolver` | `Topolactor.Runtime` | Maps `EndpointRequestDto` fields to an `OperationVector`. Derives `AttractorKey` as `target:layer:action` (lowercase). |
| `RuntimeGuard` | `Topolactor.Guard` | Validates the `OperationVector` before resolution begins. Returns errors for missing `Target`, `Layer`, `Action`, or `AttractorKey`. |
| `AttractorResolver` | `Topolactor.Runtime` | Loads the structure map record keyed by `AttractorKey`. Throws if not found — no fallback. |
| `StructureMapResolver` | `Topolactor.Runtime` | Loads the full structure map by ID and constructs the initial `RuntimeWorkingShape`. |
| `PackageResolver` | `Topolactor.Runtime` | Loads the package definition by `PackageId` from the working shape. Throws if missing or not found. |
| `SchemaResolver` | `Topolactor.Runtime` | Loads the schema definition by `SchemaId` from the working shape. Throws if missing or not found. |
| `EmissionBuilder` | `Topolactor.Runtime` | Constructs the final `Emission` from the fully resolved `RuntimeWorkingShape`. |
| `SemanticMapper` | `Topolactor.Mapper` | Translates a resolved `RuntimeWorkingShape` into a `RepositoryCommand`. Maps domain meaning — not an ORM mapper. |
| `TopologyRepository` | `Topolactor.Repository` | Loads stored topology data (structure maps, packages, schemas) from the database. Stub implementations return null. |
| `DiffLogRepository` | `Topolactor.Repository` | Append-only log of topology mutations. Records before/after state for each operation. Stub logs to `ILogger`. |

### Key Types (in `Topolactor.Schema`)

- **`EndpointRequestDto`** — inbound DTO from the caller.
- **`EndpointResponseDto`** — outbound DTO returned to the caller.
- **`OperationVector`** — internal resolved vector; never returned to the frontend.
- **`RuntimeWorkingShape`** — internal-only working state for a single execution pass; never returned to the frontend, never persisted as a business fact.
- **`Emission`** — validated output carrying resolved identifiers and data.
- **`AttractorResult`** — output of attractor resolution mapping key → structure map/package/schema IDs.
- **`ValidationError`** — structured error with a `Code` and `Message`.

## How to Run

```bash
cd backend/
dotnet run
```

Requires .NET 8 or later.

## Database Dependency

All `TopologyRepository` methods are stubs that return `null`. A real implementation requires a database connection. Pass the connection string via the `TopologyRepository` constructor or configure it via dependency injection. The expected schema is defined in `/db/schema.sql` and `/db/topology_tables.sql`.

## Scope Note

All implementations in this layer are **skeleton stubs**. Real business logic — attractor table population, package/schema storage, semantic mapping rules, diff log persistence — is out of scope for this skeleton. The structural wiring, type contracts, and canonical execution path are the deliverables.
