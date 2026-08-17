/** @jsxImportSource preact */
import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  disableSchedulerJob,
  enableSchedulerJob,
  fetchSchedulerJobManifests,
  type SchedulerJobFilterOptions,
  type SchedulerJobManifestItem,
} from "../api/adminApi.ts";

// scheduler-settings subBundle (admin-surface-topology-seed-conversion): scope reduction, not
// retirement. 2026-07-22 Owner-confirmed design (.agent/tasks/todo.md, "scheduler-settings 3分割設計
// の確定") — this panel keeps its existing hardcoded route (/admin/scheduler) and does NOT become a
// seed-backed ProjectionShell wrapper. Its own responsibility is reduced to exactly
// docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.scheduler's
// scope_boundary: list / search (job_key) / filter (trigger_kind, schedule_policy_kind, active) /
// explicit enable / explicit disable. The removed create/edit/step-chain authoring form routes
// through /admin/contents' existing generic physical_table_and_page_binding pipeline instead
// (scheduler_jobs:create/edit remain dispatcher-mapped there, unchanged). credential_requirement_ref
// / external_port_ref binding belongs to the credential-management surface's own
// consumer_reference_binding, not here — this panel's projection never requests those fields (backend
// forbidden_projection_fields).
//
// mutation_confirmation_contract (docs/design/admin-normal-surface-projection-seed-ssot.yaml
// surface_axes.admin.surfaces.scheduler.seed_contract.mutation_confirmation_contract:
// [explicit_confirm, write, diff_log]): clicking Enable/Disable first dispatches a non-mutating
// dryRun preview; only a successful preview reveals the row's own inline confirm/cancel controls,
// and only Confirm dispatches the real write (payload.confirmed=true). Cancel dispatches nothing.

type PendingToggle = {
  schedulerJobId: string;
  operation: "enable" | "disable";
  jobKey: string;
};

const EMPTY_FILTER_OPTIONS: SchedulerJobFilterOptions = {
  triggerKindOptions: [],
  schedulePolicyKindOptions: [],
  activeOptions: [],
};

export default function SchedulerJobSettingsPanel(): JSX.Element {
  const [jobs, setJobs] = useState<SchedulerJobManifestItem[] | null>(null);
  const [filterOptions, setFilterOptions] = useState<SchedulerJobFilterOptions>(EMPTY_FILTER_OPTIONS);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [triggerKindFilter, setTriggerKindFilter] = useState("");
  const [schedulePolicyKindFilter, setSchedulePolicyKindFilter] = useState("");
  const [activeFilter, setActiveFilter] = useState("");

  const [pendingToggle, setPendingToggle] = useState<PendingToggle | null>(null);

  function reload() {
    setLoading(true);
    fetchSchedulerJobManifests({
      search: search.trim() || undefined,
      triggerKind: triggerKindFilter || undefined,
      schedulePolicyKind: schedulePolicyKindFilter || undefined,
      active: activeFilter === "" ? undefined : activeFilter === "true",
    })
      .then((result) => {
        if (result === null) {
          setBackendUnavailable(true);
          return;
        }
        setJobs(result.schedulerJobs);
        setFilterOptions({
          triggerKindOptions: result.triggerKindOptions,
          schedulePolicyKindOptions: result.schedulePolicyKindOptions,
          activeOptions: result.activeOptions,
        });
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }

  // deno-lint-ignore react-hooks/exhaustive-deps
  useEffect(reload, [search, triggerKindFilter, schedulePolicyKindFilter, activeFilter]);

  async function onRequestToggle(job: SchedulerJobManifestItem) {
    const operation = job.active ? "disable" : "enable";
    setBusy(true);
    setNotice(null);
    setError(null);
    setPendingToggle(null);
    try {
      const dispatch = operation === "enable" ? enableSchedulerJob : disableSchedulerJob;
      const result = await dispatch(job.schedulerJobId, { dryRun: true });
      if (result.valid) {
        setPendingToggle({ schedulerJobId: job.schedulerJobId, operation, jobKey: job.jobKey });
      } else {
        setError(`Cannot ${operation} scheduler job ${job.jobKey}.`);
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function onConfirmToggle() {
    if (!pendingToggle) return;
    const { schedulerJobId, operation, jobKey } = pendingToggle;
    setBusy(true);
    setNotice(null);
    setError(null);
    try {
      const dispatch = operation === "enable" ? enableSchedulerJob : disableSchedulerJob;
      await dispatch(schedulerJobId, { confirmed: true });
      setNotice(`Scheduler job ${jobKey} ${operation}d.`);
      setPendingToggle(null);
      reload();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function onCancelToggle() {
    setPendingToggle(null);
  }

  if (loading && jobs === null) {
    return <div class="scheduler-settings-panel"><p>Loading scheduler jobs...</p></div>;
  }

  if (backendUnavailable) {
    return (
      <div class="scheduler-settings-panel">
        <p>Scheduler job manifest backend is not configured.</p>
      </div>
    );
  }

  return (
    <div class="scheduler-settings-panel">
      <h2>Scheduler Job Settings</h2>
      {error && <p class="scheduler-error">Error: {error}</p>}
      {notice && <p class="scheduler-notice">{notice}</p>}

      <div class="scheduler-search-filter">
        <label>
          Search (job key)
          <input
            type="search"
            value={search}
            onInput={(e) => setSearch((e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          Trigger Kind
          <select
            value={triggerKindFilter}
            onChange={(e) => setTriggerKindFilter((e.target as HTMLSelectElement).value)}
          >
            <option value="">All</option>
            {filterOptions.triggerKindOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
        <label>
          Schedule Policy
          <select
            value={schedulePolicyKindFilter}
            onChange={(e) => setSchedulePolicyKindFilter((e.target as HTMLSelectElement).value)}
          >
            <option value="">All</option>
            {filterOptions.schedulePolicyKindOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
        <label>
          Active
          <select
            value={activeFilter}
            onChange={(e) => setActiveFilter((e.target as HTMLSelectElement).value)}
          >
            <option value="">All</option>
            {filterOptions.activeOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
      </div>

      {(!jobs || jobs.length === 0)
        ? <p>No scheduler jobs found.</p>
        : (
          <table>
            <thead>
              <tr>
                <th>Job Key</th>
                <th>Trigger Kind</th>
                <th>Schedule Policy</th>
                <th>Cron Expression</th>
                <th>Interval (s)</th>
                <th>Timezone</th>
                <th>Manual Run</th>
                <th>Active</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((job) => (
                <tr key={job.schedulerJobId}>
                  <td>{job.jobKey}</td>
                  <td>{job.triggerKind}</td>
                  <td>{job.schedulePolicyKind}</td>
                  <td>{job.cronExpression ?? "—"}</td>
                  <td>{job.scheduleIntervalSeconds ?? "—"}</td>
                  <td>{job.timezone ?? "—"}</td>
                  <td>{job.manualRunAllowed ? "yes" : "no"}</td>
                  <td>{job.active ? "yes" : "no"}</td>
                  <td>{job.updatedAt}</td>
                  <td>
                    {pendingToggle?.schedulerJobId === job.schedulerJobId
                      ? (
                        <span class="scheduler-toggle-confirm">
                          Confirm {pendingToggle.operation} {job.jobKey}?
                          <button type="button" disabled={busy} onClick={onConfirmToggle}>
                            Confirm
                          </button>
                          <button type="button" disabled={busy} onClick={onCancelToggle}>
                            Cancel
                          </button>
                        </span>
                      )
                      : (
                        <button type="button" disabled={busy} onClick={() => onRequestToggle(job)}>
                          {job.active ? "Disable" : "Enable"}
                        </button>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
    </div>
  );
}
