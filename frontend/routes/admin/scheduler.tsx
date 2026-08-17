import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import ProjectionShell from "../../islands/ProjectionShell.tsx";

// Route retirement (scheduler-settings subBundle, admin-surface-topology-seed-conversion): same URL
// (/admin/scheduler), no longer SchedulerJobSettingsPanel (deleted) -- a thin ProjectionShell wrapper
// pinned to the scheduler.settings.projection manifest (db/seed_empty.sql 5c1 family), the SAME
// generic production runtime every other projection surface uses, matching /admin/enums' and
// /admin/team-dashboard's own thin-wrapper shape.
//
// Scope note (docs/design/admin-normal-surface-projection-seed-ssot.yaml
// surface_axes.admin.surfaces.scheduler scope_boundary): this surface is list / search (job_key) /
// filter (trigger_kind, schedule_policy_kind, active) / explicit enable / explicit disable ONLY. The
// retired island also carried create/edit forms for scheduler job bodies and step chains; that
// authoring is NOT reproduced here -- it routes to /admin/contents' existing generic
// physical_table_and_page_binding pipeline over topology.scheduler_jobs / topology.scheduler_job_steps
// (scheduler_jobs:create/edit remain dispatcher-mapped and reachable there). credential_requirement_ref
// / external_port_ref binding belongs to the credential-management surface's own
// consumer_reference_binding, not here.
//
// Pinned by manifest_key, never a raw UUID constant: resolved backend-side by the generic
// manifest_key_target_ref_resolution_contract (ManifestDispatcher.cs, "exactly one active row" over
// hubs.topology_manifests.manifest_key). See docs/design/runtime-orchestration-ssot.yaml
// dispatcher_contract.
const SCHEDULER_SETTINGS_MANIFEST_KEY = "scheduler.settings.projection";

export default function AdminSchedulerRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <ProjectionShell manifestKey={SCHEDULER_SETTINGS_MANIFEST_KEY} />
    </AdminAuthGate>
  );
}
