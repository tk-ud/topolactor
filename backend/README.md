# Topolactor Backend

## Overview

The backend is the **abstract runtime layer**. It receives operations from the frontend (or any caller), aligns them through a timeline scheduler, resolves them through a manifest-driven pipeline, and routes effects to explicit output lanes. There are **no silent fallback paths**. Any broken manifest reference, missing attractor, or unresolvable package produces an explicit error response.

## Canonical Execution Path

```
EndpointRequestDto
  → RuntimeTimelineScheduler   (cron / hook / client trigger alignment; Channel-based queue)
    → ManifestDispatcher       (resolve active manifest by role + target + layer + action)
      → IDispatchableRuntime handler
          topology_transform_runtime → RuntimeExecutor
          admin_runtime              → AdminRuntimeDispatchAdapter → AdminRuntime

RuntimeExecutor canonical pipeline:
  → OperationVectorResolver
  → RuntimeGuard              (validation: target / layer / action / attractor_key)
  → AttractorResolver
  → StructureMapResolver
  → PackageResolver
  → SchemaResolver
  → SemanticMapper / DiffLogRepository
  → EmissionBuilder
  → OutputLaneRouter          (response / db_notify_emission / registry_attractor_update)
```

## Implementation Status

See `docs/system-roadmap.yaml` for feature-bundle status. Core runtime route and projection/output lanes remain partial at bundle level; SQL Attention (M7) is tracked as an observation/recommendation feedback capability with thin CI compatibility entries; admin topology authoring (M5) remains partial; the self-hosted no-code authoring loop (M6) is implemented but not production_ready while manual combined-UX acceptance is pending.

## Key Class Roles

| Class | Role |
|---|---|
| `RuntimeTimelineScheduler` | Aligns cron / hook / client triggers in one Channel-based queue. Routes to ManifestDispatcher. |
| `ManifestDispatcher` | Resolves active manifest from DB by role + target + layer + action. Dispatches to registered `IDispatchableRuntime` handler. Returns explicit error for MANIFEST_NOT_FOUND / MANIFEST_AMBIGUOUS / RUNTIME_DESTINATION_UNKNOWN. |
| `RuntimeExecutor` | Canonical pipeline orchestrator. No target/layer/action dispatch branches. Registered as handler for `topology_transform_runtime`. |
| `AdminRuntime` / `AdminRuntimeDispatchAdapter` | Admin operation handler registered for `admin_runtime` destination. Covers seed, bucket, package generation, and system CI operations. |
| `OperationVectorResolver` | Maps `EndpointRequestDto` fields to `OperationVector`. Derives `AttractorKey` as `target:layer:action`. |
| `RuntimeGuard` | Validates `OperationVector` before resolution begins. |
| `AttractorResolver` | Loads structure map record by `AttractorKey`. Throws if not found. |
| `StructureMapResolver` | Loads full structure map by ID; constructs `RuntimeWorkingShape`. |
| `PackageResolver` | Loads package definition by `PackageId`. Throws if missing. |
| `SchemaResolver` | Loads schema definition by `SchemaId`. Throws if missing. |
| `EmissionBuilder` | Constructs final `Emission` from fully resolved `RuntimeWorkingShape`. |
| `OutputLaneRouter` | Routes post-emission effects: response, `db_notify_emission`, `registry_attractor_update`. |
| `SseProjectionRuntime` | Handles `sse_projection_runtime` destination; broadcasts to `SseEventBroadcaster`. |
| `HubAttractorExplorationRuntime` | SQL Attention hub-attractor exploration scheduler runtime. |
| `SqlAttentionScheduler` | Scheduler for SQL Attention observation cycles. |
| `DbNotifyListener` | Routes pg_notify events through `RuntimeTimelineScheduler` as hook triggers. |
| `SystemOperationCiRuntime` | Admin-callable system CI diagnostics surface. |

## How to Run

```sh
# From repo root — start Postgres first
bash .agent/scripts/bootstrap-local-postgres.sh

# Then run backend
source ~/.topolactor-tools/env.sh
cd backend
dotnet run
```

See `.agent/protocols/claude.md` for environment setup details when running in an ephemeral container.

## Database

Real DB-backed Npgsql repositories (`NpgsqlManifestRepository`, `NpgsqlTopologyRepository`, `NpgsqlUiTopologyRepository`, etc.) are wired in `Program.cs`. Schema setup and seed order are documented in `db/README.md`.

The `default:entity:search` attractor resolves through the full canonical pipeline using the seed topology.
