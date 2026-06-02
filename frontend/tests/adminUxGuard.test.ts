import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ADMIN_MANIFESTS_GUIDE,
  ADMIN_HUB_NAVIGATION_GUIDE,
  ADMIN_MAIN_FLOW_STEPS,
} from "../content/adminGuides.ts";
import {
  UX_STATUS_LABELS,
  UX_ACTION_LABELS,
  UX_RUNTIME_DESTINATION_LABELS,
  UX_FIELD_TABLE_REF,
  UX_FIELD_IMPORT_SCHEMA,
  UX_FIELD_NULLABLE,
} from "../content/adminUxTerms.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

// ─── Banned terms guard ───────────────────────────────────────────────────────
// These technical terms must not appear in primary-visible guide text.

const BANNED_PRIMARY_TERMS = [
  "hub_relation",
  "dispatcher axes",
  "runtime_destination",
  "projection_constructor_mapping",
  "SQL Attention",
  "cosine",
  "UUID",
  "blocking error",
];

function collectPrimaryText(guide: {
  purpose: string;
  howToSteps: string[];
  inputs: string[];
}): string {
  return [guide.purpose, ...guide.howToSteps, ...guide.inputs].join(" ");
}

BANNED_PRIMARY_TERMS.forEach((term) => {
  Deno.test(`ADMIN_MANIFESTS_GUIDE primary text does not contain "${term}"`, () => {
    const text = collectPrimaryText(ADMIN_MANIFESTS_GUIDE);
    assertFalse(
      text.includes(term),
      `Primary text must not contain "${term}"`,
    );
  });
});

BANNED_PRIMARY_TERMS.forEach((term) => {
  Deno.test(`ADMIN_HUB_NAVIGATION_GUIDE primary text does not contain "${term}"`, () => {
    const text = collectPrimaryText(ADMIN_HUB_NAVIGATION_GUIDE);
    assertFalse(
      text.includes(term),
      `Primary text must not contain "${term}"`,
    );
  });
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: does not contain hub_relation in purpose or completionSign", () => {
  const text = ADMIN_MAIN_FLOW_STEPS
    .map((s) => s.purpose + " " + s.completionSign)
    .join(" ");
  assertFalse(text.includes("hub_relation"), "Flow steps must not contain hub_relation");
});

Deno.test("ADMIN_HUB_NAVIGATION_GUIDE title does not contain hub_relation", () => {
  assertFalse(
    ADMIN_HUB_NAVIGATION_GUIDE.title.includes("hub_relation"),
    "Guide title must not expose hub_relation",
  );
});

// ─── promoteDisabled logic guard ──────────────────────────────────────────────
// validate must be run before promote is enabled.

function computePromoteDisabled(
  selectedId: string,
  status: string,
  validation: { isBlocking: boolean } | null,
): boolean {
  return !selectedId || status !== "draft" || validation === null || validation.isBlocking;
}

Deno.test("promoteDisabled: disabled when validation is null (not yet validated)", () => {
  assertEquals(
    computePromoteDisabled("some-id", "draft", null),
    true,
    "promote must be disabled when validation has not been run",
  );
});

Deno.test("promoteDisabled: enabled when validation passed without blocking", () => {
  assertEquals(
    computePromoteDisabled("some-id", "draft", { isBlocking: false }),
    false,
    "promote must be enabled when validation passed",
  );
});

Deno.test("promoteDisabled: disabled when validation is blocking", () => {
  assertEquals(
    computePromoteDisabled("some-id", "draft", { isBlocking: true }),
    true,
    "promote must be disabled when validation is blocking",
  );
});

Deno.test("promoteDisabled: disabled when status is not draft", () => {
  assertEquals(
    computePromoteDisabled("some-id", "active", { isBlocking: false }),
    true,
    "promote must be disabled for non-draft status",
  );
});

Deno.test("promoteDisabled: disabled when no id selected", () => {
  assertEquals(
    computePromoteDisabled("", "draft", { isBlocking: false }),
    true,
    "promote must be disabled when no id selected",
  );
});

// Contract: handleSaveDraft calls setValidation(null), so after a draft save
// validation is null — promote stays disabled until the user explicitly re-validates.
Deno.test("promoteDisabled: after draft save validation resets to null — disabled until re-validated", () => {
  // State immediately after handleSaveDraft (validation cleared to null)
  assertEquals(
    computePromoteDisabled("some-id", "draft", null),
    true,
    "promote must be disabled immediately after draft save (validation cleared)",
  );
  // State after user explicitly re-runs validate with no blocking errors
  assertEquals(
    computePromoteDisabled("some-id", "draft", { isBlocking: false }),
    false,
    "promote must be enabled only after re-validation passes",
  );
});

// ─── HubNavigation sequence auto-set guard ────────────────────────────────────

function computeNextSequencePosition(activeRelations: { status: string }[]): number {
  return activeRelations.filter((hr) => hr.status === "active").length + 1;
}

Deno.test("HubNavigation: new entry sequence auto-set to end", () => {
  const active = [
    { status: "active" },
    { status: "active" },
  ];
  assertEquals(computeNextSequencePosition(active), 3, "should auto-position at end");
});

Deno.test("HubNavigation: first entry sequence defaults to 1", () => {
  assertEquals(computeNextSequencePosition([]), 1, "first entry should be at position 1");
});

Deno.test("HubNavigation: deprecated entries excluded from auto-position count", () => {
  const mixed = [
    { status: "active" },
    { status: "deprecated" },
    { status: "active" },
  ];
  assertEquals(
    computeNextSequencePosition(mixed),
    3,
    "deprecated entries must not affect next position",
  );
});

// ─── Catalog registration auto-fill guard ─────────────────────────────────────
// Catalog entries with registrationRequired:true must have sourcePath/componentKind
// so that auto-fill works without hand-input.

Deno.test("catalog: registration-required entries have sourcePath for auto-fill", () => {
  const required = COMPONENT_CATALOG_ENTRIES.filter((e) => e.registrationRequired);
  for (const e of required) {
    assertFalse(
      !e.sourcePath || e.sourcePath === "",
      `Entry "${e.componentKey}" must have a sourcePath for auto-fill`,
    );
  }
});

Deno.test("catalog: registration-required entries have componentKind for auto-fill", () => {
  const required = COMPONENT_CATALOG_ENTRIES.filter((e) => e.registrationRequired);
  for (const e of required) {
    assertFalse(
      !e.componentKind || e.componentKind === "",
      `Entry "${e.componentKey}" must have a componentKind for auto-fill`,
    );
  }
});

// ─── UX term dictionary completeness ─────────────────────────────────────────

Deno.test("UX_STATUS_LABELS: covers draft/active/deprecated", () => {
  assertEquals(UX_STATUS_LABELS["draft"], "下書き");
  assertEquals(UX_STATUS_LABELS["active"], "有効");
  assertEquals(UX_STATUS_LABELS["deprecated"], "利用停止");
});

Deno.test("UX_ACTION_LABELS: covers validate/promote", () => {
  assertEquals(UX_ACTION_LABELS["validate"], "内容を確認");
  assertEquals(UX_ACTION_LABELS["promote"], "有効化");
});

Deno.test("UX_RUNTIME_DESTINATION_LABELS: covers all runtime destination options", () => {
  assertEquals(UX_RUNTIME_DESTINATION_LABELS["topology_transform_runtime"], "通常ルーティング");
  assertEquals(UX_RUNTIME_DESTINATION_LABELS["admin_runtime"], "管理機能");
  assertEquals(UX_RUNTIME_DESTINATION_LABELS["sse_projection_runtime"], "リアルタイム投影");
});

// ─── Advanced/details: can still hold raw technical information ────────────────

Deno.test("ADMIN_MANIFESTS_GUIDE boundaryNotes can hold implementation notes", () => {
  // boundaryNotes are not primary text — technical info is allowed there
  assertEquals(Array.isArray(ADMIN_MANIFESTS_GUIDE.boundaryNotes), true);
});

Deno.test("ADMIN_HUB_NAVIGATION_GUIDE boundaryNotes can hold technical scope notes", () => {
  assertEquals(Array.isArray(ADMIN_HUB_NAVIGATION_GUIDE.boundaryNotes), true);
});

// ─── ContentsScreenDesignPanel field vocabulary regression ────────────────────
// Internal technical terms must not appear in the user-facing label constants.

Deno.test("UX_FIELD_TABLE_REF: uses user-friendly label 参照テーブル名", () => {
  assertEquals(UX_FIELD_TABLE_REF, "参照テーブル名");
  assertFalse(UX_FIELD_TABLE_REF.includes("physical table ref"), "must not use internal term");
  assertFalse(UX_FIELD_TABLE_REF.includes("table_ref"), "must not use internal snake_case key");
});

Deno.test("UX_FIELD_IMPORT_SCHEMA: uses user-friendly label 取り込みデータ定義名", () => {
  assertEquals(UX_FIELD_IMPORT_SCHEMA, "取り込みデータ定義名");
  assertFalse(UX_FIELD_IMPORT_SCHEMA.includes("import schema"), "must not use internal term");
  assertFalse(UX_FIELD_IMPORT_SCHEMA.includes("importSchema"), "must not use internal camelCase key");
});

Deno.test("UX_FIELD_NULLABLE: uses user-friendly label 空欄許可", () => {
  assertEquals(UX_FIELD_NULLABLE, "空欄許可");
  assertFalse(UX_FIELD_NULLABLE.includes("nullable"), "must not use internal term");
});
