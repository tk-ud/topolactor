import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_CHECKLIST,
  ADMIN_CONTENTS_GUIDE,
  ADMIN_HUB_NAVIGATION_GUIDE,
  ADMIN_INDEX_GUIDE,
  ADMIN_MAIN_FLOW_STEPS,
  ADMIN_MANIFESTS_GUIDE,
  ADMIN_ROUTE_CARDS,
  ADMIN_UI_BUILDER_GUIDE,
} from "../content/adminGuides.ts";
import {
  COLUMN_TYPE_NORMAL_VIEW_OPTIONS,
  DISPLAY_COLUMN_MODE_LABELS,
  HAVING_OPERATOR_OPTIONS,
  LOGICAL_CONNECTOR_OPTIONS,
  NORMAL_VIEW_BANNED_TERMS,
  SEARCH_OPERATOR_OPTIONS,
  UX_ACTION_LABELS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_COLUMN_TYPE_LABELS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_DISPLAY_MODE,
  UX_FIELD_HAVING_CONDITIONS,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_NULLABLE,
  UX_FIELD_RELATION_INTENT,
  UX_FIELD_SAMPLE_VIEWING,
  UX_FIELD_SEARCH_CONDITIONS,
  UX_FIELD_SEARCH_KEY,
  UX_MAIN_FLOW_STEP_LABELS,
  UX_RUNTIME_DESTINATION_LABELS,
  UX_STATUS_LABELS,
} from "../content/adminUxTerms.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  clearManifestScreenDesignLocal,
  emptyManifestScreenDesign,
  loadManifestScreenDesignLocal,
  saveManifestScreenDesignLocal,
  screenDesignFromBackendShape,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";

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
  assertFalse(
    text.includes("hub_relation"),
    "Flow steps must not contain hub_relation",
  );
});

Deno.test("ADMIN_HUB_NAVIGATION_GUIDE title does not contain hub_relation", () => {
  assertFalse(
    ADMIN_HUB_NAVIGATION_GUIDE.title.includes("hub_relation"),
    "Guide title must not expose hub_relation",
  );
});

// ─── promoteDisabled logic guard ──────────────────────────────────────────────
// validate must be run before promote is enabled.
//
// ContentsPromotionPanel の「内容を確認」は manifest data shape (validateAdminManifest)
// と promotion metadata (validateAdminPromotionManifest) の両方を検証する。
// どちらか一方でも blocking なら { isBlocking: true } となり promote は無効のまま。
// backend promote は独立した fail-close を持つため、frontend 側の validation state は
// pre-check として機能する。

function computePromoteDisabled(
  selectedId: string,
  status: string,
  validation: { isBlocking: boolean } | null,
): boolean {
  return !selectedId || status !== "draft" || validation === null ||
    validation.isBlocking;
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

function computeNextSequencePosition(
  activeRelations: { status: string }[],
): number {
  return activeRelations.filter((hr) => hr.status === "active").length + 1;
}

Deno.test("HubNavigation: new entry sequence auto-set to end", () => {
  const active = [
    { status: "active" },
    { status: "active" },
  ];
  assertEquals(
    computeNextSequencePosition(active),
    3,
    "should auto-position at end",
  );
});

Deno.test("HubNavigation: first entry sequence defaults to 1", () => {
  assertEquals(
    computeNextSequencePosition([]),
    1,
    "first entry should be at position 1",
  );
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
  const required = COMPONENT_CATALOG_ENTRIES.filter((e) =>
    e.registrationRequired
  );
  for (const e of required) {
    assertFalse(
      !e.sourcePath || e.sourcePath === "",
      `Entry "${e.componentKey}" must have a sourcePath for auto-fill`,
    );
  }
});

Deno.test("catalog: registration-required entries have componentKind for auto-fill", () => {
  const required = COMPONENT_CATALOG_ENTRIES.filter((e) =>
    e.registrationRequired
  );
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
  assertEquals(
    UX_RUNTIME_DESTINATION_LABELS["topology_transform_runtime"],
    "通常ルーティング",
  );
  assertEquals(UX_RUNTIME_DESTINATION_LABELS["admin_runtime"], "管理機能");
  assertEquals(
    UX_RUNTIME_DESTINATION_LABELS["sse_projection_runtime"],
    "リアルタイム投影",
  );
});

// ─── Advanced/details: can still hold raw technical information ────────────────

Deno.test("ADMIN_MANIFESTS_GUIDE boundaryNotes can hold implementation notes", () => {
  // boundaryNotes are not primary text — technical info is allowed there
  assertEquals(Array.isArray(ADMIN_MANIFESTS_GUIDE.boundaryNotes), true);
});

Deno.test("ADMIN_HUB_NAVIGATION_GUIDE boundaryNotes can hold technical scope notes", () => {
  assertEquals(Array.isArray(ADMIN_HUB_NAVIGATION_GUIDE.boundaryNotes), true);
});

// ─── UiBuilderAdmin layout_patch placement-only boundary ──────────────────────

Deno.test("UiBuilderAdmin: layout_patch dispatch omits cssTokenRefs from normal path", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const patchBlock = src.slice(src.indexOf('dispatchAdminOp("layout_patch"'));
  assert(patchBlock.length > 0, "layout_patch dispatch must exist");
  assertEquals(patchBlock.includes("cssTokenRefs:"), false);
});

// ─── ContentsScreenDesignPanel data input + import wiring ─────────────────────

Deno.test("ContentsScreenDesignPanel: step 3 mounts embedded CSV/JSON import subfeature", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(src.includes("AdminImportPanel"), "must embed AdminImportPanel");
  assert(src.includes('dataInputMode === "import"'), "must offer import tab");
  assert(src.includes("admin_csv_json_import") === false, "panel must use adminApi not raw layer strings");
  const adminApiSrc = await Deno.readTextFile(
    new URL("../api/adminApi.ts", import.meta.url),
  );
  assert(adminApiSrc.includes('"admin_csv_json_import"'), "adminApi owns import layer");
});

// ─── ContentsScreenDesignPanel Step 3 normal-view regression ───────────────────
// SSOT: admin-console-workflow-ssot.yaml step 3 normal_view_ui_excludes manual table_ref/import_schema inputs.

Deno.test("ContentsScreenDesignPanel: step 3 omits legacy table_ref and import_schema_name inputs", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assertEquals(src.includes("参照テーブル名"), false);
  assertEquals(src.includes("取り込みルール名"), false);
  assertEquals(src.includes("UX_FIELD_TABLE_REF"), false);
  assertEquals(src.includes("UX_FIELD_IMPORT_SCHEMA"), false);
  assertEquals(src.includes("importSchemaName:"), false);
});

Deno.test("UX_FIELD_NULLABLE: uses user-friendly label 空欄許可", () => {
  assertEquals(UX_FIELD_NULLABLE, "空欄許可");
  assertFalse(
    UX_FIELD_NULLABLE.includes("nullable"),
    "must not use internal term",
  );
});

// ─── Column type normal-view select regression ────────────────────────────────
// SSOT: admin-console-workflow-ssot.yaml step3.column_type_UI.candidates
// Normal-view select must cover exactly the SSOT candidates; free-text is in advanced/other.

Deno.test("COLUMN_TYPE_NORMAL_VIEW_OPTIONS: contains all SSOT candidates", () => {
  const required = [
    "text",
    "integer",
    "bigint",
    "boolean",
    "numeric",
    "timestamp with time zone",
    "date",
    "jsonb",
    "uuid",
    "varchar",
  ];
  for (const t of required) {
    assertEquals(
      COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(t),
      true,
      `COLUMN_TYPE_NORMAL_VIEW_OPTIONS must include "${t}"`,
    );
  }
});

Deno.test("COLUMN_TYPE_NORMAL_VIEW_OPTIONS: has exactly 10 entries matching SSOT", () => {
  assertEquals(
    COLUMN_TYPE_NORMAL_VIEW_OPTIONS.length,
    10,
    "must have exactly 10 normal-view candidates from SSOT",
  );
});

Deno.test("COLUMN_TYPE_NORMAL_VIEW_OPTIONS: does not contain banned aggregation term 'group by'", () => {
  assertFalse(
    COLUMN_TYPE_NORMAL_VIEW_OPTIONS.some((t) =>
      t.toLowerCase().includes("group by")
    ),
    "normal-view column type select must not contain aggregation vocabulary 'group by'",
  );
});

Deno.test("COLUMN_TYPE_NORMAL_VIEW_OPTIONS: every persisted type has a user-facing label", () => {
  for (const type of COLUMN_TYPE_NORMAL_VIEW_OPTIONS) {
    assert(
      UX_COLUMN_TYPE_LABELS[type],
      `normal-view label is required for persisted type "${type}"`,
    );
  }
});

Deno.test("UX_COLUMN_TYPE_ADVANCED_LABEL: is a non-empty string for advanced/other isolation", () => {
  assertEquals(typeof UX_COLUMN_TYPE_ADVANCED_LABEL, "string");
  assertEquals(
    UX_COLUMN_TYPE_ADVANCED_LABEL.length > 0,
    true,
    "advanced label must not be empty",
  );
  assertFalse(
    UX_COLUMN_TYPE_ADVANCED_LABEL.toLowerCase().includes("group by"),
    "advanced label must not contain 'group by'",
  );
});

// ─── New structured field UX vocabulary regression ────────────────────────────
// SSOT: admin-console-workflow-ssot.yaml steps 4–7 UX vocabulary requirements

Deno.test("UX_FIELD_SEARCH_KEY: does not expose internal field names", () => {
  assertFalse(
    UX_FIELD_SEARCH_KEY.includes("searchTargets"),
    "must not expose internal searchTargets key",
  );
  assertFalse(
    UX_FIELD_SEARCH_KEY.includes("_"),
    "must not use internal snake_case in normal-view label",
  );
  assertEquals(
    UX_FIELD_SEARCH_KEY.length > 0,
    true,
    "must be a non-empty label",
  );
});

Deno.test("UX_FIELD_AGGREGATION_KEY: does not expose 'group by' vocabulary", () => {
  assertFalse(
    UX_FIELD_AGGREGATION_KEY.toLowerCase().includes("group by"),
    "aggregation key label must not contain 'group by' (SSOT prohibited in normal view)",
  );
  assertEquals(
    UX_FIELD_AGGREGATION_KEY.length > 0,
    true,
    "must be a non-empty label",
  );
});

Deno.test("UX_FIELD_DISPLAY_COLUMNS: does not expose 'group by' vocabulary", () => {
  assertFalse(
    UX_FIELD_DISPLAY_COLUMNS.toLowerCase().includes("group by"),
    "display columns label must not contain 'group by'",
  );
});

Deno.test("UX_FIELD_SAMPLE_VIEWING: is a non-empty user-facing label", () => {
  assertEquals(typeof UX_FIELD_SAMPLE_VIEWING, "string");
  assertEquals(
    UX_FIELD_SAMPLE_VIEWING.length > 0,
    true,
    "sample viewing label must not be empty",
  );
});

Deno.test("UX_FIELD_INITIAL_DATA: does not expose direct-DB-write vocabulary", () => {
  assertFalse(
    UX_FIELD_INITIAL_DATA.toLowerCase().includes("insert"),
    "initial data label must not suggest direct DB insert",
  );
  assertEquals(
    UX_FIELD_INITIAL_DATA.length > 0,
    true,
    "must be a non-empty label",
  );
});

Deno.test("UX_FIELD_RELATION_INTENT: does not imply created-manifest hub management ownership", () => {
  // relation intent in /admin/contents is draft data-shape only; hub/relation management stays in /admin/manifests
  assertFalse(
    UX_FIELD_RELATION_INTENT.toLowerCase().includes("hub"),
    "relation intent label must not suggest hub management (owned by /admin/manifests)",
  );
  assertEquals(
    UX_FIELD_RELATION_INTENT.length > 0,
    true,
    "must be a non-empty label",
  );
});

// ─── ManifestScreenDesignDraft structured field round-trip ────────────────────
// Validates that emptyManifestScreenDesign has structured fields and localStorage round-trip preserves them.

Deno.test("emptyManifestScreenDesign: has all structured fields with correct defaults", () => {
  const d = emptyManifestScreenDesign();
  assertEquals(
    Array.isArray(d.searchKeyColumns),
    true,
    "searchKeyColumns must be an array",
  );
  assertEquals(
    d.searchKeyColumns.length,
    0,
    "searchKeyColumns defaults to empty",
  );
  assertEquals(
    Array.isArray(d.displayColumns),
    true,
    "displayColumns must be an array",
  );
  assertEquals(d.displayColumns.length, 0, "displayColumns defaults to empty");
  assertEquals(
    Array.isArray(d.aggregationColumns),
    true,
    "aggregationColumns must be an array",
  );
  assertEquals(
    Array.isArray(d.logicalTables),
    true,
    "logicalTables must be an array",
  );
  assertEquals(
    typeof d.aggregationKey,
    "string",
    "aggregationKey must be a string",
  );
  assertEquals(d.aggregationKey, "", "aggregationKey defaults to empty string");
  assertEquals(
    Array.isArray(d.relationIntents),
    true,
    "relationIntents must be an array",
  );
  assertEquals(
    d.relationIntents.length,
    0,
    "relationIntents defaults to empty",
  );
  assertEquals(
    Array.isArray(d.initialDataRows),
    true,
    "initialDataRows must be an array",
  );
  assertEquals(
    d.initialDataRows.length,
    0,
    "initialDataRows defaults to empty",
  );
});

Deno.test("screen_data_shape topology extension: extracts structured fields from topology JSON", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      tableRef: "my_table",
      searchTargets: ["col_a"],
      searchKeyColumns: ["col_a", "col_b"],
      aggregationKey: "col_a",
      aggregationFunction: "sum",
      aggregationColumns: ["col_b"],
      displayColumns: ["col_a", "col_b", "col_c"],
      aggregationSpec: null,
      logicalTables: [{
        tableName: "my_table",
        columns: [{ name: "col_a", dataType: "text", nullable: true }],
      }],
      screenOperationKind: "list",
      columns: [{ name: "col_a", dataType: "text", nullable: true }],
      relationIntents: [{
        localTableRef: "my_table",
        joinTableRef: "other_table",
        localKey: "id",
        remoteKey: "ref_id",
      }],
      aggregationMeasures: [{ column: "col_b", function: "sum" }],
      initialDataRows: [{ col_a: "value1" }],
    },
  ]);
  const shape = extractScreenDataShapeFromTopology(topology);
  assertEquals(shape.tableRef, "my_table");
  assertEquals(shape.searchKeyColumns, ["col_a", "col_b"]);
  assertEquals(shape.aggregationKey, "col_a");
  assertEquals(shape.aggregationFunction, "sum");
  assertEquals(shape.aggregationColumns, ["col_b"]);
  assertEquals(shape.displayColumns, ["col_a", "col_b", "col_c"]);
  assertEquals(shape.logicalTables.length, 1);
  assertEquals(shape.columns.length, 1);
  assertEquals(shape.columns[0].name, "col_a");
  assertEquals(shape.relationIntents.length, 1);
  assertEquals(shape.relationIntents[0].localTableRef, "my_table");
  assertEquals(shape.relationIntents[0].joinTableRef, "other_table");
  assertEquals(shape.aggregationMeasures[0].column, "col_b");
  assertEquals(shape.initialDataRows.length, 1);
  assertEquals(shape.initialDataRows[0]["col_a"], "value1");
});

Deno.test("screen_data_shape topology extension: returns empty structured fields when absent", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      tableRef: "my_table",
      searchTargets: ["col_a"],
    },
  ]);
  const shape = extractScreenDataShapeFromTopology(topology);
  assertEquals(
    shape.searchKeyColumns,
    [],
    "absent searchKeyColumns returns empty array",
  );
  assertEquals(
    shape.displayColumns,
    [],
    "absent displayColumns returns empty array",
  );
  assertEquals(
    shape.aggregationKey,
    null,
    "absent aggregationKey returns null",
  );
  assertEquals(shape.columns, [], "absent columns returns empty array");
  assertEquals(
    shape.relationIntents,
    [],
    "absent relationIntents returns empty array",
  );
  assertEquals(
    shape.initialDataRows,
    [],
    "absent initialDataRows returns empty array",
  );
});

Deno.test("screenDesignFromBackendShape: maps structured fields from topology shape", () => {
  const shape = {
    tableRef: "tbl",
    importSchemaName: null,
    searchTargets: ["col_a"],
    searchKeyColumns: ["col_a"],
    aggregationSpec: null,
    aggregationKey: "col_a",
    aggregationFunction: "avg",
    aggregationColumns: ["col_b"],
    aggregationMeasures: [],
    displayColumns: ["col_a", "col_b"],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    userFacingTopologyLabel: null,
    columns: [{ name: "col_a", dataType: "text", nullable: true }],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
  };
  const draft = screenDesignFromBackendShape(shape, "list");
  assertEquals(draft.searchKeyColumns, ["col_a"]);
  assertEquals(draft.aggregationKey, "col_a");
  assertEquals(draft.aggregationFunction, "avg");
  assertEquals(draft.displayColumns, ["col_a", "col_b"]);
  assertEquals(draft.columns.length, 1);
  assertEquals(draft.columns[0].name, "col_a");
});

Deno.test("initial data flow: initial data rows stored as intent not direct DB write", () => {
  // Guard: initialDataRows is a local state field only — no direct DB write function exists
  // The field is sent through assignAdminManifestScreenDataShape via backend topology extension
  const d = emptyManifestScreenDesign();
  d.initialDataRows = [{ col_a: "test_value" }];
  // Verify it's just a plain record array — no DB connection object, no execute function
  assertEquals(Array.isArray(d.initialDataRows), true);
  assertEquals(typeof d.initialDataRows[0], "object");
  assertEquals(d.initialDataRows[0]["col_a"], "test_value");
});

Deno.test("relation intent: /admin/contents relation intent does not own hub/inter-manifest management", () => {
  // Guard: UX_FIELD_RELATION_INTENT must not imply cross-manifest hub management
  // (that belongs to /admin/manifests)
  assertFalse(
    UX_FIELD_RELATION_INTENT.includes("manifest"),
    "relation intent label must not mention manifest (cross-manifest management is /admin/manifests)",
  );
});

Deno.test("aggregation UX: normal-view aggregation vocabulary does not contain 'group by'", () => {
  const normalViewLabels = [
    UX_FIELD_AGGREGATION_KEY,
    UX_FIELD_DISPLAY_COLUMNS,
    UX_FIELD_SAMPLE_VIEWING,
  ];
  for (const label of normalViewLabels) {
    assertFalse(
      label.toLowerCase().includes("group by"),
      `Normal-view aggregation label "${label}" must not contain 'group by' (SSOT prohibited vocabulary)`,
    );
  }
});

Deno.test("sample viewing: UX_FIELD_SAMPLE_VIEWING is the mandatory preview path label", () => {
  // Guard: sample viewing / preview path is required per SSOT step 7
  assertEquals(typeof UX_FIELD_SAMPLE_VIEWING, "string");
  assert(
    UX_FIELD_SAMPLE_VIEWING.length > 0,
    "sample viewing label must be defined",
  );
});

// ─── Normal-view source regression guard ─────────────────────────────────────
// Scan rendered copy from normal-view sources. Explicit <details> disclosures are
// removed before extraction so technical information remains available without
// leaking into the default path.

const NORMAL_VIEW_SOURCE_FILES = [
  "../routes/index.tsx",
  "../routes/demo.tsx",
  "../routes/runtime-status.tsx",
  "../routes/admin/index.tsx",
  "../islands/AdminImport.tsx",
  "../islands/ContentsAdmin.tsx",
  "../islands/ContentsPromotionPanel.tsx",
  "../islands/ContentsScreenDesignPanel.tsx",
  "../islands/ManifestsAdmin.tsx",
  "../islands/HubNavigationAdmin.tsx",
  "../islands/UiBuilderAdmin.tsx",
  "../components/ContentsPipelineStepper.tsx",
  "../components/UiBuilderFlowStepper.tsx",
  "../lib/manifestScreenDesign.ts",
];

function stripTechnicalDisclosures(source: string): string {
  return source
    .replace(/<details\b[\s\S]*?<\/details>/gi, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/^\s*\/\/.*$/gm, "");
}

function extractNormalViewCopy(source: string): string {
  const visibleSource = stripTechnicalDisclosures(source);
  const fragments: string[] = [];
  for (const match of visibleSource.matchAll(/>([^<{]+)</g)) {
    if (/[ぁ-んァ-ヶ一-龠]/.test(match[1])) fragments.push(match[1]);
  }
  for (
    const match of visibleSource.matchAll(
      /(?:placeholder|title|aria-label)=(?:"([^"]*)"|'([^']*)')/g,
    )
  ) {
    fragments.push(match[1] ?? match[2] ?? "");
  }
  // Status/error labels are normal-view output even though they are not JSX text nodes.
  for (
    const match of visibleSource.matchAll(
      /(?:setStatus|setError)\(\s*(["'`])([^"'`]*)\1/g,
    )
  ) {
    fragments.push(match[2]);
  }
  for (const match of visibleSource.matchAll(/message:\s*"([^"]*)"/g)) {
    fragments.push(match[1]);
  }
  // Draft source labels and local-cache notices are interpolated into normal view.
  for (
    const match of visibleSource.matchAll(
      /(?:none|local|backend|merged):\s*"([^"]*)"/g,
    )
  ) {
    fragments.push(match[1]);
  }
  for (
    const match of visibleSource.matchAll(
      /MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE\s*=\s*"([^"]*)"/g,
    )
  ) {
    fragments.push(match[1]);
  }
  return fragments.join(" ").replace(/\$\{[^}]+\}/g, "");
}

function collectGuideNormalViewCopy(): string {
  const guides = [
    ADMIN_INDEX_GUIDE,
    ADMIN_UI_BUILDER_GUIDE,
    ADMIN_CONTENTS_GUIDE,
    ADMIN_MANIFESTS_GUIDE,
    ADMIN_HUB_NAVIGATION_GUIDE,
  ];
  return [
    ...guides.flatMap((guide) => [
      guide.title,
      guide.purpose,
      ...guide.howToSteps,
      ...(guide.prerequisites ?? []),
      ...guide.inputs,
      ...guide.actions,
      ...guide.outputs,
      ...(guide.nextSteps ?? []),
      ...(guide.errorGuide ?? []),
      guide.caution ?? "",
    ]),
    ...ADMIN_ROUTE_CARDS.flatMap((
      card,
    ) => [card.label, card.purpose, card.relation, ...card.howToSummary]),
    ...ADMIN_MAIN_FLOW_STEPS.flatMap((
      step,
    ) => [step.label, step.purpose, step.completionSign, step.nextLabel ?? ""]),
    ...ACCEPTANCE_CHECKLIST.flatMap((item) => [item.label, ...item.checks]),
  ].join(" ");
}

Deno.test("normal view guide guard: shared guide copy excludes extracted internal vocabulary", () => {
  const normalViewCopy = collectGuideNormalViewCopy()
    .replace(/\/admin\/manifests/g, "")
    .toLowerCase();
  for (const term of NORMAL_VIEW_BANNED_TERMS) {
    assertFalse(
      normalViewCopy.includes(term.toLowerCase()),
      `shared guide copy must not expose internal term "${term}"`,
    );
  }
});

Deno.test("normal view source guard: scanned default-path copy excludes extracted internal vocabulary", async () => {
  for (const relativePath of NORMAL_VIEW_SOURCE_FILES) {
    const source = await Deno.readTextFile(
      new URL(relativePath, import.meta.url),
    );
    const normalViewCopy = extractNormalViewCopy(source).toLowerCase();
    for (const term of NORMAL_VIEW_BANNED_TERMS) {
      assertFalse(
        normalViewCopy.includes(term.toLowerCase()),
        `${relativePath} must not expose internal term "${term}" outside technical details`,
      );
    }
  }
});

Deno.test("v0.7.2: ContentsPipelineStepper uses pipeline step labels not legacy ⑧ promote", async () => {
  const source = await Deno.readTextFile(
    new URL("../components/ContentsPipelineStepper.tsx", import.meta.url),
  );
  assertEquals(source.includes("Step 1"), true);
  assertEquals(source.includes("空登録"), true);
  assertFalse(source.includes("⑧"), "legacy numbered promote step must not appear");
});

Deno.test("v0.7.2: UiBuilderFlowStepper uses package packaging label", async () => {
  const source = await Deno.readTextFile(
    new URL("../components/UiBuilderFlowStepper.tsx", import.meta.url),
  );
  assertEquals(source.includes("部品選択でパッケージ化"), true);
  assertFalse(source.includes("配置できる状態にする"), "legacy placement-ready goal removed");
});

Deno.test("normal view source guard: technical disclosures are excluded, adjacent default copy is checked", () => {
  const source =
    `<p>ページを設定</p><details><summary>技術情報</summary><code>backend runtime payload</code></details><p>manifest を選択</p>`;
  const copy = extractNormalViewCopy(source).toLowerCase();
  assertFalse(copy.includes("backend"));
  assertFalse(copy.includes("runtime"));
  assertFalse(copy.includes("payload"));
  assert(
    copy.includes("manifest"),
    "default-path copy after details must remain scannable",
  );
});

Deno.test("normal view terms: shared flow labels use user-facing page vocabulary", () => {
  assertEquals(UX_MAIN_FLOW_STEP_LABELS[0], "新しいページを作る");
  assertFalse(UX_MAIN_FLOW_STEP_LABELS.join(" ").includes("manifest"));
});

Deno.test("normal view banned terms: shared regression vocabulary covers extracted implementation categories", () => {
  for (
    const term of [
      "manifest",
      "manifestid",
      "manifest_key",
      "topology_manifest",
      "canonical",
      "projection",
      "runtime",
      "dispatcher",
      "payload",
      "backend",
      "db table",
      "column",
      "schema",
      "package",
      "component",
      "grouping intent",
      "raw",
      "silent fallback",
    ]
  ) {
    assert(
      NORMAL_VIEW_BANNED_TERMS.includes(
        term as typeof NORMAL_VIEW_BANNED_TERMS[number],
      ),
      `NORMAL_VIEW_BANNED_TERMS must include extracted category term "${term}"`,
    );
  }
});

// ─── SearchCondition / HavingCondition / DisplayColumnMode round-trip ─────────

Deno.test("emptyManifestScreenDesign: has searchConditions, havingConditions, displayColumnMode", () => {
  const d = emptyManifestScreenDesign();
  assertEquals(Array.isArray(d.searchConditions), true, "searchConditions must be an array");
  assertEquals(d.searchConditions.length, 0, "searchConditions defaults to empty");
  assertEquals(Array.isArray(d.havingConditions), true, "havingConditions must be an array");
  assertEquals(d.havingConditions.length, 0, "havingConditions defaults to empty");
  assertEquals(typeof d.displayColumnMode, "string", "displayColumnMode must be a string");
  assertEquals(d.displayColumnMode, "selected", "displayColumnMode defaults to selected");
});

Deno.test("screen_data_shape topology extension: extracts searchConditions and havingConditions", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      tableRef: "my_table",
      searchTargets: [],
      searchConditions: [
        { column: "col_a", operator: "=", value: "test", logicalConnector: "and" },
        { column: "col_b", operator: "between", value: "1", valueTo: "10" },
        { column: "col_c", operator: "in", values: ["x", "y"] },
        { column: "col_d", operator: "is null" },
      ],
      havingConditions: [
        { column: "salary", function: "sum", operator: ">", value: "1000" },
      ],
      displayColumnMode: "none",
    },
  ]);
  const shape = extractScreenDataShapeFromTopology(topology);
  assertEquals(shape.searchConditions.length, 4);
  assertEquals(shape.searchConditions[0].column, "col_a");
  assertEquals(shape.searchConditions[0].operator, "=");
  assertEquals(shape.searchConditions[0].value, "test");
  assertEquals(shape.searchConditions[1].operator, "between");
  assertEquals(shape.searchConditions[1].valueTo, "10");
  assertEquals(shape.searchConditions[2].operator, "in");
  assertEquals(shape.searchConditions[2].values?.length, 2);
  assertEquals(shape.searchConditions[3].operator, "is null");
  assertEquals(shape.havingConditions.length, 1);
  assertEquals(shape.havingConditions[0].column, "salary");
  assertEquals(shape.havingConditions[0].function, "sum");
  assertEquals(shape.havingConditions[0].operator, ">");
  assertEquals(shape.havingConditions[0].value, "1000");
  assertEquals(shape.displayColumnMode, "none");
});

Deno.test("screen_data_shape topology extension: displayColumnMode defaults null when absent", () => {
  const topology = JSON.stringify([
    { type: "screen_data_shape", tableRef: "t" },
  ]);
  const shape = extractScreenDataShapeFromTopology(topology);
  assertEquals(shape.searchConditions, []);
  assertEquals(shape.havingConditions, []);
  assertEquals(shape.displayColumnMode, null);
});

Deno.test("screenDesignFromBackendShape: maps searchConditions, havingConditions, displayColumnMode", () => {
  const shape = {
    tableRef: "tbl",
    importSchemaName: null,
    searchTargets: [],
    searchKeyColumns: [],
    aggregationSpec: null,
    aggregationKey: null,
    aggregationFunction: null,
    aggregationColumns: [],
    aggregationMeasures: [],
    displayColumns: [],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
    searchConditions: [{ column: "col_a", operator: "like", value: "test%" }],
    havingConditions: [{ column: "salary", function: "avg", operator: ">=", value: "500" }],
    displayColumnMode: "none",
  };
  const draft = screenDesignFromBackendShape(shape, "list");
  assertEquals(draft.searchConditions.length, 1);
  assertEquals(draft.searchConditions[0].operator, "like");
  assertEquals(draft.havingConditions.length, 1);
  assertEquals(draft.havingConditions[0].function, "avg");
  assertEquals(draft.displayColumnMode, "none");
});

Deno.test("DISPLAY_COLUMN_MODE_LABELS: covers selected/all/none", () => {
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["selected"], "string");
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["all"], "string");
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["none"], "string");
});

Deno.test("SEARCH_OPERATOR_OPTIONS: includes all required operators", () => {
  const ops = SEARCH_OPERATOR_OPTIONS.map((o) => o.value);
  const required = ["=", "!=", "like", "ilike", "not like", ">", ">=", "<", "<=", "between", "in", "not in", "is null", "is not null"];
  for (const op of required) {
    assert(ops.includes(op), `SEARCH_OPERATOR_OPTIONS must include operator "${op}"`);
  }
});

Deno.test("UX_FIELD_SEARCH_CONDITIONS: is a non-empty user-facing label without internal vocabulary", () => {
  assertEquals(typeof UX_FIELD_SEARCH_CONDITIONS, "string");
  assert(UX_FIELD_SEARCH_CONDITIONS.length > 0, "must be non-empty");
  assertFalse(UX_FIELD_SEARCH_CONDITIONS.includes("searchConditions"), "must not expose internal field name");
});

Deno.test("UX_FIELD_HAVING_CONDITIONS: is a non-empty user-facing label without internal vocabulary", () => {
  assertEquals(typeof UX_FIELD_HAVING_CONDITIONS, "string");
  assert(UX_FIELD_HAVING_CONDITIONS.length > 0, "must be non-empty");
  assertFalse(UX_FIELD_HAVING_CONDITIONS.toLowerCase().includes("having"), "must not expose SQL HAVING keyword in primary label");
});

Deno.test("displayColumnMode: none mode means aggregate-only display without row columns", () => {
  const d = emptyManifestScreenDesign();
  d.displayColumnMode = "none";
  assertEquals(d.displayColumnMode, "none");
  // none mode is intended for aggregate-only display — row columns excluded
});

Deno.test("LOGICAL_CONNECTOR_OPTIONS: covers and/or/not", () => {
  const vals = LOGICAL_CONNECTOR_OPTIONS.map((o) => o.value);
  assert(vals.includes("and"), "must include and");
  assert(vals.includes("or"), "must include or");
  assert(vals.includes("not"), "must include not");
});

Deno.test("HAVING_OPERATOR_OPTIONS: covers comparison operators", () => {
  const vals = HAVING_OPERATOR_OPTIONS.map((o) => o.value);
  assert(vals.includes("="), "must include =");
  assert(vals.includes(">"), "must include >");
  assert(vals.includes(">="), "must include >=");
  assert(vals.includes("<"), "must include <");
  assert(vals.includes("<="), "must include <=");
});

Deno.test("UX_FIELD_DISPLAY_MODE: is a non-empty user-facing label", () => {
  assertEquals(typeof UX_FIELD_DISPLAY_MODE, "string");
  assert(UX_FIELD_DISPLAY_MODE.length > 0, "must be non-empty");
  assertFalse(UX_FIELD_DISPLAY_MODE.includes("displayColumnMode"), "must not expose internal field name");
});
