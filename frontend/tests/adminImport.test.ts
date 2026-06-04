import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  listImportManifests,
  listImportSchemas,
  uploadImportPreview,
  applyImport,
  listImportSnapshotRecords,
  type AdminImportManifestItem,
  type AdminImportSchemaItem,
  type AdminImportPreviewResult,
  type AdminImportApplyResult,
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

// ---------------------------------------------------------------------------
// listImportManifests
// ---------------------------------------------------------------------------

Deno.test("listImportManifests uses admin_csv_json_import:list_manifests", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const manifests: AdminImportManifestItem[] = [
    { manifestId: "aaaa", status: "active", createdAt: "2024-01-01T00:00:00Z" },
  ];
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: manifests } },
    (_u, i) => { reqBody = String(i?.body ?? ""); },
  );
  try {
    const result = await listImportManifests();
    assertEquals(result, manifests);
    assertEquals(reqBody.includes('"layer":"admin_csv_json_import"'), true);
    assertEquals(reqBody.includes('"action":"list_manifests"'), true);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("listImportManifests returns null on 501 not configured", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, {
    success: false,
    errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }],
  });
  try {
    const result = await listImportManifests();
    assertEquals(result, null);
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// listImportSchemas
// ---------------------------------------------------------------------------

Deno.test("listImportSchemas uses admin_csv_json_import:list_schemas", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const schemas: AdminImportSchemaItem[] = [
    { schemaId: "bbbb", name: "default_schema" },
  ];
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: schemas } },
    (_u, i) => { reqBody = String(i?.body ?? ""); },
  );
  try {
    const result = await listImportSchemas();
    assertEquals(result, schemas);
    assertEquals(reqBody.includes('"action":"list_schemas"'), true);
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// uploadImportPreview
// ---------------------------------------------------------------------------

Deno.test("uploadImportPreview sends upload_preview and returns result", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const previewData: AdminImportPreviewResult = {
    ok: true,
    snapshotId: "snap-1",
    sourceType: "csv",
    manifestId: "m1",
    schemaId: "s1",
    validCount: 2,
    invalidCount: 0,
    records: [
      { rowIndex: 0, records: { name: "Alice" }, status: "valid", validationErrors: [] },
      { rowIndex: 1, records: { name: "Bob" }, status: "valid", validationErrors: [] },
    ],
  };
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: previewData } },
    (_u, i) => { reqBody = String(i?.body ?? ""); },
  );
  try {
    const result = await uploadImportPreview("csv", "test.csv", "m1", "s1", "name\nAlice\nBob");
    assertEquals(result.snapshotId, "snap-1");
    assertEquals(result.validCount, 2);
    assertEquals(reqBody.includes('"action":"upload_preview"'), true);
    assertEquals(reqBody.includes('"sourceType":"csv"'), true);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("uploadImportPreview throws on dispatch error", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, {
    success: false,
    errors: [{ message: "SCHEMA_NOT_FOUND" }],
  });
  try {
    await assertRejects(
      () => uploadImportPreview("csv", "test.csv", "m1", "s1", "name\nAlice"),
      Error,
      "SCHEMA_NOT_FOUND",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("uploadImportPreview throws DISPATCH_BACKEND_NOT_CONFIGURED on 501", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(501, {
    success: false,
    errors: [{ code: "DISPATCH_BACKEND_NOT_CONFIGURED", message: "not configured" }],
  });
  try {
    await assertRejects(
      () => uploadImportPreview("csv", "test.csv", "m1", "s1", "name\nAlice"),
      Error,
      "DISPATCH_BACKEND_NOT_CONFIGURED",
    );
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// applyImport
// ---------------------------------------------------------------------------

Deno.test("applyImport sends apply action with snapshotId", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const applyData: AdminImportApplyResult = {
    ok: true,
    applyLogId: "log-1",
    snapshotId: "snap-1",
    appliedRecordCount: 2,
    status: "staged",
    note: "MVP: canonical mutation not implemented",
  };
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: applyData } },
    (_u, i) => { reqBody = String(i?.body ?? ""); },
  );
  try {
    const result = await applyImport("snap-1");
    assertEquals(result.status, "staged");
    assertEquals(result.appliedRecordCount, 2);
    assertEquals(reqBody.includes('"action":"apply"'), true);
    assertEquals(reqBody.includes('"snapshotId":"snap-1"'), true);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("applyImport throws on SNAPSHOT_NOT_FOUND error", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeFetch(422, {
    success: false,
    errors: [{ message: "SNAPSHOT_NOT_FOUND" }],
  });
  try {
    await assertRejects(() => applyImport("bad-snap"), Error, "SNAPSHOT_NOT_FOUND");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("listImportSnapshotRecords uses admin_csv_json_import:list_snapshot_records", async () => {
  const original = globalThis.fetch;
  let reqBody = "";
  const preview: AdminImportPreviewResult = {
    ok: true,
    snapshotId: "snap-1",
    sourceType: "csv",
    manifestId: "m-1",
    schemaId: "s-1",
    validCount: 1,
    invalidCount: 0,
    records: [{
      rowIndex: 0,
      records: { name: "Alice" },
      status: "valid",
      validationErrors: [],
    }],
  };
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: preview } },
    (_u, i) => { reqBody = String(i?.body ?? ""); },
  );
  try {
    const result = await listImportSnapshotRecords("snap-1");
    assertEquals(result.records.length, 1);
    assertEquals(reqBody.includes('"action":"list_snapshot_records"'), true);
    assertEquals(reqBody.includes('"snapshotId":"snap-1"'), true);
  } finally {
    globalThis.fetch = original;
  }
});

// ---------------------------------------------------------------------------
// Frontend direct DB write guard
// ---------------------------------------------------------------------------

Deno.test("admin import API functions go through /api/dispatch not direct DB", async () => {
  const original = globalThis.fetch;
  const capturedUrls: string[] = [];
  const capturedBodies: string[] = [];
  globalThis.fetch = makeFetch(
    200,
    { success: true, emission: { data: [] } },
    (u, i) => {
      capturedUrls.push(String(u));
      capturedBodies.push(String(i?.body ?? ""));
    },
  );
  try {
    await listImportManifests();
    await listImportSchemas();
    // All captured URLs must be /api/dispatch (not direct DB or other endpoints)
    for (const url of capturedUrls) {
      assertEquals(
        url.includes("/api/dispatch") || url === "/api/dispatch",
        true,
        `URL should be /api/dispatch, got: ${url}`,
      );
    }
    // All requests must include triggerKind='client' and must not contain role
    for (const bodyStr of capturedBodies) {
      const body = JSON.parse(bodyStr);
      assertEquals(body.triggerKind, "client", "admin import ops must include triggerKind='client' (client_command_lane SSOT)");
      assertEquals("role" in body, false, "role must NOT be in frontend dispatch body; JWT claim is authoritative");
    }
  } finally {
    globalThis.fetch = original;
  }
});
