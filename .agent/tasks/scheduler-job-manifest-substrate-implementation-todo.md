# Scheduler job manifest substrate implementation todo

Status: `partial`
Roadmap/status SSOT: `product.scheduler_job_manifest_substrate`
SSOT: `docs/design/scheduler-job-manifest-ssot.yaml`

## Audit basis

Read `AGENTS.md` before implementation. Treat SSOT docs as canonical; implementation and PR bodies are evidence surfaces, not the source of truth. This todo is Bundle-scoped and must not be split into helper/route/component atoms.

The previous `not_started` classification is stale. Current implementation already includes:

- canonical DDL for `topology.scheduler_jobs`, `topology.scheduler_job_steps`, `topology.scheduler_job_runs`, and `topology.scheduler_job_run_steps`
- `NpgsqlSchedulerJobManifestRepository`
- `SchedulerJobRunner`
- `CronScheduleEvaluator`
- `scheduler_job_runtime` execution context / `scheduler_context` binding tests
- read-only scheduler settings projection and frontend route
- demo/test scheduler seed proving cron poll -> AbstractFunctionExecutor -> DB NOTIFY representative path

Therefore this Bundle must be managed as `partial`, not `not_started`.

## Remaining problem

The substrate exists, but it is not yet an implemented completion Bundle under `docs/design/scheduler-job-manifest-ssot.yaml` because the following SSOT conditions are still incomplete:

- `admin.contents` authoring surface is not implemented for create/edit/disable scheduler job manifests; current UI is read-only `/admin/scheduler` settings projection.
- input source lease and input row lifecycle status transitions are not implemented as manifest-authorized table/column/status operations.
- retry / backoff / terminal status policy is not yet implemented as scheduler job runtime behavior.
- `scheduler_job_run_steps` exists as DDL but the runner does not yet write per-step ledger entries.
- current representative absorption is demo/test seed; at least one existing cron flow still needs real absorption or an explicit SSOT/TODO split that keeps existing cron absorption as a separate Bundle.
- compatibility external-port policy operations still exist and must not be treated as canonical scheduler/event/http primitive execution.

## Purpose

Close `scheduler-job-manifest-substrate-implementation` as a Bundle by making scheduler jobs fully data-authored and runtime-executed through manifest authority, not domain-specific C# job bodies or compatibility policy operations.

## Implementation plan

- [ ] Update `.agent/tasks/todo.md` status for `scheduler-job-manifest-substrate-implementation` from `not_started` to `partial` and replace the stale problem statement with the implementation reality above.
- [ ] Add `admin.contents` create/edit/disable authoring for scheduler job manifests using existing admin runtime / manifest dispatch boundaries; do not add a dedicated narrow backend route.
- [ ] Implement input source selection and lease acquisition using manifest-authorized `input_table_ref`, id/status/due columns, and explicit status values.
- [ ] Update input row lifecycle status only after lease acquisition; write processing/completed/failed/skipped/retry-wait values from manifest authority, not runtime defaults or payload.
- [ ] Implement retry/backoff/terminal status behavior from `retry_policy` and step `on_error`.
- [ ] Write `scheduler_job_run_steps` ledger entries for each abstract-function step execution with sanitized per-step result/error data.
- [ ] Keep `SchedulerJobRunner` free of SQL Attention / weather / retention / export domain body switches.
- [ ] Either absorb one existing cron flow into scheduler job manifest execution or explicitly split existing-cron absorption into a follow-up Bundle with SSOT-backed scope.
- [ ] Add backend tests for input lease/status transitions, retry policy, run_steps ledger, and no payload-derived table/column authority.
- [ ] Add frontend/admin tests proving scheduler job projection/authoring does not expose credential plaintext and does not make SQL/runtime decisions in the frontend.

## Materials

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/protocols/todo-carry-over.md`
- `docs/design/scheduler-job-manifest-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/db-schema.yaml`

## Target files

- `.agent/tasks/todo.md`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `db/demo/demo_scheduler.sql`
- `backend/scheduler/SchedulerJobRunner.cs`
- `backend/scheduler/CronScheduleEvaluator.cs`
- `backend/repository/NpgsqlSchedulerJobManifestRepository.cs`
- `backend/runtime/AdminRuntime.SchedulerSettings.cs`
- `backend/runtime/AbstractFunctionRuntime.cs`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/Program.cs`
- `frontend/routes/admin/contents.tsx`
- `frontend/routes/admin/scheduler.tsx`
- `frontend/islands/SchedulerJobSettingsPanel.tsx`
- `frontend/api/adminApi.ts`
- `backend/tests/Topolactor.Runtime.Tests/SchedulerJobRunnerTests.cs`
- `frontend/tests/schedulerJobManifestProjection.test.ts`

## Target functions/classes

- `SchedulerJobRunner.RunDueJobsAsync`
- `SchedulerJobRunner.TryExecuteJobAsync`
- `CronScheduleEvaluator.IsDue`
- `NpgsqlSchedulerJobManifestRepository.LoadActiveJobsAsync`
- `NpgsqlSchedulerJobManifestRepository.LoadStepsAsync`
- `NpgsqlSchedulerJobManifestRepository.CreateRunAsync`
- `NpgsqlSchedulerJobManifestRepository.UpdateRunStatusAsync`
- new repository methods for input lease/status and run-step ledger
- `AdminRuntime.DataListSchedulerJobsSettingsAsync`
- `AbstractFunctionExecutor.ExecuteAsync`
- `ExternalPortPolicyStepExecutor.ExecuteAsync`

## NG axis

- domain-specific scheduler job body in C#
- payload-derived table / column / output authority
- credential plaintext in manifest / projection / seed / run log / admin UI
- treating demo seed as existing cron absorption completion
- treating read-only `/admin/scheduler` projection as `admin.contents` authoring completion
- C# function-name arrays or switches as canonical step chain
- marking this Bundle implemented while input lease/status/retry/run_steps/admin authoring remain incomplete
