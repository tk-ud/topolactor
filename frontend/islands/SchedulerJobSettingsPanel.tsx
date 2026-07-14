/** @jsxImportSource preact */
import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  createSchedulerJob,
  disableSchedulerJob,
  editSchedulerJob,
  fetchSchedulerJobManifests,
  type SchedulerJobManifestItem,
  type SchedulerJobStepInput,
} from "../api/adminApi.ts";

// Authoring form draft state. Frontend holds NO runtime judgment / SQL / credential
// authority — it submits a manifest draft to the backend admin_runtime boundary only.
// Table/column authority is validated on the backend (identifier guards).
type StepDraft = {
  abstractFunctionKey: string;
  onError: string;
  resultContextKey: string;
  inputBindingJson: string;
  resultBindingJson: string;
  authorityScope: string;
  active: boolean;
};

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
  maxBatchSize: string;
  leaseSeconds: string;
  // input source / lifecycle
  inputTableRef: string;
  inputIdColumn: string;
  inputStatusColumn: string;
  inputDueColumn: string;
  inputStatusPendingValue: string;
  inputStatusProcessingValue: string;
  inputStatusCompletedValue: string;
  inputStatusFailedValue: string;
  inputStatusSkippedValue: string;
  inputStatusRetryWaitValue: string;
  // output + policies
  outputTableRef: string;
  retryPolicyJson: string;
  projectionPolicyJson: string;
  steps: StepDraft[];
};

const EMPTY_STEP: StepDraft = {
  abstractFunctionKey: "",
  onError: "fail_run",
  resultContextKey: "",
  inputBindingJson: "{}",
  resultBindingJson: "{}",
  authorityScope: "",
  active: true,
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
  maxBatchSize: "10",
  leaseSeconds: "300",
  inputTableRef: "",
  inputIdColumn: "id",
  inputStatusColumn: "",
  inputDueColumn: "",
  inputStatusPendingValue: "",
  inputStatusProcessingValue: "",
  inputStatusCompletedValue: "",
  inputStatusFailedValue: "",
  inputStatusSkippedValue: "",
  inputStatusRetryWaitValue: "",
  outputTableRef: "",
  retryPolicyJson: '{"max_attempts":3,"backoff_seconds":60}',
  projectionPolicyJson: "{}",
  steps: [],
};

function nonEmpty(s: string): string | undefined {
  return s.trim().length > 0 ? s.trim() : undefined;
}

export default function SchedulerJobSettingsPanel(): JSX.Element {
  const [jobs, setJobs] = useState<SchedulerJobManifestItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [draft, setDraft] = useState<DraftState>(EMPTY_DRAFT);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [editingJobId, setEditingJobId] = useState<string | null>(null);

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

  function buildSteps(): SchedulerJobStepInput[] {
    return draft.steps.map((s, i) => ({
      stepOrder: i + 1,
      abstractFunctionKey: s.abstractFunctionKey,
      onError: s.onError,
      resultContextKey: nonEmpty(s.resultContextKey) ?? null,
      // The frontend only carries the manifest binding text; the backend parses/validates it.
      inputBinding: JSON.parse(s.inputBindingJson || "{}"),
      resultBinding: JSON.parse(s.resultBindingJson || "{}"),
      authorityScope: nonEmpty(s.authorityScope) ?? null,
      active: s.active,
    }));
  }

  async function onCreate(e: Event) {
    e.preventDefault();
    setBusy(true);
    setNotice(null);
    setError(null);
    try {
      let steps: SchedulerJobStepInput[];
      let retryPolicy: Record<string, unknown> | undefined;
      let projectionPolicy: Record<string, unknown> | undefined;
      try {
        steps = buildSteps();
        retryPolicy = draft.retryPolicyJson.trim() ? JSON.parse(draft.retryPolicyJson) : undefined;
        projectionPolicy = draft.projectionPolicyJson.trim() ? JSON.parse(draft.projectionPolicyJson) : undefined;
      } catch (_jsonErr) {
        setError("Invalid JSON in a binding / policy field. Fix the JSON and retry.");
        setBusy(false);
        return;
      }
      const input = {
        jobKey: draft.jobKey,
        triggerKind: draft.triggerKind,
        schedulePolicyKind: draft.schedulePolicyKind,
        cronExpression: nonEmpty(draft.cronExpression) ?? null,
        scheduleIntervalSeconds: draft.scheduleIntervalSeconds ? Number(draft.scheduleIntervalSeconds) : null,
        manualRunAllowed: draft.manualRunAllowed,
        active: draft.active,
        authorityScope: draft.authorityScope,
        maxBatchSize: draft.maxBatchSize ? Number(draft.maxBatchSize) : undefined,
        leaseSeconds: draft.leaseSeconds ? Number(draft.leaseSeconds) : undefined,
        credentialRequirementRef: nonEmpty(draft.credentialRequirementRef) ?? null,
        externalPortRef: nonEmpty(draft.externalPortRef) ?? null,
        inputTableRef: nonEmpty(draft.inputTableRef) ?? null,
        inputIdColumn: nonEmpty(draft.inputIdColumn) ?? null,
        inputStatusColumn: nonEmpty(draft.inputStatusColumn) ?? null,
        inputDueColumn: nonEmpty(draft.inputDueColumn) ?? null,
        inputStatusPendingValue: nonEmpty(draft.inputStatusPendingValue) ?? null,
        inputStatusProcessingValue: nonEmpty(draft.inputStatusProcessingValue) ?? null,
        inputStatusCompletedValue: nonEmpty(draft.inputStatusCompletedValue) ?? null,
        inputStatusFailedValue: nonEmpty(draft.inputStatusFailedValue) ?? null,
        inputStatusSkippedValue: nonEmpty(draft.inputStatusSkippedValue) ?? null,
        inputStatusRetryWaitValue: nonEmpty(draft.inputStatusRetryWaitValue) ?? null,
        outputTableRef: nonEmpty(draft.outputTableRef) ?? null,
        retryPolicy,
        projectionPolicy,
        steps,
      };
      const result = editingJobId
        ? await editSchedulerJob(editingJobId, input)
        : await createSchedulerJob(input);
      setNotice(
        editingJobId
          ? `Updated scheduler job ${result.jobKey ?? draft.jobKey}.`
          : `Created scheduler job ${result.jobKey ?? draft.jobKey}.`,
      );
      setDraft(EMPTY_DRAFT);
      setEditingJobId(null);
      reload();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function onStartEdit(job: SchedulerJobManifestItem) {
    setEditingJobId(job.schedulerJobId);
    setNotice(null);
    setError(null);
    setDraft({
      ...EMPTY_DRAFT,
      jobKey: job.jobKey,
      triggerKind: job.triggerKind,
      schedulePolicyKind: job.schedulePolicyKind,
      cronExpression: job.cronExpression ?? "",
      scheduleIntervalSeconds: job.scheduleIntervalSeconds != null ? String(job.scheduleIntervalSeconds) : "",
      manualRunAllowed: job.manualRunAllowed,
      active: job.active,
      authorityScope: job.authorityScope,
      maxBatchSize: String(job.maxBatchSize),
      leaseSeconds: String(job.leaseSeconds),
      credentialRequirementRef: job.credentialRequirementRef ?? "",
      externalPortRef: job.externalPortRef ?? "",
    });
    globalThis.scrollTo?.({ top: document.body.scrollHeight, behavior: "smooth" });
  }

  function onCancelEdit() {
    setEditingJobId(null);
    setDraft(EMPTY_DRAFT);
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

  function setStep(idx: number, key: keyof StepDraft, value: string | boolean) {
    setDraft((d) => ({
      ...d,
      steps: d.steps.map((s, i) => (i === idx ? { ...s, [key]: value } : s)),
    }));
  }

  function addStep() {
    setDraft((d) => ({ ...d, steps: [...d.steps, { ...EMPTY_STEP }] }));
  }

  function removeStep(idx: number) {
    setDraft((d) => ({ ...d, steps: d.steps.filter((_, i) => i !== idx) }));
  }

  function textField(label: string, key: keyof DraftState, required = false) {
    return (
      <label>
        {label}
        <input
          type="text"
          value={draft[key] as string}
          onInput={(e) => set(key, (e.target as HTMLInputElement).value as DraftState[typeof key])}
          required={required}
        />
      </label>
    );
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
                      <button type="button" disabled={busy} onClick={() => onDisable(job.schedulerJobId)}>
                        Disable
                      </button>
                    )}
                    {!job.active && (
                      <button type="button" disabled={busy} onClick={() => onStartEdit(job)}>
                        Edit
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

      <h3>{editingJobId ? `Edit scheduler job manifest (${editingJobId})` : "Create scheduler job manifest"}</h3>
      {editingJobId && (
        <p class="scheduler-notice">
          Editing an inactive job requires re-specifying input/output/step fields — they are not
          returned by the settings list (credential-adjacent fields are read-redacted).
        </p>
      )}
      <form class="scheduler-create-form" onSubmit={onCreate}>
        <fieldset>
          <legend>Header</legend>
          {textField("Job Key", "jobKey", true)}
          <label>
            Trigger Kind
            <select value={draft.triggerKind} onChange={(e) => set("triggerKind", (e.target as HTMLSelectElement).value)}>
              <option value="cron">cron</option>
              <option value="hook">hook</option>
              <option value="client">client</option>
            </select>
          </label>
          <label>
            Schedule Policy
            <select value={draft.schedulePolicyKind} onChange={(e) => set("schedulePolicyKind", (e.target as HTMLSelectElement).value)}>
              <option value="manual_only">manual_only</option>
              <option value="cron">cron</option>
              <option value="interval_seconds">interval_seconds</option>
            </select>
          </label>
          {draft.schedulePolicyKind === "cron" && textField("Cron Expression", "cronExpression")}
          {draft.schedulePolicyKind === "interval_seconds" && textField("Interval (seconds)", "scheduleIntervalSeconds")}
          {textField("Authority Scope", "authorityScope", true)}
          {textField("Max Batch Size", "maxBatchSize")}
          {textField("Lease Seconds", "leaseSeconds")}
          {textField("Credential Requirement Ref (reference key only)", "credentialRequirementRef")}
          {textField("External Port Ref (reference key only)", "externalPortRef")}
          <label>
            <input type="checkbox" checked={draft.manualRunAllowed} onChange={(e) => set("manualRunAllowed", (e.target as HTMLInputElement).checked)} />
            Manual run allowed
          </label>
          <label>
            <input type="checkbox" checked={draft.active} onChange={(e) => set("active", (e.target as HTMLInputElement).checked)} />
            Active
          </label>
        </fieldset>

        <fieldset>
          <legend>Input source / lifecycle (optional)</legend>
          {textField("Input Table Ref (schema.table)", "inputTableRef")}
          {textField("Input Id Column", "inputIdColumn")}
          {textField("Input Status Column", "inputStatusColumn")}
          {textField("Input Due Column", "inputDueColumn")}
          {textField("Status: pending", "inputStatusPendingValue")}
          {textField("Status: processing", "inputStatusProcessingValue")}
          {textField("Status: completed", "inputStatusCompletedValue")}
          {textField("Status: failed", "inputStatusFailedValue")}
          {textField("Status: skipped", "inputStatusSkippedValue")}
          {textField("Status: retry_wait", "inputStatusRetryWaitValue")}
        </fieldset>

        <fieldset>
          <legend>Output binding + policies</legend>
          {textField("Output Table Ref (schema.table)", "outputTableRef")}
          <label>
            Retry policy (JSON)
            <textarea value={draft.retryPolicyJson} onInput={(e) => set("retryPolicyJson", (e.target as HTMLTextAreaElement).value)} />
          </label>
          <label>
            Projection policy (JSON)
            <textarea value={draft.projectionPolicyJson} onInput={(e) => set("projectionPolicyJson", (e.target as HTMLTextAreaElement).value)} />
          </label>
        </fieldset>

        <fieldset>
          <legend>Steps (ordered abstract function chain)</legend>
          {draft.steps.map((step, idx) => (
            <div class="scheduler-step" key={idx}>
              <strong>Step {idx + 1}</strong>
              <label>
                Abstract Function Key
                <input type="text" value={step.abstractFunctionKey} onInput={(e) => setStep(idx, "abstractFunctionKey", (e.target as HTMLInputElement).value)} />
              </label>
              <label>
                On Error
                <select value={step.onError} onChange={(e) => setStep(idx, "onError", (e.target as HTMLSelectElement).value)}>
                  <option value="fail_run">fail_run</option>
                  <option value="retry">retry</option>
                  <option value="skip_input">skip_input</option>
                  <option value="mark_input_failed">mark_input_failed</option>
                </select>
              </label>
              <label>
                Result Context Key
                <input type="text" value={step.resultContextKey} onInput={(e) => setStep(idx, "resultContextKey", (e.target as HTMLInputElement).value)} />
              </label>
              <label>
                Input Binding (JSON)
                <textarea value={step.inputBindingJson} onInput={(e) => setStep(idx, "inputBindingJson", (e.target as HTMLTextAreaElement).value)} />
              </label>
              <label>
                Result Binding (JSON)
                <textarea value={step.resultBindingJson} onInput={(e) => setStep(idx, "resultBindingJson", (e.target as HTMLTextAreaElement).value)} />
              </label>
              <label>
                <input type="checkbox" checked={step.active} onChange={(e) => setStep(idx, "active", (e.target as HTMLInputElement).checked)} />
                Active
              </label>
              <button type="button" onClick={() => removeStep(idx)}>Remove step</button>
            </div>
          ))}
          <button type="button" onClick={addStep}>Add step</button>
        </fieldset>

        <button type="submit" disabled={busy}>{editingJobId ? "Save edit" : "Create"}</button>
        {editingJobId && <button type="button" onClick={onCancelEdit} disabled={busy}>Cancel edit</button>}
      </form>
    </div>
  );
}
