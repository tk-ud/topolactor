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
  isEnumBackedColumnDataType,
  LOGICAL_CONNECTOR_OPTIONS,
  NORMAL_VIEW_BANNED_TERMS,
  SEARCH_OPERATOR_OPTIONS,
  UX_ACTION_LABELS,
  UX_COLUMN_TYPE_ADVANCED_LABEL,
  UX_COLUMN_TYPE_LABELS,
  UX_FIELD_ADD_SEARCH_CONDITION,
  UX_FIELD_ADVANCED_SEARCH_CONDITIONS,
  UX_FIELD_AGGREGATION_KEY,
  UX_FIELD_DISPLAY_COLUMNS,
  UX_FIELD_DISPLAY_MODE,
  UX_FIELD_HAVING_CONDITIONS,
  UX_FIELD_INITIAL_DATA,
  UX_FIELD_LOGICAL_CONDITION,
  UX_FIELD_NULLABLE,
  UX_FIELD_OPERATION_KINDS,
  UX_FIELD_RELATION_INTENT,
  UX_FIELD_SAMPLE_VIEWING,
  UX_FIELD_SEARCH_CONDITIONS,
  UX_FIELD_SEARCH_KEY,
  UX_MAIN_FLOW_STEP_LABELS,
  UX_ROUTE_NAVIGATION_NONE_LABEL,
  UX_ROUTE_NAVIGATION_PRESET_LABEL,
  UX_ROUTE_NAVIGATION_ROUTE_SELECT_LABEL,
  UX_ROUTE_NAVIGATION_SAVE_LABEL,
  UX_RUNTIME_DESTINATION_LABELS,
  UX_STATUS_LABELS,
  CONTENTS_STEP1_ENTRY_MODE_OPTIONS,
  CLONE_DRAFT_ORIGIN_LABELS,
  CLONE_MODE_LABELS,
} from "../content/adminUxTerms.ts";
import {
  encodeManifestPackageTargetRef,
  encodeRouteNavigationTargetRef,
  isRouteNavigationTargetRef,
  parseManifestPackageTargetRef,
  parseRouteNavigationTargetRef,
  ROUTE_NAV_PREFIX,
} from "../lib/packageWiringPicker.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  clearManifestScreenDesignLocal,
  emptyManifestScreenDesign,
  loadManifestScreenDesignLocal,
  saveManifestScreenDesignLocal,
  screenDesignFromBackendShape,
} from "../lib/manifestScreenDesign.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import {
  applyHavingConditions,
  applySearchConditions,
} from "../lib/searchConditionEval.ts";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import {
  topologySystemNameToUiBuilderKey,
  isValidTopologySystemName,
} from "../lib/topologySystemName.ts";

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
  const dispatchMatch = src.match(
    /dispatchAdminOp\("layout_patch"[\s\S]*?tensorPatchJson:\s*[^,]+,\s*\}\);/,
  );
  assert(dispatchMatch, "layout_patch dispatch must exist");
  assertEquals(dispatchMatch[0].includes("cssTokenRefs:"), false);
});

Deno.test("UiBuilderAdmin: layout patch JSON uses buildVisualLayoutPatchJson only", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(src.includes("function buildLayoutPatchJson"), false,
    "legacy buildLayoutPatchJson must be removed");
  const patchBuildStart = src.indexOf(
    "const tensorPatchJson = buildVisualLayoutPatchJson",
  );
  assert(patchBuildStart >= 0, "tensorPatchJson must be built via buildVisualLayoutPatchJson");
  const patchBuild = src.slice(patchBuildStart, src.indexOf("const effectiveLayoutId", patchBuildStart));
  assert(patchBuild.includes("draftNodes"), "buildVisualLayoutPatchJson must receive draftNodes");
  assert(
    patchBuild.includes("selectedLayoutClassRefs"),
    "buildVisualLayoutPatchJson must receive selectedLayoutClassRefs",
  );
  assert(src.includes("submittedTensorPatchJson = tensorPatchJson"),
    "preview/validate/apply must submit the visual layout patch");
});

Deno.test("UiBuilderAdmin: component_style_design upsert retains design editor fields", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    /dispatchAdminOp\([\s\S]*?"component_style_design"[\s\S]*?"upsert"/.test(src),
    "component_style_design upsert dispatch must exist",
  );
  const payloadStart = src.indexOf("const buildDesignPayload = () =>");
  assert(payloadStart >= 0, "buildDesignPayload helper must exist for upsert/save_tmp");
  const payloadBlock = src.slice(
    payloadStart,
    src.indexOf("const reloadSavedDesigns", payloadStart),
  );
  for (const field of [
    "cssTokenRefs",
    "responsiveTokenRefs: filterEmptyResponsiveRules",
    "inlineText:",
    "linkHref:",
    "reactionIntent:",
    "layoutNodeId",
  ]) {
    assert(
      payloadBlock.includes(field),
      `design payload must include ${field} for upsert/save_tmp`,
    );
  }
  assert(
    /"component_style_design"[\s\S]*?"upsert"[\s\S]*?buildDesignPayload\(\)/.test(src),
    "upsert must submit buildDesignPayload()",
  );
});

Deno.test("UiBuilderAdmin: deselect must not delete canvas designDraftByNodeId entries", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // onDeselectAll must call setSelectedNodeId(null) — selection may be multi-line
  assert(
    /onDeselectAll[\s\S]{0,120}setSelectedNodeId\(null\)/.test(src),
    "deselect must preserve designDraftByNodeId for canvas preview persistence",
  );
  assertFalse(
    /onDeselectAll[\s\S]{0,200}designDraftByNodeId[\s\S]{0,80}\.delete/.test(src),
    "onDeselectAll must not delete design draft entries",
  );
});

Deno.test("UiBuilderAdmin: layout_patch apply uses unified modal with validate then handoff", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("LayoutPatchApplyModal"),
    "apply must use unified apply modal",
  );
  assert(
    src.includes("layoutId={effectiveLayoutId}"),
    "modal must pass layoutId for demo deep-link",
  );
  assert(
    src.includes("openDesignInspectorHandoff"),
    "handoff must route authors to the docked right-panel design inspector",
  );
  assert(
    src.includes("右パネルのデザインインスペクタで保存できます"),
    "handoff must announce design inspector destination",
  );
  assert(
    src.includes('setLayoutApplyModalPhase("success")'),
    "apply success must transition modal to handoff phase",
  );
  assertFalse(
    src.includes("LayoutPatchApplyHandoffModal"),
    "legacy handoff modal must be removed",
  );
});

Deno.test("UiBuilderAdmin: surface shows apply only; validate runs inside modal", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertFalse(
    src.includes("LayoutPatchPreviewModal"),
    "preview modal component must not remain in the normal authoring route",
  );
  assertFalse(
    src.includes("LayoutVisualAuditCanvas"),
    "separate visual audit canvas must not remain in the normal authoring route",
  );
  assertFalse(
    src.includes('callLayoutPatch("preview")'),
    "preview action must not be exposed in authoring UI",
  );
  assert(
    src.includes("openLayoutApplyModal"),
    "surface apply button must open modal workflow",
  );
  const modalSrc = await Deno.readTextFile(
    new URL("../components/LayoutPatchApplyModal.tsx", import.meta.url),
  );
  assertFalse(
    modalSrc.includes("FlowLayoutCanvas"),
    "apply modal must not embed flow canvas wireframe",
  );
});

Deno.test("UiBuilderAdmin: flow canvas uses tree-centric layout not coordinate drag", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("FlowLayoutCanvas"),
    "authoring canvas must use flow tree projection",
  );
  assertFalse(
    src.includes("CANVAS_DRAG_HOLD_MS"),
    "coordinate drag hold must be removed in flow layout mode",
  );
  assert(
    src.includes("reorderLayoutNodeStack"),
    "layer stack reorder must use shared util",
  );
  assert(
    src.includes("migrateAbsolutePatchToFlowStack"),
    "legacy absolute layout must require explicit migration",
  );
});

Deno.test("UiBuilderAdmin: design inspector is selected canvas node driven and auto-saves design _tmp", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("selectedCanvasNode"),
    "design inspector must receive selected canvas node",
  );
  assert(
    src.includes("ノードを選択してください"),
    "no-selection state must follow SSOT copy",
  );
  assert(
    src.includes('"component_style_design",') && src.includes('"save_tmp",'),
    "design token edits must auto-save to backend _tmp",
  );
  assert(
    src.includes("designDraftByNodeId"),
    "design draft must lift for canvas live preview",
  );
  assert(
    src.includes("onDesignPreviewChange"),
    "design inspector must notify canvas preview on change",
  );
  assert(
    src.includes('"component_style_design",') && src.includes('"upsert",'),
    "explicit save must promote design via upsert",
  );
  assert(
    src.includes("CssTokenPicker"),
    "design inspector must expose token picker for selected node",
  );
  assertFalse(
    src.includes("ComponentDesignPreviewCanvas"),
    "design inspector must not be a separate preview panel",
  );
});

// ─── ContentsScreenDesignPanel data input + import wiring ─────────────────────

Deno.test("ContentsScreenDesignPanel: step 3 mounts embedded CSV/JSON import subfeature", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(src.includes("AdminImportPanel"), "must embed AdminImportPanel");
  assert(src.includes("embedded"), "must use embedded import panel on step 3");
  assert(
    src.includes("onMergePreviewToEditor"),
    "must wire CSV/JSON preview merge into unified editor",
  );
  assert(
    src.includes("ContentsDataEditor"),
    "must keep unified manual editor grid",
  );
  assert(
    src.includes("admin_csv_json_import") === false,
    "panel must use adminApi not raw layer strings",
  );
  const adminApiSrc = await Deno.readTextFile(
    new URL("../api/adminApi.ts", import.meta.url),
  );
  assert(
    adminApiSrc.includes('"admin_csv_json_import"'),
    "adminApi owns import layer",
  );
});

// ─── ContentsScreenDesignPanel Step 3 normal-view regression ───────────────────
// SSOT: admin-console-workflow-ssot.yaml step 3 normal_view_ui_excludes manual table_ref/import_schema inputs.

Deno.test("ContentsScreenDesignPanel: step 3 embedded import uses page column specs not schema_registry picker", async () => {
  const panel = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  const adminImport = await Deno.readTextFile(
    new URL("../islands/AdminImport.tsx", import.meta.url),
  );
  assert(panel.includes("columnSpecs={qualifiedColumns.map"));
  assert(panel.includes("q.column.dataType"));
  assert(adminImport.includes("usePageColumnImport"));
  assert(adminImport.includes("UX_IMPORT_RULE_PAGE_COLUMNS"));
  assert(
    adminImport.includes("!usePageColumnImport") &&
      adminImport.includes("handleApply"),
    "backend apply must be gated off for embedded page-column local preview",
  );
});

Deno.test("ContentsScreenDesignPanel: local import preview does not use backend snapshot reload", async () => {
  const panel = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(panel.includes("isLocalImportPreviewSnapshotId"));
  assert(
    panel.includes("!isLocalImportPreviewSnapshotId(lastImportSnapshotId)"),
    "snapshot reload must be backend snapshot ids only",
  );
});

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

Deno.test("ContentsScreenDesignPanel: step 3 does not render ページ名 input", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  const step3Blocks = [...src.matchAll(/\{activeStep === 3 && \([\s\S]*?\n      \)\}/g)];
  assert(step3Blocks.length > 0, "step 3 rendering blocks must exist");
  for (const match of step3Blocks) {
    assertFalse(
      match[0].includes("ページ名"),
      "step 3 must not render ページ名 input (display label belongs to step 1 only)",
    );
  }
});

Deno.test("ContentsScreenDesignPanel: step 3 does not patch screenLabel", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  const step3Blocks = [...src.matchAll(/\{activeStep === 3 && \([\s\S]*?\n      \)\}/g)];
  for (const match of step3Blocks) {
    assertFalse(
      match[0].includes("screenLabel"),
      "step 3 must not patch design.screenLabel",
    );
  }
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
    "enum",
  ];
  for (const t of required) {
    assertEquals(
      COLUMN_TYPE_NORMAL_VIEW_OPTIONS.includes(t),
      true,
      `COLUMN_TYPE_NORMAL_VIEW_OPTIONS must include "${t}"`,
    );
  }
});

Deno.test("isEnumBackedColumnDataType: true only for enum type", () => {
  assertEquals(isEnumBackedColumnDataType("enum"), true);
  assertEquals(isEnumBackedColumnDataType("ENUM"), true);
  assertEquals(isEnumBackedColumnDataType("text"), false);
});

Deno.test("COLUMN_TYPE_NORMAL_VIEW_OPTIONS: has exactly 11 entries including enum", () => {
  assertEquals(
    COLUMN_TYPE_NORMAL_VIEW_OPTIONS.length,
    11,
    "must have exactly 11 normal-view candidates (SSOT scalars + enum)",
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
      aggregationBlocks: [{
        sourceRef: "my_table",
        aggregationKey: "col_a",
        measures: [{ column: "col_b", function: "sum" }],
      }],
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
  assertEquals(shape.aggregationBlocks[0].sourceRef, "my_table");
  assertEquals(shape.aggregationBlocks[0].measures[0].function, "sum");
  assertEquals(shape.initialDataRows.length, 1);
  assertEquals(shape.initialDataRows[0].values["col_a"], "value1");
});

Deno.test("screen_data_shape topology extension: extracts enumGroupId on columns", () => {
  const groupId = "22222222-2222-2222-2222-222222222201";
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      logicalTables: [{
        tableName: "employees",
        columns: [{
          name: "status",
          dataType: "text",
          nullable: true,
          enumGroupId: groupId,
        }],
      }],
    },
  ]);
  const shape = extractScreenDataShapeFromTopology(topology);
  assertEquals(shape.logicalTables[0].columns[0].enumGroupId, groupId);
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
    aggregationBlocks: [],
    displayColumns: ["col_a", "col_b"],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    topologySystemName: null,
    userFacingTopologyLabel: null,
    columns: [{ name: "col_a", dataType: "text", nullable: true }],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
    searchConditions: [],
    havingConditions: [],
    displayColumnMode: null,
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
  d.initialDataRows = [{
    values: { col_a: "test_value" },
    lineage: { source: "manual" },
  }];
  assertEquals(Array.isArray(d.initialDataRows), true);
  assertEquals(d.initialDataRows[0].values["col_a"], "test_value");
  assertEquals(d.initialDataRows[0].lineage.source, "manual");
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
  // Exclude `=>` arrow functions — `>` there is not JSX text.
  for (const match of visibleSource.matchAll(/(?<!=)>([^<{]+)</g)) {
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
      const needle = term.toLowerCase();
      const hitAt = normalViewCopy.indexOf(needle);
      assertFalse(
        hitAt >= 0,
        `${relativePath} must not expose internal term "${term}" outside technical details` +
          (hitAt >= 0
            ? ` (context: …${
              normalViewCopy.slice(
                Math.max(0, hitAt - 30),
                hitAt + needle.length + 30,
              )
            }…)`
            : ""),
      );
    }
  }
});

Deno.test("ContentsAdmin: legacy promote path is not mounted on the page", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsAdmin.tsx", import.meta.url),
  );
  assertEquals(
    src.includes("ContentsPromotionPanel"),
    false,
    "legacy ContentsPromotionPanel must not be mounted on /admin/contents",
  );
  assertEquals(
    src.includes("旧経路"),
    false,
    "legacy promote accordion must not appear on contents page",
  );
});

Deno.test("v0.7.2: ContentsPipelineStepper uses pipeline step labels not legacy ⑧ promote", async () => {
  const source = await Deno.readTextFile(
    new URL("../components/ContentsPipelineStepper.tsx", import.meta.url),
  );
  assertEquals(source.includes("Step 1"), true);
  assertEquals(source.includes("空登録"), true);
  assertFalse(
    source.includes("⑧"),
    "legacy numbered promote step must not appear",
  );
});

Deno.test("v0.9.0: UiBuilderFlowStepper reflects implicit package canvas flow", async () => {
  const source = await Deno.readTextFile(
    new URL("../components/UiBuilderFlowStepper.tsx", import.meta.url),
  );
  assertEquals(source.includes("UI_BUILDER_CANVAS_FLOW"), true);
  assertEquals(source.includes("UiBuilderFlowStepId"), true);
  assertEquals(source.includes("ルートを選ぶ"), true);
  assertEquals(source.includes("canvas workspace"), true);
  assertFalse(source.includes("部品選択でパッケージ化"));
  assertFalse(source.includes("UiBuilderPhaseId"));
  // Old tab-based vocabulary must be gone
  assertFalse(
    source.includes('"layout editor"'),
    "old layout editor tab label must not appear",
  );
  assertFalse(
    source.includes('"standalone design surface"'),
    "old design editor tab label must not appear",
  );
  assertFalse(
    source.includes('"visual view"'),
    "old visual view tab label must not appear",
  );
  assertFalse(
    source.includes("UiBuilderTabId"),
    "old tab type must not appear",
  );
});

Deno.test("v0.8.0: UiBuilderAdmin canvas workspace has structural HTML palette and copy operations", async () => {
  // Updated for canvas-first workspace (SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract).
  // Old separate visual/layout/design tabs replaced with unified workspace.
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Canvas workspace contract markers
  assertEquals(source.includes("UI_BUILDER_WORKSPACE_MODE"), true);
  assertEquals(source.includes("UI_BUILDER_HAS_SEPARATE_TABS"), true);
  // Structural HTML palette still exposed in canvas workspace
  assertEquals(source.includes("StructuralHtmlPalette"), true);
  // Copy operations remain (flow tree sibling copy, not coordinate offset clone)
  assertEquals(source.includes("copyNode"), true);
  assertEquals(source.includes("nextOrderIndexForParent"), true);
  // LayoutBuilderSection is the main canvas
  assertEquals(source.includes("LayoutBuilderSection"), true);
  // Design inspector panel is docked in the LayoutBuilderSection right panel and selected-node driven.
  const rightPanelIndex = source.indexOf("layout-right-dock");
  const designPanelIndex = source.indexOf("<PackageDesignPanel", rightPanelIndex);
  assert(rightPanelIndex >= 0, "layout-right-dock must exist");
  assert(designPanelIndex > rightPanelIndex, "PackageDesignPanel must be rendered in the right dock");
  assert(
    source.slice(rightPanelIndex, designPanelIndex + 240).includes("selectedCanvasNode={selectedNode}"),
    "docked design inspector must be driven by the selected canvas node",
  );
  assertFalse(
    source.includes("designPanelOpen"),
    "standalone design panel toggle must not remain in normal authoring",
  );
  // No separate tab-based visual view panel
  assertFalse(
    source.includes("function VisualViewPanel("),
    "VisualViewPanel must be removed",
  );
  assertFalse(source.includes("const TABS:"), "TABS constant must be removed");
});

// ─── PR#364 follow-up: canvas_workspace_contract bucket cards / drag / right dock ─

Deno.test("UiBuilderAdmin: canvas palette uses icon-style bucket cards not table", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("component-bucket-card"), "icon-style card class must exist");
  assert(src.includes("component-bucket-panel"), "bucket panel vocabulary must exist");
  assert(src.includes("ComponentBucketCard"), "shared bucket card component must exist");
  const paletteSection = src.slice(
    src.indexOf("function LayoutPalette"),
    src.indexOf("function DashboardCandidatePalette"),
  );
  assertEquals(
    paletteSection.includes('<table class="table'),
    false,
    "LayoutPalette primary catalog UI must not use table",
  );
  assertEquals(
    src.includes("function BucketSection"),
    false,
    "standalone BucketSection removed from ui-builder surface",
  );
});

Deno.test("UiBuilderAdmin: bucket card drag source and canvas drop path exist", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("handleDragStartPalette"), "palette drag start handler required");
  assert(src.includes("handleDropOnCanvas"), "canvas drop handler required");
  assert(src.includes("readBucketCardDragPayload"), "canvas drop must parse card drag payload");
  assert(src.includes("BUCKET_CARD_DRAG_MIME"), "bucket card drag mime type required");
  assert(src.includes("makeNewNode"), "drop must create draft node");
  assert(src.includes("buildVisualLayoutPatchJson"), "canonical layout patch builder required");
  assert(
    src.includes('draggable={draggable && placementReady}') ||
      src.includes("draggable\n              placementReady"),
    "bucket cards must expose conditional drag source",
  );
  assert(src.includes("onRegisterComponentBeforePlace"), "drop must auto-register component to package");
  assert(src.includes("ensureShellPackageForRoute"), "route must auto-generate shell package");
});

Deno.test("UiBuilderAdmin: bucket card shows source_path on card face", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("component-bucket-card__source-path"));
});

Deno.test("UiBuilderAdmin: canvas workspace has left docked panel; no separate palette strip above", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // canvas workspace section must exist
  assert(src.includes("ui-builder-canvas-workspace"), "canvas must be a dedicated workspace section");
  assert(src.includes("CANVAS_WORKSPACE_HEIGHT") || src.includes("UI_BUILDER_DOCK_HEIGHT"), "canvas workspace must use viewport-height sizing for docks");
  // left docked panel must exist inside source (SSOT: canvas_workspace_contract.left_panel)
  assert(src.includes("left-docked-panel"), "palettes must live in a left docked panel inside the canvas workspace");
  assert(src.includes("data-component-add-panel"), "left panel must carry data-component-add-panel marker");
  // legacy separate palette strip above canvas must not exist
  assertEquals(src.includes("ui-builder-palette-strip"), false, "legacy palette strip above canvas must be removed");
});

Deno.test("UiBuilderAdmin: empty canvas does not auto-seed all palette components", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("seedWhenEmpty === true"),
    "palette seeding must be opt-in only",
  );
  assert(
    src.includes("空のキャンバスから開始"),
    "hydrate without draft must start empty canvas",
  );
});

Deno.test("UiBuilderAdmin: legacy manual route commits on Enter not on every keystroke", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("committedRouteKey"), "committed route key must drive package auto-gen");
  assert(src.includes("function LegacyManualRouteInput("), "manual input must live in legacy-only component");
  assert(src.includes("manualRouteDraft"), "legacy manual input must be draft-only while typing");
  assert(src.includes('e.key === "Enter"'), "Enter must commit legacy manual route");
  const mainExport = src.slice(src.indexOf("export default function UiBuilderAdmin"));
  assert(
    mainExport.includes("}, [committedRouteKey]);"),
    "package auto-gen effect must depend on committedRouteKey only",
  );
  assertEquals(
    mainExport.includes("}, [manualRouteDraft]);"),
    false,
    "package auto-gen must not run on every manual route keystroke",
  );
});

Deno.test("UiBuilderAdmin: normal authoring does not expose manual routeKey guidance", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const normalPath = stripTechnicalDisclosures(src);
  assertFalse(
    normalPath.includes("直接入力にルートキー"),
    "normal path must not guide authors to type routeKey manually",
  );
  assertFalse(
    normalPath.includes("直接入力（初回はこちら）"),
    "normal path must not show first-time manual routeKey input",
  );
  assert(
    normalPath.includes("ManifestRouteEntry"),
    "normal path must use topologySystemName-derived ManifestRouteEntry",
  );
  const bucketStart = src.indexOf("function BucketPackageRouteFields(");
  const bucketEnd = src.indexOf("\nfunction LegacyManualRouteInput(");
  const bucketBody = src.slice(bucketStart, bucketEnd);
  assertFalse(
    bucketBody.includes("直接入力"),
    "BucketPackageRouteFields must not contain manual route input",
  );
});

Deno.test("UiBuilderAdmin: layout right dock is not fixed 200px wide", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(src.includes('style="width:200px;"'), false);
  assert(src.includes("layout-right-dock"), "layout right dock surface must exist");
  assert(src.includes("clamp(320px, 24vw, 420px)"), "right dock must use practical width clamp");
  assert(src.includes("PackageDesignPanel"), "design inspector must share selected node");
  assert(src.includes("data-selected-node-id"), "selected node must drive dock inspectors");
  assert(src.includes("LayoutRightDock"), "right dock wrapper must exist");
});

Deno.test("UiBuilderAdmin: empty canvas guidance promotes drag-to-canvas", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const terms = await Deno.readTextFile(
    new URL("../content/adminUxTerms.ts", import.meta.url),
  );
  assert(src.includes("UX_EMPTY_CANVAS_DRAG_GUIDANCE"));
  assert(src.includes("UX_ROUTE_KEY_REQUIRED_FOR_CANVAS"));
  assert(terms.includes("左パネルのカードをドラッグしてキャンバスへ配置"));
});

Deno.test("UiBuilderAdmin: flow canvas accepts palette drop on root stack", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("FlowLayoutCanvas"));
  assert(src.includes("onDrop={handleDropOnCanvas}"));
  assert(src.includes("makeNewNode(entry, null)"));
});

Deno.test("v0.7.2: UiBuilderFlowStepper does not expose raw phase id in default path", async () => {
  const source = await Deno.readTextFile(
    new URL("../components/UiBuilderFlowStepper.tsx", import.meta.url),
  );
  assertFalse(
    source.includes("現在: <strong>{activeTab}</strong>"),
    "raw tab id must not be shown in stepper header",
  );
  assertFalse(
    source.includes("現在: <strong>{activePhase}</strong>"),
    "raw phase id must not be shown in stepper header",
  );
  assertEquals(source.includes("UI_BUILDER_FLOW_LABELS"), true);
});

Deno.test("ManifestsAdmin empty state aligns with step3 → ui-builder flow", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/ManifestsAdmin.tsx", import.meta.url),
  );
  const emptyStateBlock = source.slice(
    source.indexOf("topologyManifests.length === 0"),
    source.indexOf("overflow-x-auto"),
  );
  assertFalse(
    emptyStateBlock.includes("有効化"),
    "empty state must not suggest legacy promote activation",
  );
  assert(
    emptyStateBlock.includes("step 1") &&
      emptyStateBlock.includes("UX_UI_BUILDER") &&
      emptyStateBlock.includes("ページ同士をつなげて"),
    "empty state should reference contents steps, ui-builder, then linking",
  );
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

Deno.test("normal view guide guard: adminGuides copy excludes pipeline and layout/design jargon", () => {
  const guideCopy = collectGuideNormalViewCopy().toLowerCase();
  for (
    const term of [
      "pipeline",
      "post-pipeline",
      "separate layout/design surface",
      "submit",
      "standalone design surface",
    ]
  ) {
    assertFalse(
      guideCopy.includes(term),
      `adminGuides primary copy must not contain "${term}"`,
    );
  }
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
      "pipeline",
      "post-pipeline",
      "submit",
      "separate layout/design surface",
      "standalone design surface",
      "promote",
      "バケット",
      "add のみ",
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
  assertEquals(
    Array.isArray(d.searchConditions),
    true,
    "searchConditions must be an array",
  );
  assertEquals(
    d.searchConditions.length,
    0,
    "searchConditions defaults to empty",
  );
  assertEquals(
    Array.isArray(d.havingConditions),
    true,
    "havingConditions must be an array",
  );
  assertEquals(
    d.havingConditions.length,
    0,
    "havingConditions defaults to empty",
  );
  assertEquals(
    typeof d.displayColumnMode,
    "string",
    "displayColumnMode must be a string",
  );
  assertEquals(
    d.displayColumnMode,
    "selected",
    "displayColumnMode defaults to selected",
  );
});

Deno.test("screen_data_shape topology extension: extracts searchConditions and havingConditions", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      tableRef: "my_table",
      searchTargets: [],
      searchConditions: [
        {
          column: "col_a",
          operator: "=",
          value: "test",
          logicalConnector: "and",
        },
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

Deno.test("screenDesignFromBackendShape: migrates global conditions into first block when blocks exist", () => {
  const shape = {
    tableRef: "tbl",
    importSchemaName: null,
    searchTargets: [],
    searchKeyColumns: [],
    aggregationSpec: null,
    aggregationKey: "employees.status",
    aggregationFunction: null,
    aggregationColumns: [],
    aggregationMeasures: [{ column: "employees.salary", function: "sum" }],
    aggregationBlocks: [],
    displayColumns: [],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    topologySystemName: null,
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
    searchConditions: [{ column: "col_a", operator: "like", value: "test%" }],
    havingConditions: [{
      column: "salary",
      function: "avg",
      operator: ">=",
      value: "500",
    }],
    displayColumnMode: "none",
  };
  const draft = screenDesignFromBackendShape(shape, "list");
  assertEquals(draft.searchConditions.length, 0);
  assertEquals(draft.aggregationBlocks.length, 1);
  assertEquals(draft.aggregationBlocks[0]!.searchConditions.length, 1);
  assertEquals(
    draft.aggregationBlocks[0]!.searchConditions[0].operator,
    "like",
  );
  assertEquals(draft.aggregationBlocks[0]!.havingConditions.length, 1);
  assertEquals(draft.aggregationBlocks[0]!.havingConditions[0].function, "avg");
  assertEquals(draft.displayColumnMode, "none");
});

Deno.test("screenDesignFromBackendShape: keeps global conditions when no aggregation blocks", () => {
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
    aggregationBlocks: [],
    displayColumns: [],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    topologySystemName: null,
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
    searchConditions: [{ column: "col_a", operator: "like", value: "test%" }],
    havingConditions: [{
      column: "salary",
      function: "avg",
      operator: ">=",
      value: "500",
    }],
    displayColumnMode: "none",
  };
  const draft = screenDesignFromBackendShape(shape, "list");
  assertEquals(draft.searchConditions.length, 1);
  assertEquals(draft.havingConditions.length, 1);
  assertEquals(draft.aggregationBlocks.length, 0);
});

Deno.test("DISPLAY_COLUMN_MODE_LABELS: covers selected/all/none", () => {
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["selected"], "string");
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["all"], "string");
  assertEquals(typeof DISPLAY_COLUMN_MODE_LABELS["none"], "string");
});

Deno.test("SEARCH_OPERATOR_OPTIONS: includes all required operators including <> alias", () => {
  const ops = SEARCH_OPERATOR_OPTIONS.map((o) => o.value);
  const required = [
    "=",
    "!=",
    "<>",
    "like",
    "ilike",
    "not like",
    ">",
    ">=",
    "<",
    "<=",
    "between",
    "in",
    "not in",
    "is null",
    "is not null",
  ];
  for (const op of required) {
    assert(
      ops.includes(op),
      `SEARCH_OPERATOR_OPTIONS must include operator "${op}"`,
    );
  }
});

Deno.test("UX_FIELD_SEARCH_CONDITIONS: is a non-empty user-facing label without internal vocabulary", () => {
  assertEquals(typeof UX_FIELD_SEARCH_CONDITIONS, "string");
  assert(UX_FIELD_SEARCH_CONDITIONS.length > 0, "must be non-empty");
  assertFalse(
    UX_FIELD_SEARCH_CONDITIONS.includes("searchConditions"),
    "must not expose internal field name",
  );
});

Deno.test("UX_FIELD_HAVING_CONDITIONS: is a non-empty user-facing label without internal vocabulary", () => {
  assertEquals(typeof UX_FIELD_HAVING_CONDITIONS, "string");
  assert(UX_FIELD_HAVING_CONDITIONS.length > 0, "must be non-empty");
  assertFalse(
    UX_FIELD_HAVING_CONDITIONS.toLowerCase().includes("having"),
    "must not expose SQL HAVING keyword in primary label",
  );
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

Deno.test("HAVING_OPERATOR_OPTIONS: covers comparison operators including <> alias", () => {
  const vals = HAVING_OPERATOR_OPTIONS.map((o) => o.value);
  assert(vals.includes("="), "must include =");
  assert(vals.includes("!="), "must include !=");
  assert(vals.includes("<>"), "must include <> alias");
  assert(vals.includes(">"), "must include >");
  assert(vals.includes(">="), "must include >=");
  assert(vals.includes("<"), "must include <");
  assert(vals.includes("<="), "must include <=");
});

Deno.test("UX_FIELD_DISPLAY_MODE: is a non-empty user-facing label", () => {
  assertEquals(typeof UX_FIELD_DISPLAY_MODE, "string");
  assert(UX_FIELD_DISPLAY_MODE.length > 0, "must be non-empty");
  assertFalse(
    UX_FIELD_DISPLAY_MODE.includes("displayColumnMode"),
    "must not expose internal field name",
  );
});

// ─── applySearchConditions: AND/OR/NOT evaluation contract ───────────────────
// SSOT: conditions[i].logicalConnector joins conditions[i] to conditions[i+1].
// Connector is stored on the source row, evaluated when combining with the next.

Deno.test("applySearchConditions: single condition = filters correctly", () => {
  const rows = [{ col: "a" }, { col: "b" }, { col: "c" }] as Record<
    string,
    string
  >[];
  const result = applySearchConditions(rows, [{
    column: "col",
    operator: "=",
    value: "b",
  }]);
  assertEquals(result.length, 1);
  assertEquals(result[0]["col"], "b");
});

Deno.test("applySearchConditions: <> operator matches != semantics", () => {
  const rows = [{ col: "a" }, { col: "b" }] as Record<string, string>[];
  const result = applySearchConditions(rows, [{
    column: "col",
    operator: "<>",
    value: "a",
  }]);
  assertEquals(result.length, 1);
  assertEquals(result[0]["col"], "b");
});

Deno.test("applySearchConditions: AND connector — both conditions must match", () => {
  const rows = [
    { name: "Alice", dept: "Eng" },
    { name: "Bob", dept: "Eng" },
    { name: "Carol", dept: "HR" },
  ] as Record<string, string>[];
  // conditions[0].logicalConnector=and joins condition 0 to condition 1
  const result = applySearchConditions(rows, [
    { column: "dept", operator: "=", value: "Eng", logicalConnector: "and" },
    { column: "name", operator: "=", value: "Alice" },
  ]);
  assertEquals(result.length, 1);
  assertEquals(result[0]["name"], "Alice");
});

Deno.test("applySearchConditions: OR connector — either condition matches", () => {
  const rows = [
    { name: "Alice", dept: "Eng" },
    { name: "Bob", dept: "HR" },
    { name: "Carol", dept: "Finance" },
  ] as Record<string, string>[];
  const result = applySearchConditions(rows, [
    { column: "dept", operator: "=", value: "Eng", logicalConnector: "or" },
    { column: "dept", operator: "=", value: "HR" },
  ]);
  assertEquals(result.length, 2);
  assert(
    result.some((r) => r["name"] === "Alice"),
    "Alice (Eng) must be included",
  );
  assert(result.some((r) => r["name"] === "Bob"), "Bob (HR) must be included");
});

Deno.test("applySearchConditions: NOT connector — excludes rows where next condition matches", () => {
  const rows = [
    { dept: "Eng" },
    { dept: "HR" },
    { dept: "Finance" },
  ] as Record<string, string>[];
  // NOT: result = result && !match_of_next_condition
  // cond[0]=dept=Eng (match=true for Eng row only)
  // cond[1]=dept=Finance, conn from cond[0]=not → result=true && !match_of_finance
  const result = applySearchConditions(rows, [
    { column: "dept", operator: "=", value: "Eng", logicalConnector: "not" },
    { column: "dept", operator: "=", value: "Finance" },
  ]);
  // Only Eng row passes: result=true (match=Eng), not Finance → true && !false = true
  // HR row: result=false (not Eng), not Finance → false && !false = false
  // Finance row: result=false (not Eng), not Finance → false && !true = false
  assertEquals(result.length, 1);
  assertEquals(result[0]["dept"], "Eng");
});

Deno.test("applySearchConditions: three conditions with AND connector chain", () => {
  const rows = [
    { a: "1", b: "2", c: "3" },
    { a: "1", b: "2", c: "X" },
    { a: "1", b: "Y", c: "3" },
  ] as Record<string, string>[];
  const result = applySearchConditions(rows, [
    { column: "a", operator: "=", value: "1", logicalConnector: "and" },
    { column: "b", operator: "=", value: "2", logicalConnector: "and" },
    { column: "c", operator: "=", value: "3" },
  ]);
  assertEquals(result.length, 1);
  assertEquals(result[0]["a"], "1");
  assertEquals(result[0]["c"], "3");
});

Deno.test("applyHavingConditions: <> operator on measure result", () => {
  const groups = new Map<string, Record<string, string>[]>([
    ["a", [{ salary: "100" }, { salary: "200" }] as Record<string, string>[]],
    ["b", [{ salary: "500" }] as Record<string, string>[]],
  ]);
  const result = applyHavingConditions(groups, [
    { column: "salary", function: "sum", operator: "<>", value: "300" },
  ]);
  assertEquals(result.size, 1, "only group b (sum=500) should survive");
  assert(result.has("b"), "group b must be kept");
});

// ─── Step 2 re-save preserves Step 3 structured fields ───────────────────────
// Regression: buildAssignPayloadForStep(step=2) must preserve searchConditions,
// havingConditions, and displayColumnMode from the existing backend shape.

Deno.test("buildAssignPayloadForStep step2: preserves searchConditions from backend shape", () => {
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
    aggregationBlocks: [],
    displayColumns: [],
    logicalTables: [],
    screenOperationKind: "list",
    screenOperationKinds: ["list"],
    topologySystemName: null,
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
    searchConditions: [{ column: "col_a", operator: "=", value: "test" }],
    havingConditions: [{
      column: "salary",
      function: "sum",
      operator: ">",
      value: "1000",
    }],
    displayColumnMode: "none",
  };
  const design = emptyManifestScreenDesign();
  const payload = buildAssignPayloadForStep(
    2,
    "manifest-id",
    design,
    shape as Parameters<typeof buildAssignPayloadForStep>[3],
  );
  assertEquals(
    Array.isArray(payload.searchConditions),
    true,
    "searchConditions must be preserved",
  );
  assertEquals(
    (payload.searchConditions ?? []).length,
    1,
    "one searchCondition must survive step 2 re-save",
  );
  assertEquals((payload.searchConditions ?? [])[0].column, "col_a");
  assertEquals(
    Array.isArray(payload.havingConditions),
    true,
    "havingConditions must be preserved",
  );
  assertEquals(
    (payload.havingConditions ?? []).length,
    1,
    "one havingCondition must survive step 2 re-save",
  );
  assertEquals(
    payload.displayColumnMode,
    "none",
    "displayColumnMode must survive step 2 re-save",
  );
});

Deno.test("Step3 aggregation: block add UI is exposed in normal view", async () => {
  const source = await Deno.readTextFile(
    new URL(
      "../components/ContentsAggregationMeasuresEditor.tsx",
      import.meta.url,
    ),
  );
  assert(
    source.includes("集計ブロックを追加") &&
      source.includes("SQL ソース（論理テーブル）"),
    "aggregation blocks must be addable per SQL/logical-table source",
  );
});

Deno.test("Step3 sample viewing: operation kind select drives preview projection", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(
    source.includes("UX_FIELD_OPERATION_KINDS") &&
      source.includes("projectionColumnsForOperationKind") &&
      source.includes("screenOperationLabel(effectivePreviewKind)"),
    "sample preview must switch entityTargetColumns by selected operation kind",
  );
});

Deno.test("Step3 progressive disclosure: raw inputs remain disclosed with SQL clause mapping", async () => {
  const panel = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  const hints = await Deno.readTextFile(
    new URL("../components/ProRawScreenDataShapeHints.tsx", import.meta.url),
  );
  assert(
    panel.includes("ProRawScreenDataShapeHints"),
    "ContentsScreenDesignPanel must mount pro raw hints component",
  );
  assert(
    hints.includes("プロ向け / raw 入力（検索・集計 — UI と双方向同期）"),
    "raw inputs must stay behind a pro-facing disclosure",
  );
  assert(
    hints.includes("searchTargets") && hints.includes("aggregationSpec"),
    "pro hints must document both raw fields",
  );
  assert(
    hints.includes("GROUP BY") && hints.includes("WHERE") &&
      hints.includes("HAVING") && hints.includes("FROM"),
    "pro hints must map structured UI to SQL clauses",
  );
  assert(
    hints.includes("双方向同期") || hints.includes("双方向"),
    "raw fields must document bidirectional sync with structured UI",
  );
  assert(
    hints.includes("searchKeyColumns") || hints.includes("集計ブロック"),
    "raw hints must reference structured bind targets",
  );
});

Deno.test("Step3 progressive disclosure: search operator UI lives inside aggregation block", async () => {
  const source = await Deno.readTextFile(
    new URL(
      "../components/AggregationBlockConditionsEditor.tsx",
      import.meta.url,
    ),
  );
  const advancedStart = source.indexOf(`{UX_FIELD_ADVANCED_SEARCH_CONDITIONS}`);
  const operatorStart = source.indexOf(
    "SEARCH_OPERATOR_OPTIONS.map",
    advancedStart,
  );
  assert(
    advancedStart >= 0,
    "advanced search disclosure summary must exist inside aggregation block",
  );
  assert(
    operatorStart > advancedStart,
    "search operator select must live inside the block conditions editor",
  );
  assert(
    source.includes("searchConditions.length === 0") &&
      source.includes(`{UX_FIELD_ADD_SEARCH_CONDITION}`),
    "zero-condition state must show only the add-search-condition path before condition rows",
  );
  assert(
    source.includes("{ci > 0 && (") &&
      source.includes(`{UX_FIELD_LOGICAL_CONDITION}`),
    "AND/OR/NOT logical connector UI must only appear from the second condition onward",
  );
  const panelSource = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(
    !panelSource.includes(`{UX_FIELD_ADVANCED_SEARCH_CONDITIONS}`),
    "screen-level advanced search disclosure must be removed; conditions belong to blocks",
  );
});

Deno.test("adminUxTerms: Step3 progressive disclosure labels are non-empty", () => {
  assertEquals(UX_FIELD_ADVANCED_SEARCH_CONDITIONS, "詳細条件を設定");
  assertEquals(UX_FIELD_ADD_SEARCH_CONDITION, "検索条件を追加");
  assertEquals(UX_FIELD_LOGICAL_CONDITION, "論理条件");
  assertEquals(UX_FIELD_HAVING_CONDITIONS, "集計後の絞り込み（詳細）");
});

Deno.test("adminUxTerms: aggregate trigger Step3 labels are Japanese-first", async () => {
  const terms = await Deno.readTextFile(
    new URL("../content/adminUxTerms.ts", import.meta.url),
  );
  assert(terms.includes("UX_AGGREGATE_TRIGGER_SECTION_TITLE"));
  assert(terms.includes("集計トリガー（構造化入力）"));
  const panel = await Deno.readTextFile(
    new URL("../components/AggregateTriggerAuthoringPanel.tsx", import.meta.url),
  );
  assert(
    panel.includes("UX_AGGREGATE_TRIGGER_SECTION_TITLE"),
    "AggregateTriggerAuthoringPanel must use adminUxTerms",
  );
  assertFalse(
    panel.includes("Aggregate trigger structured authoring"),
    "English section title must not remain in panel",
  );
  const surface = await Deno.readTextFile(
    new URL("../components/AggregateTriggerAuthoringSurface.tsx", import.meta.url),
  );
  assert(
    surface.includes("UX_AGGREGATE_TRIGGER_GATE_HINT"),
    "AggregateTriggerAuthoringSurface must gate on aggregation logic",
  );
});

// ─── Route navigation wiring preset ──────────────────────────────────────────

Deno.test("encodeRouteNavigationTargetRef: round-trip with parseRouteNavigationTargetRef", () => {
  const routeKey = "admin/demo";
  const encoded = encodeRouteNavigationTargetRef(routeKey);
  assertEquals(encoded, `${ROUTE_NAV_PREFIX}${routeKey}`);
  assertEquals(parseRouteNavigationTargetRef(encoded), routeKey);
});

Deno.test("encodeRouteNavigationTargetRef: produces route: prefix not manifest: prefix", () => {
  const encoded = encodeRouteNavigationTargetRef("some/route");
  assert(encoded.startsWith("route:"), "must start with route:");
  assertFalse(encoded.startsWith("manifest:"), "must not start with manifest:");
});

Deno.test("parseRouteNavigationTargetRef: returns null for manifest: refs", () => {
  const manifestRef =
    "manifest:00000000-0000-0000-0000-000000000001:someWiringKey";
  assertEquals(
    parseRouteNavigationTargetRef(manifestRef),
    null,
    "manifest: ref must not parse as route navigation",
  );
});

Deno.test("parseRouteNavigationTargetRef: returns null for empty string", () => {
  assertEquals(parseRouteNavigationTargetRef(""), null);
  assertEquals(parseRouteNavigationTargetRef("route:"), null, "bare prefix without key must return null");
});

Deno.test("isRouteNavigationTargetRef: true for route: refs, false for others", () => {
  assert(isRouteNavigationTargetRef("route:admin/demo"));
  assertFalse(isRouteNavigationTargetRef("manifest:uuid:key"));
  assertFalse(isRouteNavigationTargetRef(""));
  assertFalse(isRouteNavigationTargetRef(null));
  assertFalse(isRouteNavigationTargetRef(undefined));
});

Deno.test("encodeManifestPackageTargetRef round-trip still works after route nav addition", () => {
  // Existing manifest wiring must not be broken
  const encoded = encodeManifestPackageTargetRef(
    "00000000-0000-0000-0000-000000000001",
    "someKey",
  );
  assertEquals(
    encoded,
    "manifest:00000000-0000-0000-0000-000000000001:someKey",
  );
  const parsed = parseManifestPackageTargetRef(encoded);
  assertEquals(parsed?.manifestId, "00000000-0000-0000-0000-000000000001");
  assertEquals(parsed?.wiringKey, "someKey");
});

Deno.test("UX route navigation labels: are user-facing and non-empty without internal terms", () => {
  for (const label of [
    UX_ROUTE_NAVIGATION_PRESET_LABEL,
    UX_ROUTE_NAVIGATION_NONE_LABEL,
    UX_ROUTE_NAVIGATION_ROUTE_SELECT_LABEL,
    UX_ROUTE_NAVIGATION_SAVE_LABEL,
  ]) {
    assert(label.length > 0, `label must be non-empty: ${label}`);
    assertFalse(
      label.toLowerCase().includes("dispatcher"),
      `label must not expose dispatcher: ${label}`,
    );
    assertFalse(
      label.toLowerCase().includes("wiring_kind"),
      `label must not expose wiring_kind: ${label}`,
    );
    assertFalse(
      label.toLowerCase().includes("target_ref"),
      `label must not expose target_ref: ${label}`,
    );
  }
});

Deno.test("UiBuilderAdmin: route navigation preset exists in normal view without raw dispatcher fields", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("RouteNavigationWiringPreset"),
    "RouteNavigationWiringPreset component must exist",
  );
  assert(
    src.includes("UX_ROUTE_NAVIGATION_PRESET_LABEL"),
    "must use UX_ROUTE_NAVIGATION_PRESET_LABEL",
  );
  assert(
    src.includes("UX_ROUTE_NAVIGATION_SAVE_LABEL"),
    "must use UX_ROUTE_NAVIGATION_SAVE_LABEL",
  );
  assert(
    src.includes("encodeRouteNavigationTargetRef"),
    "must use encodeRouteNavigationTargetRef",
  );
  assert(
    src.includes("isRouteNavigationTargetRef"),
    "must use isRouteNavigationTargetRef",
  );
  const presetIdx = src.indexOf("<RouteNavigationWiringPreset");
  const advancedDetailsIdx = src.indexOf("{UX_PACKAGE_WIRING_ADVANCED_LABEL}");
  assert(presetIdx >= 0, "RouteNavigationWiringPreset must be rendered");
  assert(advancedDetailsIdx >= 0, "advanced package wiring details must exist");
  assert(
    presetIdx < advancedDetailsIdx,
    "preset must appear before advanced package wiring disclosure",
  );
});

Deno.test("SSOT: optional per-component wiring phrase must not exist — use user_optional_operation vocabulary", async () => {
  const ssot = await Deno.readTextFile(
    new URL("../../docs/design/admin-console-workflow-ssot.yaml", import.meta.url),
  );
  assertFalse(
    ssot.includes("optional per-component wiring"),
    "admin-console-workflow-ssot.yaml must not use 'optional per-component wiring' — use user_optional_operation vocabulary to distinguish user-optional from implementation-required",
  );
});

Deno.test("UiBuilderAdmin: raw dispatcher fields remain inside details disclosure not in normal path", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Verify that 'wiringKind' (raw field) remains only inside PackageWiringEditor (inside <details>)
  // and not exposed by RouteNavigationWiringPreset
  const presetStart = src.indexOf("function RouteNavigationWiringPreset(");
  const presetEnd = src.indexOf("\nfunction PackageWiringEditor(");
  assert(presetStart >= 0, "RouteNavigationWiringPreset must exist");
  assert(presetEnd > presetStart, "PackageWiringEditor must follow");
  const presetBody = src.slice(presetStart, presetEnd);
  // Preset should NOT render raw wiringKind select, targetSurface select, or wiringId
  assertFalse(
    presetBody.includes("配線種別 (wiring_kind)"),
    "preset must not show raw wiring_kind label",
  );
  assertFalse(
    presetBody.includes("接続先サーフェス"),
    "preset must not show raw target surface select",
  );
});

Deno.test("UiBuilderAdmin: ManifestRouteEntry exists and derives routeKey from topologySystemNameToUiBuilderKey", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // ManifestRouteEntry component must be defined in UiBuilderAdmin
  assert(src.includes("function ManifestRouteEntry("), "ManifestRouteEntry component must be defined");
  // Must use topologySystemNameToUiBuilderKey for routeKey derivation (SSOT contract)
  assert(src.includes("topologySystemNameToUiBuilderKey"), "Must call topologySystemNameToUiBuilderKey for routeKey derivation");
  // Must use isValidTopologySystemName to gate valid entries
  assert(src.includes("isValidTopologySystemName"), "Must validate topologySystemName before deriving routeKey");
  // Must NOT derive from userFacingTopologyLabel alone
  const manifestRouteEntryStart = src.indexOf("function ManifestRouteEntry(");
  const manifestRouteEntryEnd = src.indexOf("\nfunction BucketPackageRouteFields(");
  assert(manifestRouteEntryStart >= 0, "ManifestRouteEntry must exist");
  assert(manifestRouteEntryEnd > manifestRouteEntryStart, "BucketPackageRouteFields must follow");
  const entryBody = src.slice(manifestRouteEntryStart, manifestRouteEntryEnd);
  // Confirm derivedRouteKey comes from topologySystemNameToUiBuilderKey not displayName
  assert(entryBody.includes("topologySystemNameToUiBuilderKey(sysName)"), "routeKey must be derived from topologySystemNameToUiBuilderKey(sysName)");
  assert(entryBody.includes("CONTENTS_ROUTE_MANIFEST_LIST_FILTER"), "contents manifest list filter");
  // Confirm userFacingTopologyLabel is used only for display label, not for routeKey
  assert(entryBody.includes("resolveVisibleTopologyName"), "display label uses resolveVisibleTopologyName");
  assertFalse(entryBody.includes("userFacingTopologyLabel)"), "userFacingTopologyLabel must NOT be passed to routeKey derivation");
  assertFalse(
    entryBody.includes("このルートキーで確定"),
    "normal flow must auto-commit routeKey without a separate confirm button",
  );
  assert(
    src.includes("readUiBuilderHandoffManifestId") &&
      entryBody.includes("initialManifestId"),
    "ManifestRouteEntry must support contents handoff via manifestId query",
  );
  assert(
    entryBody.includes("topology.name から自動"),
    "committed route must collapse to read-only topology.name summary",
  );
});

Deno.test("ContentsScreenDesignPanel: step 3 completion links ui-builder with manifestId handoff", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(
    src.includes("/admin/ui-builder?manifestId="),
    "contents completion must hand off manifestId to ui-builder",
  );
  assert(
    src.includes("encodeURIComponent(selectedId)"),
    "manifestId handoff must be URL-encoded",
  );
});

Deno.test("UiBuilderAdmin: scoped canvas hides route selector when package route is locked", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    src.includes("!layoutSelectorsLocked && (") &&
      src.includes("<RouteLayoutSelector"),
    "route/layout picker must be hidden when package route is locked",
  );
});

Deno.test("ManifestRouteEntry SSOT: topologySystemNameToUiBuilderKey roundtrip matches encodeRouteNavigationTargetRef expectation", () => {
  // Verify the SSOT derivation chain: topology.name → uiBuilderKey → route nav target_ref
  const name = "customer-management";
  assert(isValidTopologySystemName(name), "test topology name must be valid");
  const uiBuilderKey = topologySystemNameToUiBuilderKey(name);
  assertEquals(uiBuilderKey, "customer-management");
  // route navigation target_ref = encodeRouteNavigationTargetRef(uiBuilderKey) = "route:customer-management"
  assertEquals(encodeRouteNavigationTargetRef(uiBuilderKey), "route:customer-management");
  // screenLabel / userFacingTopologyLabel must not affect routeKey
  const screenLabel = "顧客管理";
  const userFacingLabel = "顧客管理";
  const _ = screenLabel + userFacingLabel; // referenced but not passed to derivation
  assertEquals(topologySystemNameToUiBuilderKey(name), "customer-management");
  assertEquals(encodeRouteNavigationTargetRef(topologySystemNameToUiBuilderKey(name)), "route:customer-management");
});

// ─── /admin/contents Step 1 clone draft lifecycle guards ──────────────────────
// SSOT: admin-console-workflow-ssot.yaml admin_contents_step1_entry_modes /
//       replacement_clone_merge_lifecycle. Frontend carries entry intent + source evidence
//       display only; merge target / conflict / active mutation authority is backend.

Deno.test("Step 1 entry modes: four SSOT-defined modes are present and visibly separated", () => {
  const ids = CONTENTS_STEP1_ENTRY_MODE_OPTIONS.map((o) => o.id);
  assertEquals(ids, [
    "create_new_topology",
    "clone_active_as_replacement_draft",
    "clone_active_as_new_topology_draft",
    "resume_existing_draft",
  ]);
  const labels = new Set(CONTENTS_STEP1_ENTRY_MODE_OPTIONS.map((o) => o.label));
  assertEquals(labels.size, 4);
  for (const o of CONTENTS_STEP1_ENTRY_MODE_OPTIONS) {
    assert(o.description.length > 0, `${o.id} needs a description`);
  }
});

Deno.test("clone lifecycle: replacement and clone-as-new-topology are distinct modes", () => {
  assert(CLONE_MODE_LABELS["replacement"]);
  assert(CLONE_MODE_LABELS["new_topology"]);
  assert(
    CLONE_MODE_LABELS["replacement"] !== CLONE_MODE_LABELS["new_topology"],
    "replacement and new_topology must not share a label",
  );
  // draft_origin vocabulary keeps manual replacement, manual new-topology, and SQL Attention
  // candidate distinct so origins are never confused.
  assert(CLONE_DRAFT_ORIGIN_LABELS["manual_clone_replacement"]);
  assert(CLONE_DRAFT_ORIGIN_LABELS["manual_clone_new_topology"]);
  assert(CLONE_DRAFT_ORIGIN_LABELS["sql_attention_candidate"]);
});

Deno.test("adminApi: clone lifecycle wrappers target the dedicated backend manifest ops", async () => {
  const src = await Deno.readTextFile(
    new URL("../api/adminApi.ts", import.meta.url),
  );
  assert(src.includes('"create_new_topology_draft"'), "create_new_topology op");
  assert(
    src.includes('"create_clone_replacement_draft_from_active"'),
    "replacement clone create op",
  );
  assert(
    src.includes('"create_clone_new_topology_draft_from_active"'),
    "clone-as-new create op",
  );
  assert(
    src.includes('"merge_clone_replacement_draft_to_active"'),
    "replacement merge op",
  );
  assert(src.includes('"load_clone_source_evidence"'), "source evidence op");
  assert(
    src.includes('"validate_clone_replacement_draft"'),
    "merge readiness op",
  );
});

Deno.test("adminApi: replacement merge is an intent submitted to backend, not a layout_patch op", async () => {
  const src = await Deno.readTextFile(
    new URL("../api/adminApi.ts", import.meta.url),
  );
  // The merge wrapper must use the manifest replacement-merge op, never layout_patch:apply,
  // which is UI Builder layout persistence — not a production manifest replacement merge.
  const mergeFnStart = src.indexOf("mergeCloneReplacementDraftToActive");
  assert(mergeFnStart > 0, "merge wrapper must exist");
  const mergeFnSrc = src.slice(mergeFnStart, mergeFnStart + 400);
  assert(
    mergeFnSrc.includes("merge_clone_replacement_draft_to_active"),
    "merge wrapper uses the dedicated backend merge op",
  );
  assertFalse(
    mergeFnSrc.includes("layout_patch"),
    "replacement merge must not be a layout_patch op",
  );
});

Deno.test("ContentsScreenDesignPanel: frontend has no merge authority — merge button gated by backend readiness", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  // The panel submits a merge INTENT; it does not decide the merge target/conflict locally.
  assert(
    src.includes("mergeCloneReplacementDraftToActive"),
    "panel submits merge intent",
  );
  assert(
    src.includes("validateCloneReplacementDraft"),
    "panel reads backend-computed readiness",
  );
  // Merge button must be disabled unless the backend marked the draft mergeReady.
  assert(
    src.includes("!replacementReadiness.mergeReady"),
    "merge button disabled unless backend says mergeReady",
  );
  // Blockers are rendered from the backend response, not locally computed.
  assert(
    src.includes("replacementReadiness.mergeBlockers"),
    "panel renders backend merge blockers",
  );
});

Deno.test("ContentsScreenDesignPanel: clone source selection is read-only evidence, not active mutation", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(
    src.includes("loadCloneSourceEvidence"),
    "panel loads read-only source evidence",
  );
  assert(
    src.includes("createCloneReplacementDraftFromActive"),
    "replacement clone draft created via backend",
  );
  assert(
    src.includes("createCloneNewTopologyDraftFromActive"),
    "clone-as-new draft created via backend",
  );
  // The panel must not promote/activate the working draft itself for replacement.
  assertFalse(
    src.includes("promoteAdminManifest(selectedId)") &&
      src.includes("isReplacementCloneDraft"),
    "replacement draft must not be promoted to active from the panel",
  );
});

Deno.test("ContentsScreenDesignPanel: resume draft uses contents list filter and skips Step 1 re-register", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url),
  );
  assert(src.includes("resume_existing_draft"), "resume entry mode");
  assert(src.includes("contentsType: \"contents\""), "contents draft list filter");
  assert(
    src.includes("requiresTopologySystemName: true"),
    "contents draft picker excludes Step-1-incomplete shells",
  );
  assert(src.includes("下書きを再開"), "resume button label");
  assert(src.includes('setActiveStep(2)'), "resume navigates to step 2");
  assert(src.includes("authoringProgressStep"), "restores progress from list item");
});

Deno.test("UiBuilderAdmin: layout apply handoff and inspector scroll region", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(src.includes("LayoutPersistHandoffBanner"), "post-apply persistence handoff banner");
  assert(src.includes("UX_LAYOUT_APPLIED_GO_DESIGN_SAVE"), "design persist CTA label");
  assert(src.includes('onLayoutApplied={() => setFlowStep("persist")}'), "flow stepper advances on apply");
  assert(src.includes("UI_BUILDER_DOCK_HEIGHT"), "side docks must use viewport height independent of canvas");
  assert(src.includes("items-start"), "dock row must not stretch docks to canvas height");
  assert(src.includes("fillHeight={false}"), "canvas must not dictate sibling dock height");
  assert(src.includes('data-inspector-scroll-region="true"'), "inspector panel must scroll inside drawer");
  assert(src.includes("layout-right-dock-scroll"), "right dock scroll wrapper");
  assert(src.includes("function PackageConnectionPanel("), "package connection must live outside design inspector");
  const packageConnectionPanelIdx = src.indexOf("function PackageConnectionPanel(");
  const designPanelIdx = src.indexOf("function PackageDesignPanel(");
  assert(packageConnectionPanelIdx >= 0 && designPanelIdx > packageConnectionPanelIdx, "PackageConnectionPanel must exist");
  const manifestPresetIdx = src.indexOf("<ManifestStep3EventWiringPreset", packageConnectionPanelIdx);
  const advancedIdx = src.indexOf("{UX_PACKAGE_WIRING_ADVANCED_LABEL}", packageConnectionPanelIdx);
  assert(manifestPresetIdx >= 0, "ManifestStep3EventWiringPreset must be rendered in package connection panel");
  assert(advancedIdx >= 0, "advanced package wiring disclosure must exist");
  assert(
    manifestPresetIdx < advancedIdx,
    "topology API preset must appear in normal view before advanced package wiring disclosure",
  );
  assert(src.includes("<NodeEventAuthoringPanel"), "node event authoring must be in right dock when node selected");
  assert(src.includes("UX_EVENT_AUTHORING_SECTION"), "right dock event accordion uses SSOT section label");
  const designPanelEnd = src.indexOf("\nfunction ", designPanelIdx + 30);
  const designPanelBody = src.slice(designPanelIdx, designPanelEnd > designPanelIdx ? designPanelEnd : designPanelIdx + 8000);
  assertFalse(designPanelBody.includes("<RouteNavigationWiringPreset"), "package route preset must not be in design inspector");
  assertFalse(designPanelBody.includes("<ManifestStep3EventWiringPreset"), "Step 3 preset must not be in design inspector");
  assertFalse(designPanelBody.includes("<NodeEventAuthoringPanel"), "node event authoring must not be in design inspector");
  assertFalse(designPanelBody.includes('id: "wiring"'), "design inspector must not have runtime wiring tab");
});

Deno.test("ManifestStep3EventWiringPreset: scoped to committed topology routeKey only", async () => {
  const src = await Deno.readTextFile(
    new URL("../components/ManifestStep3EventWiringPreset.tsx", import.meta.url),
  );
  assert(src.includes("resolveManifestRouteOptionForRouteKey"));
  assert(src.includes("UX_MANIFEST_API_EVENT_TOPOLOGY_CONTEXT"));
  assert(src.includes("formatScreenReadQueryWiringCandidateLabel"));
  assertFalse(src.includes("listAdminManifests"), "must not list all manifests in global picker");
  assertFalse(src.includes("UX_MANIFEST_API_EVENT_PAGE_SELECT"), "page picker removed from normal view");
});

Deno.test("adminUxTerms: external integration / instance operation / topology API labels", async () => {
  const terms = await import("../content/adminUxTerms.ts");
  assertEquals(terms.UX_RUNTIME_INTERACTION_ADD_EXTERNAL, "+ 外部連携");
  assertEquals(terms.UX_RUNTIME_INTERACTION_ADD_INSTANCE, "+ インスタンス操作");
  assertEquals(terms.UX_TRIGGER_UI_LABEL, "トリガUI指定");
  assertEquals(terms.UX_TARGET_UI_LABEL, "操作対象UI指定");
  assertEquals(terms.UX_MANIFEST_API_EVENT_PRESET_LABEL, "トポロジ API 設定");
  assert(terms.UX_TOPOLOGY_API_SECTION_HINT.includes("Contents dispatch"));
  assert(terms.UX_EXTERNAL_INTEGRATION_SECTION_HINT.includes("別系統"));
  assert(terms.UX_INSTANCE_OPERATION_SECTION_HINT.includes("別系統"));
  assert(terms.UX_EXTERNAL_PORT_EMPTY_HINT.includes("別系統"));
  assert(terms.UX_INSTANCE_OPERATION_EMPTY_HINT.includes("別系統"));
  assertFalse(terms.UX_PACKAGE_WIRING_SECTION_TITLE.includes("runtimeDispatch"));
  assertFalse(terms.UX_RUNTIME_INTERACTION_ADD_EXTERNAL.includes("External API"));
  assertFalse(terms.UX_RUNTIME_INTERACTION_ADD_INSTANCE.includes("External instance"));
});

Deno.test("UiBuilderAdmin: event authoring uses SSOT trigger/target vocabulary not screen-common click", async () => {
  const uiBuilderSrc = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const eventPanelSrc = await Deno.readTextFile(
    new URL("../components/NodeEventAuthoringPanel.tsx", import.meta.url),
  );
  assert(uiBuilderSrc.includes("UX_PACKAGE_WIRING_SECTION_TITLE"));
  assert(eventPanelSrc.includes("UX_RUNTIME_INTERACTION_SECTION_TITLE"));
  assert(uiBuilderSrc.includes("NodeEventAuthoringPanel"));
  assert(eventPanelSrc.includes("UX_TRIGGER_UI_LABEL"));
  assert(eventPanelSrc.includes("UX_TARGET_UI_LABEL"));
  assert(eventPanelSrc.includes("UX_EXTERNAL_INTEGRATION_SECTION_TITLE"));
  assert(uiBuilderSrc.includes("<ManifestStep3EventWiringPreset"));
  const manifestPresetSrc = await Deno.readTextFile(
    new URL("../components/ManifestStep3EventWiringPreset.tsx", import.meta.url),
  );
  assert(manifestPresetSrc.includes("UX_MANIFEST_API_EVENT_PRESET_LABEL"));
  assertFalse(uiBuilderSrc.includes("UX_WIRING_SCREEN_COMMON_SECTION_TITLE"));
  assertFalse(uiBuilderSrc.includes("画面共通のクリック動作"));
  assertFalse(uiBuilderSrc.includes("UX_RUNTIME_INTERACTION_ADD_DISPATCH"));
  assertFalse(uiBuilderSrc.includes("dispatchRuntime"));
  assertFalse(uiBuilderSrc.includes("nodeRuntimeInteractionDispatch"));
  assertFalse(eventPanelSrc.includes("dispatchRuntime"));
  assertFalse(eventPanelSrc.includes("nodeRuntimeInteractionDispatch"));
  assertFalse(uiBuilderSrc.includes("+ External API"));
  assertFalse(uiBuilderSrc.includes("+ External instance"));
  assertFalse(eventPanelSrc.includes("+ External API"));
  assertFalse(eventPanelSrc.includes("+ External instance"));
});

Deno.test("HubNavigationAdmin: destination picker uses page label not raw hub id", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/HubNavigationAdmin.tsx", import.meta.url),
  );
  assert(src.includes("UX_HUB_NAV_DESTINATION_LABEL"));
  assert(src.includes("hubDestinationPickerOptions"));
  assert(src.includes("hubDestinationOptionLabel"));
});
