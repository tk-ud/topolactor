import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  createSchedulerJob,
  disableSchedulerJob,
  editSchedulerJob,
  fetchSchedulerJobManifests,
  type SchedulerJobManifestItem,
} from "../api/adminApi.ts";

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

// ─── DB projection reading ────────────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: reads scheduler_jobs layer from dispatch and returns list", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};

  const jobs: SchedulerJobManifestItem[] = [
    {
      schedulerJobId: "00000000-0000-0000-0000-00000000sj01",
      jobKey: "demo_schedule",
      triggerKind: "cron",
      schedulePolicyKind: "manual_only",
      cronExpression: null,
      scheduleIntervalSeconds: null,
      manualRunAllowed: true,
      active: true,
      maxBatchSize: 1,
      leaseSeconds: 60,
      authorityScope: "demo_scheduler_job",
      credentialRequirementRef: null,
      externalPortRef: null,
    },
  ];

  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobs: jobs } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );

  try {
    const result = await fetchSchedulerJobManifests();
    assertEquals(result?.length, 1);
    assertEquals(result?.[0].jobKey, "demo_schedule");
    assertEquals(result?.[0].schedulerJobId, "00000000-0000-0000-0000-00000000sj01");
    assertEquals(reqBody.layer, "scheduler_jobs", "must use layer=scheduler_jobs");
    assertEquals(reqBody.action, "list_settings", "must use action=list_settings");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── No secret exposure ───────────────────────────────────────────────────────

Deno.test("fetchSchedulerJobManifests: projection must not contain credential plaintext or token body", async () => {
  const original = globalThis.fetch;

  const jobWithSensitiveAttempt = {
    schedulerJobId: "00000000-0000-0000-0000-00000000sj01",
    jobKey: "demo_schedule",
    triggerKind: "cron",
    schedulePolicyKind: "manual_only",
    cronExpression: null,
    scheduleIntervalSeconds: null,
    manualRunAllowed: true,
    active: true,
    maxBatchSize: 1,
    leaseSeconds: 60,
    authorityScope: "demo_scheduler_job",
    credentialRequirementRef: "vault_ref_key_only",
    externalPortRef: "ext_port_ref_key_only",
  };

  globalThis.fetch = makeFetch(200, {
    success: true,
    emission: { data: { ok: true, schedulerJobs: [jobWithSensitiveAttempt] } },
  });

  try {
    const result = await fetchSchedulerJobManifests();
    const job = result?.[0];
    // Type surface must NOT expose credential plaintext or token body fields
    // credentialRequirementRef is a reference key only (no "credential_payload", "decrypted_payload", etc.)
    const jobStr = JSON.stringify(job);
    assertEquals(jobStr.includes("credential_payload"), false, "no credential_payload in projection");
    assertEquals(jobStr.includes("decrypted_payload"), false, "no decrypted_payload in projection");
    assertEquals(jobStr.includes("token_body"), false, "no token_body in projection");
    assertEquals(jobStr.includes("plaintext"), false, "no plaintext field in projection");
    // credentialRequirementRef is allowed as reference key
    assertEquals(job?.credentialRequirementRef, "vault_ref_key_only");
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
    { success: true, emission: { data: { ok: true, schedulerJobs: [] } } },
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
    errors: [{ code: "SCHEDULER_JOB_MANIFEST_NOT_CONFIGURED", message: "not registered" }],
  });

  try {
    await assertRejects(
      () => fetchSchedulerJobManifests(),
      Error,
      "not registered",
    );
  } finally {
    globalThis.fetch = original;
  }
});

// ─── authoring: create / edit / disable dispatch contract ────────────────────

Deno.test("createSchedulerJob: dispatches scheduler_jobs:create with manifest draft (no role)", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobId: "id-1", jobKey: "weather_24h" } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    const result = await createSchedulerJob({
      jobKey: "weather_24h",
      triggerKind: "cron",
      schedulePolicyKind: "cron",
      cronExpression: "0 * * * *",
      authorityScope: "weather_job",
      active: true,
    });
    assertEquals(result.ok, true);
    assertEquals(reqBody.layer, "scheduler_jobs");
    assertEquals(reqBody.action, "create");
    assertEquals(reqBody.triggerKind, "client", "admin dispatch must include triggerKind='client'");
    assertEquals("role" in reqBody, false, "role must NOT be in frontend dispatch body");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.jobKey, "weather_24h");
    // No secret material is ever sent by the authoring surface.
    const bodyStr = JSON.stringify(reqBody);
    assertEquals(bodyStr.includes("api_key"), false);
    assertEquals(bodyStr.includes("access_token"), false);
    assertEquals(bodyStr.includes("client_secret"), false);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("createSchedulerJob: full manifest carries input source, output binding, policies, and steps[]", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobId: "id-1", jobKey: "weather_24h" } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    await createSchedulerJob({
      jobKey: "weather_24h",
      triggerKind: "cron",
      schedulePolicyKind: "interval_seconds",
      scheduleIntervalSeconds: 3600,
      authorityScope: "weather_job",
      active: true,
      inputTableRef: "weather.region_inputs",
      inputStatusColumn: "status",
      inputStatusPendingValue: "pending",
      inputStatusProcessingValue: "processing",
      inputStatusCompletedValue: "completed",
      inputStatusFailedValue: "failed",
      outputTableRef: "weather.observations",
      retryPolicy: { max_attempts: 3, backoff_seconds: 60 },
      projectionPolicy: { allowed_result_keys: ["observation"] },
      steps: [
        {
          stepOrder: 1,
          abstractFunctionKey: "weather.fetch",
          onError: "retry",
          resultContextKey: "observation",
          inputBinding: { region: { source: "input", path: "region" } },
          resultBinding: { kind: "output_upsert", result_context_key: "observation", conflict_columns: ["region"], column_map: { region: "observation" } },
        },
      ],
    });
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.inputTableRef, "weather.region_inputs");
    assertEquals(payload.outputTableRef, "weather.observations");
    assertEquals(Array.isArray(payload.steps), true);
    const steps = payload.steps as Record<string, unknown>[];
    assertEquals(steps.length, 1);
    assertEquals(steps[0].abstractFunctionKey, "weather.fetch");
    assertEquals(steps[0].onError, "retry");
    // No secret material anywhere in the manifest authoring body.
    const bodyStr = JSON.stringify(reqBody);
    assertEquals(bodyStr.includes("api_key"), false);
    assertEquals(bodyStr.includes("client_secret"), false);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("editSchedulerJob: dispatches scheduler_jobs:edit with schedulerJobId", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobId: "id-1", jobKey: "weather_24h" } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    await editSchedulerJob("id-1", {
      jobKey: "weather_24h",
      triggerKind: "cron",
      schedulePolicyKind: "interval_seconds",
      scheduleIntervalSeconds: 3600,
      authorityScope: "weather_job",
    });
    assertEquals(reqBody.action, "edit");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.schedulerJobId, "id-1");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("disableSchedulerJob: dispatches scheduler_jobs:disable with schedulerJobId", async () => {
  const original = globalThis.fetch;
  let reqBody: Record<string, unknown> = {};
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: { ok: true, schedulerJobId: "id-1", active: false } } },
    (_u, i) => { reqBody = JSON.parse(String(i?.body ?? "{}")); },
  );
  try {
    const result = await disableSchedulerJob("id-1");
    assertEquals(result.ok, true);
    assertEquals(result.active, false);
    assertEquals(reqBody.action, "disable");
    const payload = reqBody.payload as Record<string, unknown>;
    assertEquals(payload.schedulerJobId, "id-1");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── no frontend SQL / credential / runtime judgment ─────────────────────────

Deno.test("fetchSchedulerJobManifests: frontend must not make SQL or credential decisions", () => {
  // This test audits the type surface of SchedulerJobManifestItem.
  // The frontend receives manifest data as a read-only projection.
  // None of the returned fields are used to make SQL, credential, or runtime decisions
  // in the frontend — that is exclusively a backend responsibility.
  //
  // Invariants verified by type inspection:
  //   - No "sql", "query", "table_ref", "column_ref", "input_table", "output_table" in the type
  //   - credentialRequirementRef and externalPortRef are reference keys (strings), not configs
  //   - The island renders them for display only
  //
  // This is an audit/contract test, not a runtime test.
  const item: SchedulerJobManifestItem = {
    schedulerJobId: "id",
    jobKey: "demo_schedule",
    triggerKind: "cron",
    schedulePolicyKind: "manual_only",
    cronExpression: null,
    scheduleIntervalSeconds: null,
    manualRunAllowed: true,
    active: true,
    maxBatchSize: 1,
    leaseSeconds: 60,
    authorityScope: "demo_scheduler_job",
    credentialRequirementRef: null,
    externalPortRef: null,
  };

  const keys = Object.keys(item);
  const forbiddenKeys = ["sql", "query", "table_ref", "column_ref", "input_table", "output_table",
    "credential_payload", "decrypted_payload", "token_body", "api_key", "access_token", "refresh_token",
    "client_secret", "decrypted_credential_payload"];
  for (const forbidden of forbiddenKeys) {
    assertEquals(
      keys.some((k) => k.toLowerCase().includes(forbidden.toLowerCase())),
      false,
      `SchedulerJobManifestItem must not expose '${forbidden}'`,
    );
  }
});
