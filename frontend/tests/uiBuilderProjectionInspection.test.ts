/**
 * uiBuilderProjectionInspection.test.ts — /admin/ui-builder read-only
 * projection inspection proof.
 *
 * Acceptance (bundle ui-projection-surface-architecture-reinforcement):
 * - inspection lives in /admin/ui-builder component scope (no standalone /demo route)
 * - applied topology / route / package / manifest / read query confirmation
 * - rows / activeColumns / displayColumnMode confirmation from emission.data branches
 * - propBindings confirmation resolved from emission.data branches
 *   (not first-row sample, not seedLabel smoke)
 * - the panel is read-only: not persistence authority, not promotion authority,
 *   not canonical projection entry, not seed fallback
 */
import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildAppliedProjectionConfirmation,
  buildPropBindingsConfirmation,
  compareCanvasPreviewWithApplied,
} from "../lib/uiBuilderProjectionInspection.ts";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import type { UserOperation } from "../runtime/resolveOperationVector.ts";

const axes: UserOperation = {
  operationType: "Search",
  target: "orders.list",
  layer: "screen_list",
  action: "Search",
};

const emission: Emission = {
  packageId: "bbbbbbbb-0000-0000-0000-000000000002",
  layoutId: "cccccccc-0000-0000-0000-000000000003",
  componentIds: [],
  data: {
    kind: "screen_data_shape_query_result",
    manifestId: "aaaaaaaa-0000-0000-0000-000000000001",
    rows: [{ name: "Alice" }, { name: "Bob" }],
    aggregationResults: [],
    activeColumns: ["name"],
    displayColumnMode: "all",
  },
  layoutNodes: [
    {
      nodeId: "table-1",
      orderIndex: 0,
      componentKind: "data_display/table",
      propBindings: {
        rows: { source: "emission.data.rows" },
        columns: {
          source: "emission.data.activeColumns",
          transform: "activeColumnsToTableColumns",
        },
      },
    },
    {
      nodeId: "json-1",
      orderIndex: 1,
      componentKind: "data_display/json",
      propBindings: {
        data: { source: "emission.data.displayColumnMode" },
      },
    },
    {
      nodeId: "unbound-1",
      orderIndex: 2,
      componentKind: "data_display/list",
      propBindings: {
        rows: { source: "emission.data.absentBranch" },
      },
    },
  ] as LayoutNode[],
};

// ── applied topology / read query / collection shape confirmation ───────────

Deno.test("inspection: applied confirmation reports read query axes, topology identity, and collection branches", () => {
  const confirmation = buildAppliedProjectionConfirmation(axes, emission);
  assertEquals(confirmation.readQuery.target, "orders.list");
  assertEquals(confirmation.readQuery.layer, "screen_list");
  assertEquals(confirmation.readQuery.action, "Search");
  assertEquals(confirmation.resultKind, "screen_data_shape_query_result");
  assertEquals(confirmation.manifestId, "aaaaaaaa-0000-0000-0000-000000000001");
  assertEquals(confirmation.layoutId, "cccccccc-0000-0000-0000-000000000003");
  assertEquals(confirmation.packageId, "bbbbbbbb-0000-0000-0000-000000000002");
  assertEquals(confirmation.rowCount, 2);
  assertEquals(confirmation.activeColumns, ["name"]);
  assertEquals(confirmation.displayColumnMode, "all");
  assertEquals(confirmation.aggregationResultCount, 0);
});

Deno.test("inspection: applied confirmation reports target_ref for manifest-pinned reads", () => {
  const pinned: UserOperation = {
    ...axes,
    payload: {
      target_ref: "manifest:aaaaaaaa-0000-0000-0000-000000000001:projection_entry",
    },
  };
  const confirmation = buildAppliedProjectionConfirmation(pinned, emission);
  assertEquals(
    confirmation.readQuery.targetRef,
    "manifest:aaaaaaaa-0000-0000-0000-000000000001:projection_entry",
  );
});

// ── propBindings confirmation from emission.data branches ───────────────────

Deno.test("inspection: propBindings confirmation resolves from emission.data branches, not first-row samples", () => {
  const entries = buildPropBindingsConfirmation(
    emission.layoutNodes,
    emission.data,
  );
  const rowsEntry = entries.find((e) =>
    e.nodeId === "table-1" && e.propName === "rows"
  );
  assertEquals(rowsEntry?.status, "resolved");
  assertEquals(rowsEntry?.resolvedKind, "array");
  assertEquals(rowsEntry?.resolvedCount, 2);

  const columnsEntry = entries.find((e) =>
    e.nodeId === "table-1" && e.propName === "columns"
  );
  assertEquals(columnsEntry?.status, "resolved");
  assertEquals(columnsEntry?.source, "emission.data.activeColumns");
  assertEquals(columnsEntry?.transform, "activeColumnsToTableColumns");

  const modeEntry = entries.find((e) => e.nodeId === "json-1");
  assertEquals(modeEntry?.status, "resolved");
  assertEquals(modeEntry?.source, "emission.data.displayColumnMode");
  assertEquals(modeEntry?.resolvedKind, "string");
});

Deno.test("inspection: absent emission.data branch is reported unresolved — no sample substitution", () => {
  const entries = buildPropBindingsConfirmation(
    emission.layoutNodes,
    emission.data,
  );
  const absent = entries.find((e) => e.nodeId === "unbound-1");
  assertEquals(absent?.status, "unresolved");
});

Deno.test("inspection: non-emission.data source is flagged invalid, never resolved", () => {
  const entries = buildPropBindingsConfirmation(
    [{
      nodeId: "bad-1",
      orderIndex: 0,
      propBindings: { rows: { source: "window.rows" } },
    }] as LayoutNode[],
    emission.data,
  );
  assertEquals(entries[0]?.status, "invalid_source");
});

// ── canvas preview vs applied projection comparison ──────────────────────────

Deno.test("inspection: canvas preview vs applied projection comparison by node identity", () => {
  const comparison = compareCanvasPreviewWithApplied(
    [{ nodeId: "table-1" }, { nodeId: "json-1" }, { nodeId: "draft-only" }],
    emission.layoutNodes,
  );
  assertEquals(comparison.previewNodeCount, 3);
  assertEquals(comparison.appliedNodeCount, 3);
  assertEquals(comparison.onlyInPreview, ["draft-only"]);
  assertEquals(comparison.onlyInApplied, ["unbound-1"]);
  assertEquals(comparison.common, ["table-1", "json-1"]);
  assertEquals(comparison.matches, false);
});

Deno.test("inspection: identical node sets compare as matching", () => {
  const comparison = compareCanvasPreviewWithApplied(
    emission.layoutNodes,
    emission.layoutNodes,
  );
  assertEquals(comparison.matches, true);
});

// ── component scope + read-only boundary (source scan) ──────────────────────

Deno.test("inspection: panel is a /admin/ui-builder component — no standalone /demo route remains", async () => {
  const panelSrc = await Deno.readTextFile(
    new URL(
      "../components/UiBuilderProjectionInspectionPanel.tsx",
      import.meta.url,
    ),
  );
  const adminSrc = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assert(
    adminSrc.includes("UiBuilderProjectionInspectionPanel"),
    "inspection panel must be mounted inside /admin/ui-builder island scope",
  );
  assert(
    panelSrc.includes('id="projection-inspection"'),
    "inspection panel must expose the in-page inspection anchor",
  );
  // Standalone /demo route must not exist (non-canonical; not revived).
  let demoRouteExists: boolean;
  try {
    await Deno.stat(new URL("../routes/demo.tsx", import.meta.url));
    demoRouteExists = true;
  } catch {
    demoRouteExists = false;
  }
  assertFalse(demoRouteExists, "standalone /demo route must not be retained");
  const freshGen = await Deno.readTextFile(
    new URL("../fresh.gen.ts", import.meta.url),
  );
  assertFalse(
    freshGen.includes("routes/demo.tsx"),
    "fresh manifest must not register a /demo route",
  );
});

Deno.test("inspection: panel is read-only — no persistence / promotion / apply authority", async () => {
  const panelSrc = await Deno.readTextFile(
    new URL(
      "../components/UiBuilderProjectionInspectionPanel.tsx",
      import.meta.url,
    ),
  );
  const helperSrc = await Deno.readTextFile(
    new URL("../lib/uiBuilderProjectionInspection.ts", import.meta.url),
  );
  for (const src of [panelSrc, helperSrc]) {
    for (
      const forbidden of [
        "layout_patch:apply",
        "layout_patch:validate",
        "component_style_design:upsert",
        ":promote",
        ":apply",
        "manifest:update",
        "manifest:create",
      ]
    ) {
      assertFalse(
        src.includes(forbidden),
        `inspection surface must not carry mutation operation "${forbidden}"`,
      );
    }
  }
  // Read-only render: inert preview bindings only.
  assert(
    panelSrc.includes("previewMode: true"),
    "inspection render must use previewMode inert bindings",
  );
  assert(
    panelSrc.includes('data-uibuilder-inspection="read_only"'),
    "inspection panel must declare its read-only boundary",
  );
  // The panel dispatches ONLY the Search read lane via the shared entry resolver.
  assert(
    panelSrc.includes("resolveProjectionEntryAxes"),
    "applied-side read must reuse the production projection entry axes resolver",
  );
  assertFalse(
    panelSrc.includes('operationType: "Create"') ||
      panelSrc.includes('operationType: "diffUpdate"') ||
      panelSrc.includes('operationType: "logicalDelete"'),
    "inspection panel must not dispatch mutation operations",
  );
});
