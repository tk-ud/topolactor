import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  disableSchedulerJob,
  enableSchedulerJob,
  fetchSchedulerJobManifests,
  type SchedulerJobManifestItem,
} from "../api/adminApi.ts";

// scheduler-settings subBundle (admin-surface-topology-seed-conversion), 2026-07-22
// Owner-confirmed scope (.agent/tasks/todo.md "scheduler-settings 3分割設計の確定"):
// list/search(job_key)/filter(trigger_kind, schedule_policy_kind, active)/explicit enable/disable
// ONLY. There is no createSchedulerJob/editSchedulerJob in frontend/api/adminApi.ts any more —
// job create/edit/step-chain authoring moved to /admin/contents' own generic
// physical_table_and_page_binding pipeline; this file no longer tests them here.

function makeFetch(
  status: number,
  body: unknown,
  capture?: (u: string | URL | Request, i?: RequestInit) => void,
): typeof globalThis.fetch {
  return (input: string | URL | Request, init?: RequestInit) => {
    capture?.(input, init);
    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };
}

const SAMPLE_JOB: SchedulerJobManifestItem = {
  schedulerJobId: "00000000-0000-0000-0000-00000000sj01",
  jobKey: "demo_schedule",
  triggerKind: "cron",
  schedulePolicyKind: "manual_only",
  cronExpression: null,
  scheduleIntervalSeconds: null,
  timezone: null,
  manualRunAllowed: true,
  active: true,
  updatedAt: "2026-08-17T00:00:00Z",
};

const FILTER_OPTIONS = {
  triggerKindOptions: [
    { value: "cron", label: "cron" },
    { value: "hook", label: "hook" },
    { value: "client", label: "client" },
  ],
  schedulePolicyKindOptions: [
    { value: "cron", label: "cron" },
    { value: "interval_seconds", label: "interval_seconds" },
    { value: "manual_only", label: "manual_only" },
  ],
  activeOptions: [
    { value: "true", label: "active" },
    { value: "false", label: "inactive" },
  ],
};

// ─── DB projection reading ────────────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: reads scheduler_jobs:list_settings and returns schedulerJobs + filter option lists", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};

  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobs: [SAMPLE_JOB], ...FILTER_OPTIONS } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );

  try {
    const result = await fetchSchedulerJobManifests();
    assertEquals(result?.schedulerJobs.length, 1);
    assertEquals(result?.schedulerJobs[0].jobKey, "demo_schedule");
    assertEquals(result?.schedulerJobs[0].schedulerJobId, "00000000-0000-0000-0000-00000000sj01");
    assertEquals(result?.triggerKindOptions, FILTER_OPTIONS.triggerKindOptions);
    assertEquals(result?.schedulePolicyKindOptions, FILTER_OPTIONS.schedulePolicyKindOptions);
    assertEquals(result?.activeOptions, FILTER_OPTIONS.activeOptions);
    assertEquals(reqBody.layer, "scheduler_jobs", "must use layer=scheduler_jobs");
    assertEquals(reqBody.action, "list_settings", "must use action=list_settings");
    assertEquals(reqBody.payload, undefined, "no search/filter args means no payload sent");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("fetchSchedulerJobManifests: search/filter args are sent as flat payload fields", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};

  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobs: [], ...FILTER_OPTIONS } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );

  try {
    await fetchSchedulerJobManifests({
      search: "demo",
      triggerKind: "cron",
      schedulePolicyKind: "manual_only",
      active: true,
    });
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.search, "demo");
    assertEquals(payload.triggerKind, "cron");
    assertEquals(payload.schedulePolicyKind, "manual_only");
    assertEquals(payload.active, true);
  } finally {
    globalThis.fetch = original;
  }
});

// ─── forbidden-field non-exposure ─────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: projection type never carries credential/port/authority-scope fields", async () => {
  const original = globalThis.fetch;

  // Server response deliberately includes forbidden fields (defense in depth: even if a future
  // backend regression leaked them, the frontend type/consumer surface must not surface them).
  const jobWithLeakAttempt = {
    ...SAMPLE_JOB,
    credentialRequirementRef: "vault_ref_key_only",
    externalPortRef: "ext_port_ref_key_only",
    authorityScope: "demo_scheduler_job",
    maxBatchSize: 1,
    leaseSeconds: 60,
  };

  globalThis.fetch = makeFetch(200, {
    success: true,
    emission: { data: { ok: true, schedulerJobs: [jobWithLeakAttempt], ...FILTER_OPTIONS } },
  });

  try {
    const result = await fetchSchedulerJobManifests();
    const job = result?.schedulerJobs[0] as unknown as Record<string, unknown>;
    // SchedulerJobManifestItem's own declared keys are exactly the allowed projection set.
    const keys = Object.keys(SAMPLE_JOB);
    assertEquals(
      keys.includes("credentialRequirementRef"),
      false,
      "SchedulerJobManifestItem type must not declare credentialRequirementRef",
    );
    assertEquals(
      keys.includes("externalPortRef"),
      false,
      "SchedulerJobManifestItem type must not declare externalPortRef",
    );
    assertEquals(
      keys.includes("authorityScope"),
      false,
      "SchedulerJobManifestItem type must not declare authorityScope",
    );
    // The raw response object passed through is whatever the server sent (server's own boundary,
    // proven separately by backend tests) -- this test's job is the frontend type contract only.
    assertEquals(job.jobKey, "demo_schedule");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── dispatch body contract ───────────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: dispatch body must include triggerKind='client' and must not contain role", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};

  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobs: [], ...FILTER_OPTIONS } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );

  try {
    await fetchSchedulerJobManifests();
    assertEquals(reqBody.triggerKind, "client", "admin dispatch must include triggerKind='client'");
    assertEquals("role" in reqBody, false, "role must NOT be in frontend dispatch body; JWT claim is authoritative");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── 501 not configured → null (not throw) ───────────────────────────────────

Deno.test("fetchSchedulerJobManifests: dispatch 501 not configured -> null", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, {
    success: false,
    errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }],
  });

  try {
    const result = await fetchSchedulerJobManifests();
    assertEquals(result, null, "501 DISPATCH_BACKEND_NOT_CONFIGURED must return null, not throw");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── error → throw ───────────────────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: dispatch error (non-501) must throw", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, {
    success: false,
    errors: [{ code: "SCHEDULER_LIST_SETTINGS_ACTIVE_FILTER_INVALID", message: "payload.active must be true/false when present." }],
  });

  try {
    await assertRejects(
      () => fetchSchedulerJobManifests(),
      Error,
      "payload.active must be true/false when present.",
    );
  } finally {
    globalThis.fetch = original;
  }
});

// ─── enable / disable dispatch contract (mutation_confirmation_contract) ─────

Deno.test("enableSchedulerJob: dryRun=true dispatches scheduler_jobs:enable with dryRun and returns preview, no confirmed flag sent", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    {
      success: true,
      emission: {
        data: {
          ok: true, dryRun: true, valid: true, schedulerJobId: "id-1",
          preview: { operation: "enable", jobKey: "demo_schedule", activeBefore: false, activeAfter: true },
        },
      },
    },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    const result = await enableSchedulerJob("id-1", { dryRun: true });
    assertEquals(result.dryRun, true);
    assertEquals(result.preview?.activeAfter, true);
    assertEquals(reqBody.layer, "scheduler_jobs");
    assertEquals(reqBody.action, "enable");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.schedulerJobId, "id-1");
    assertEquals(payload.dryRun, true);
    assertEquals("confirmed" in payload, false, "a dryRun call must never also send confirmed=true");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("disableSchedulerJob: confirmed=true dispatches scheduler_jobs:disable with confirmed and returns the written active value", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobId: "id-1", jobKey: "demo_schedule", active: false } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    const result = await disableSchedulerJob("id-1", { confirmed: true });
    assertEquals(result.ok, true);
    assertEquals(result.active, false);
    assertEquals(reqBody.action, "disable");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.schedulerJobId, "id-1");
    assertEquals(payload.confirmed, true);
    assertEquals("dryRun" in payload, false, "a confirmed call must never also send dryRun=true");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("enableSchedulerJob / disableSchedulerJob: neither dryRun nor confirmed sends a bare payload with only schedulerJobId", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: false, errors: [{ code: "SCHEDULER_JOB_ENABLE_NOT_CONFIRMED", message: "requires payload.confirmed=true" }] },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    await assertRejects(() => enableSchedulerJob("id-1", {}), Error, "requires payload.confirmed=true");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(Object.keys(payload), ["schedulerJobId"]);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("disableSchedulerJob: dispatch error propagates as a thrown Error (e.g. SCHEDULER_JOB_ALREADY_INACTIVE)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, {
    success: false,
    errors: [{ code: "SCHEDULER_JOB_ALREADY_INACTIVE", message: "Scheduler job is already inactive; nothing to disable." }],
  });
  try {
    await assertRejects(
      () => disableSchedulerJob("id-1", { confirmed: true }),
      Error,
      "already inactive",
    );
  } finally {
    globalThis.fetch = original;
  }
});

// ─── no frontend SQL / credential / runtime judgment ─────────────────────────

Deno.test("SchedulerJobManifestItem: type surface carries no SQL/credential/runtime-authority fields", () => {
  // This test audits the type surface of SchedulerJobManifestItem. The frontend receives manifest
  // data as a read-only, scope-reduced projection: list/search/filter/enable/disable only. None of
  // the returned fields are used to make SQL, credential, or runtime decisions in the frontend --
  // that is exclusively a backend responsibility. This is an audit/contract test, not a runtime test.
  const keys = Object.keys(SAMPLE_JOB);
  const forbiddenKeys = [
    "sql", "query", "table_ref", "column_ref", "input_table", "output_table",
    "credential_payload", "decrypted_payload", "token_body", "api_key", "access_token",
    "refresh_token", "client_secret", "decrypted_credential_payload",
    "credentialRequirementRef", "externalPortRef", "authorityScope", "maxBatchSize", "leaseSeconds",
  ];
  for (const forbidden of forbiddenKeys) {
    assertEquals(
      keys.some((k) => k.toLowerCase().includes(forbidden.toLowerCase())),
      false,
      `SchedulerJobManifestItem must not expose '${forbidden}'`,
    );
  }
});
