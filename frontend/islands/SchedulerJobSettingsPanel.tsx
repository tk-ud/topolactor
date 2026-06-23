/** @jsxImportSource preact */
import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  createSchedulerJob,
  disableSchedulerJob,
  fetchSchedulerJobManifests,
  type SchedulerJobManifestItem,
} from "../api/adminApi.ts";

// Authoring form draft state. Frontend holds NO runtime judgment / SQL / credential
// authority — it submits a manifest draft to the backend admin_runtime boundary only.
type DraftState = {
  jobKey: string;
  triggerKind: string;
  schedulePolicyKind: string;
  cronExpression: string;
  scheduleIntervalSeconds: string;
  manualRunAllowed: boolean;
  active: boolean;
  authorityScope: string;
  credentialRequirementRef: string;
  externalPortRef: string;
};

const EMPTY_DRAFT: DraftState = {
  jobKey: "",
  triggerKind: "cron",
  schedulePolicyKind: "manual_only",
  cronExpression: "",
  scheduleIntervalSeconds: "",
  manualRunAllowed: false,
  active: false,
  authorityScope: "",
  credentialRequirementRef: "",
  externalPortRef: "",
};

export default function SchedulerJobSettingsPanel(): JSX.Element {
  const [jobs, setJobs] = useState<SchedulerJobManifestItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [draft, setDraft] = useState<DraftState>(EMPTY_DRAFT);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  function reload() {
    setLoading(true);
    fetchSchedulerJobManifests()
      .then((result) => {
        if (result === null) setBackendUnavailable(true);
        else setJobs(result);
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }

  useEffect(reload, []);

  async function onCreate(e: Event) {
    e.preventDefault();
    setBusy(true);
    setNotice(null);
    setError(null);
    try {
      const result = await createSchedulerJob({
        jobKey: draft.jobKey,
        triggerKind: draft.triggerKind,
        schedulePolicyKind: draft.schedulePolicyKind,
        cronExpression: draft.cronExpression || null,
        scheduleIntervalSeconds: draft.scheduleIntervalSeconds
          ? Number(draft.scheduleIntervalSeconds)
          : null,
        manualRunAllowed: draft.manualRunAllowed,
        active: draft.active,
        authorityScope: draft.authorityScope,
        credentialRequirementRef: draft.credentialRequirementRef || null,
        externalPortRef: draft.externalPortRef || null,
      });
      setNotice(`Created scheduler job ${result.jobKey ?? draft.jobKey}.`);
      setDraft(EMPTY_DRAFT);
      reload();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function onDisable(schedulerJobId: string) {
    setBusy(true);
    setNotice(null);
    setError(null);
    try {
      await disableSchedulerJob(schedulerJobId);
      setNotice("Scheduler job disabled.");
      reload();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function set<K extends keyof DraftState>(key: K, value: DraftState[K]) {
    setDraft((d) => ({ ...d, [key]: value }));
  }

  if (loading) {
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

      {(!jobs || jobs.length === 0)
        ? <p>No scheduler jobs found.</p>
        : (
          <table>
            <thead>
              <tr>
                <th>Job Key</th>
                <th>Trigger Kind</th>
                <th>Schedule Policy</th>
                <th>Manual Run</th>
                <th>Active</th>
                <th>Max Batch</th>
                <th>Lease (s)</th>
                <th>Authority Scope</th>
                <th>Credential Ref</th>
                <th>External Port Ref</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((job) => (
                <tr key={job.schedulerJobId}>
                  <td>{job.jobKey}</td>
                  <td>{job.triggerKind}</td>
                  <td>{job.schedulePolicyKind}</td>
                  <td>{job.manualRunAllowed ? "yes" : "no"}</td>
                  <td>{job.active ? "yes" : "no"}</td>
                  <td>{job.maxBatchSize}</td>
                  <td>{job.leaseSeconds}</td>
                  <td>{job.authorityScope}</td>
                  <td>{job.credentialRequirementRef ?? "—"}</td>
                  <td>{job.externalPortRef ?? "—"}</td>
                  <td>
                    {job.active && (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => onDisable(job.schedulerJobId)}
                      >
                        Disable
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

      <h3>Create scheduler job</h3>
      <form class="scheduler-create-form" onSubmit={onCreate}>
        <label>
          Job Key
          <input
            type="text"
            value={draft.jobKey}
            onInput={(e) => set("jobKey", (e.target as HTMLInputElement).value)}
            required
          />
        </label>
        <label>
          Trigger Kind
          <select
            value={draft.triggerKind}
            onChange={(e) => set("triggerKind", (e.target as HTMLSelectElement).value)}
          >
            <option value="cron">cron</option>
            <option value="hook">hook</option>
            <option value="client">client</option>
          </select>
        </label>
        <label>
          Schedule Policy
          <select
            value={draft.schedulePolicyKind}
            onChange={(e) => set("schedulePolicyKind", (e.target as HTMLSelectElement).value)}
          >
            <option value="manual_only">manual_only</option>
            <option value="cron">cron</option>
            <option value="interval_seconds">interval_seconds</option>
          </select>
        </label>
        {draft.schedulePolicyKind === "cron" && (
          <label>
            Cron Expression
            <input
              type="text"
              value={draft.cronExpression}
              onInput={(e) => set("cronExpression", (e.target as HTMLInputElement).value)}
            />
          </label>
        )}
        {draft.schedulePolicyKind === "interval_seconds" && (
          <label>
            Interval (seconds)
            <input
              type="number"
              value={draft.scheduleIntervalSeconds}
              onInput={(e) => set("scheduleIntervalSeconds", (e.target as HTMLInputElement).value)}
            />
          </label>
        )}
        <label>
          Authority Scope
          <input
            type="text"
            value={draft.authorityScope}
            onInput={(e) => set("authorityScope", (e.target as HTMLInputElement).value)}
            required
          />
        </label>
        <label>
          Credential Requirement Ref (reference key only)
          <input
            type="text"
            value={draft.credentialRequirementRef}
            onInput={(e) => set("credentialRequirementRef", (e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          External Port Ref (reference key only)
          <input
            type="text"
            value={draft.externalPortRef}
            onInput={(e) => set("externalPortRef", (e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          <input
            type="checkbox"
            checked={draft.manualRunAllowed}
            onChange={(e) => set("manualRunAllowed", (e.target as HTMLInputElement).checked)}
          />
          Manual run allowed
        </label>
        <label>
          <input
            type="checkbox"
            checked={draft.active}
            onChange={(e) => set("active", (e.target as HTMLInputElement).checked)}
          />
          Active
        </label>
        <button type="submit" disabled={busy}>Create</button>
      </form>
    </div>
  );
}
