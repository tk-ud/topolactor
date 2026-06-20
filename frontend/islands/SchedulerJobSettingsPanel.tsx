/** @jsxImportSource preact */
import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  fetchSchedulerJobManifests,
  type SchedulerJobManifestItem,
} from "../api/adminApi.ts";

export default function SchedulerJobSettingsPanel(): JSX.Element {
  const [jobs, setJobs] = useState<SchedulerJobManifestItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [backendUnavailable, setBackendUnavailable] = useState(false);

  useEffect(() => {
    fetchSchedulerJobManifests()
      .then((result) => {
        if (result === null) {
          setBackendUnavailable(true);
        } else {
          setJobs(result);
        }
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

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

  if (error) {
    return (
      <div class="scheduler-settings-panel">
        <p>Error: {error}</p>
      </div>
    );
  }

  if (!jobs || jobs.length === 0) {
    return (
      <div class="scheduler-settings-panel">
        <p>No scheduler jobs found.</p>
      </div>
    );
  }

  return (
    <div class="scheduler-settings-panel">
      <h2>Scheduler Job Settings</h2>
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
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
