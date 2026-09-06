/**
 * Production-composition boundary proof for the Team Dashboard Admin/Normal canonical
 * shared-dashboard surfaces (docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * surface_axes.admin.surfaces.team_dashboard / surface_axes.normal.surfaces.dashboard.
 * team_dashboard_canonical_shared_contract). The LayoutNode[] fixtures below are the real
 * production tensor shape verbatim from db/seed_empty.sql's dd010 (team_dashboard.admin.
 * projection) and dd020 (team_dashboard.normal.projection) rows (layout_schema_json is
 * {"records":[]} for both -- the tensor-only path, so no schema-record composition is
 * involved), driven through the real renderEmission() -> LayoutProjectionTree ->
 * runtimeComponentFactory -> real DOM pipeline (happy-dom + Preact render(), real native
 * DOM events -- never renderToString or dispatcher.set() standing in for a real interaction).
 *
 * This is the regression proof for the production-projection-boundary bug this Bundle fixes:
 * frontend/runtime/renderEmission.ts's buildProductionCatalogComponentProps() had no explicit
 * case for componentKind="data_display/md_viewer", so its default branch fell through to
 * buildLayoutPreviewPlaceholderProps() (frontend/runtime/layoutComponentPreview.ts) -- the SAME
 * synthetic UI-Builder canvas-preview savedView (preview.sample_table / preview_record_001 /
 * preview_template / preview.v1) authoring/canvas preview legitimately uses -- even in
 * PRODUCTION (non-preview) rendering. Because runtimeComponentFactory.ts's mdViewerPreviewFactory
 * documented savedView>markdown priority is unchanged (and correct -- a REAL authored savedView
 * must still win over a bare markdown string), the synthetic placeholder savedView silently
 * outranked the real propBindings.markdown value (emission.data.bodyMarkdown), so both Team
 * Dashboard axes rendered UI-Builder Saved View preview chrome instead of the real note body.
 * Fixed generically by componentKind (never by team_dashboard/route/manifest/node-id) -- see the
 * third Deno.test below for the same-mechanism regression proof against UI-Builder canvas
 * authoring preview (previewMode=true), which must keep the synthetic placeholder unchanged.
 * Team Markdown Dashboard Saved View (frontend/tests/
 * teamMarkdownSavedView.test.ts) renders MdViewer directly as a Preact component, entirely
 * outside this renderEmission/runtimeComponentFactory catalog-component pathway, so it is
 * structurally unaffected by either the bug or this fix.
 *
 * A second, independent production-completion gap surfaced and fixed alongside it (same Bundle,
 * same generic production-projection-boundary scope): frontend/runtime/propBindingResolver.ts's
 * resolvePropBindings() scalar branch (acceptsNonArrayResolvedValue -- form_input/search_input,
 * form_input/textarea_template, data_display/md_viewer) set only the top-level props[propName],
 * never mirroring into props.data[propName] the way the array branch already documented and did.
 * Concretely: team_dashboard_admin_body (textarea.template, which reads props.data.value, not
 * top-level props.value) had a real, successfully-resolving propBindings.value binding
 * (emission.data.bodyMarkdown) that was silently shadowed by the stale "" placeholder default --
 * the Admin textarea never actually displayed the real bodyMarkdown despite the binding
 * "succeeding" with zero error. Fixed by mirroring the scalar branch identically to the existing
 * array branch. The Admin Deno.test below (step 2) is the regression proof.
 *
 * KNOWN, OUT-OF-SCOPE gap intentionally NOT fixed by this Bundle (reported to the auditor, never
 * silently patched here or in db/seed_empty.sql): team_dashboard's Admin Save/Cancel/Confirm
 * buttons (componentKey=button.primitive) render their visible button LABEL as the literal
 * shared componentKey string ("button.primitive"), never their seed-authored propsJson
 * {"label": "..."} text. buildProductionCatalogComponentProps's action/button case reads
 * node.label (populated only for schema-composed leaves via layout_schema_json.records[] -- see
 * runtime-orchestration-ssot.yaml layout_schema_structural_render_contract), which stays absent
 * for a tensor-only node (team_dashboard's own layout_schema_json is {"records":[]}); the
 * tensor's own flat, top-level propsJson {"label": ...} convention (db/seed_empty.sql's own
 * authored shape for every button.primitive node in this seed, team_dashboard's included) is a
 * shape buttonFactory never reads (it reads props.data.label, which mergeNodeLocalProps' shallow
 * top-level merge never touches). This is a distinct, likely-generic, pre-existing authoring/
 * runtime-contract mismatch outside this Bundle's explicit scope (the Saved-View-chrome
 * production-projection-boundary) -- this file deliberately does NOT assert on the Save/Cancel/
 * Confirm buttons' visible label text, only on their real dispatch behavior via data-node-id
 * (machine identity, never a label string, the same principle
 * credentialManagementCategorySelectorProductionPath.test.ts's own doc comment states), so this
 * known gap neither silently fails this proof nor gets misrepresented as fixed by it.
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, render } from "preact";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";
import {
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
} from "../runtime/uiEventEffectRunner.ts";
import { createLiveNodeValueTracker } from "../runtime/liveNodeValueTracker.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";

// The real body_markdown db/seed_empty.sql seeds into topology.team_dashboard_note (note_id
// dd001) -- reused verbatim here so this fixture's "real content" is the actual production
// default, not an arbitrary test string.
const REAL_BODY_MARKDOWN = "# Team Dashboard\n\nShared notes go here.";

// Verbatim from db/seed_empty.sql's dd015 tensor row (team_dashboard.admin.projection,
// route_key "admin/team-dashboard#default"), with the wiring identity fields
// (wiringId/wiringKey/wiringKind/targetSurface/targetRef, componentId, componentKind) every
// node in this layout inherits uniformly from its single ui_wiring_registry row (dd014) and
// ui_component_registry (db/ui_component_registry_preset_catalog_bootstrap.sql) added
// explicitly, the same way NpgsqlTopologyRepository.LoadLayoutNodesAsync composes them onto
// the real Emission.LayoutNodes this test stands in for.
const ADMIN_WIRING = {
  wiringId: "00000000-0000-0000-0000-0000000dd014",
  wiringKey: "team_dashboard.admin.projection.wiring",
  wiringKind: "admin_runtime",
  targetSurface: "manifest",
  targetRef: "team_dashboard.admin.projection",
};
const ADMIN_UPDATE_TARGET_REF =
  "manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update";

const ADMIN_LAYOUT_NODES: LayoutNode[] = [
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_viewer",
    nodeKind: "catalog_component",
    componentKey: "md_viewer.projection",
    componentId: "00000000-0000-0000-0001-000000000021",
    componentKind: "data_display/md_viewer",
    orderIndex: 0,
    runtimeInteractions: [],
    propsJson: '{"label": "Rendered preview"}',
    propBindings: { markdown: { source: "emission.data.bodyMarkdown" } },
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_body",
    nodeKind: "catalog_component",
    componentKey: "textarea.template",
    componentId: "00000000-0000-0000-0001-00000000001f",
    componentKind: "form_input/textarea_template",
    orderIndex: 1,
    runtimeInteractions: [],
    propsJson: '{"label": "Markdown body"}',
    propBindings: { value: { source: "emission.data.bodyMarkdown" } },
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_save_button",
    nodeKind: "catalog_component",
    componentKey: "button.primitive",
    componentId: "00000000-0000-0000-0001-000000000010",
    componentKind: "action/button",
    orderIndex: 2,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "openModal",
        targetNodeId: "team_dashboard_admin_save_confirm_modal",
        statePath: "open",
      },
    ],
    dispatchTargetRefByTrigger: { click: ADMIN_UPDATE_TARGET_REF },
    dispatchPayloadFromByTrigger: {
      click: { bodyMarkdown: "node:team_dashboard_admin_body.value", dryRun: "literal:true" },
    },
    propsJson: '{"label": "Save"}',
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_save_confirm_modal",
    nodeKind: "catalog_component",
    componentKey: "modal.template",
    componentId: "00000000-0000-0000-0001-000000000015",
    componentKind: "disclosure/modal",
    orderIndex: 3,
    runtimeInteractions: [
      {
        trigger: "toggle",
        actionType: "closeModal",
        targetNodeId: "team_dashboard_admin_save_confirm_modal",
        statePath: "open",
      },
    ],
    propsJson:
      '{"data": {"open": false, "title": "Save team dashboard", "body": "Save the edited Markdown as the team dashboard\'s shared content."}}',
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_save_confirm_button",
    nodeKind: "catalog_component",
    componentKey: "button.primitive",
    componentId: "00000000-0000-0000-0001-000000000010",
    componentKind: "action/button",
    orderIndex: 4,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: "team_dashboard_admin_save_confirm_modal",
        statePath: "open",
      },
    ],
    dispatchTargetRefByTrigger: { click: ADMIN_UPDATE_TARGET_REF },
    dispatchPayloadFromByTrigger: {
      click: { bodyMarkdown: "node:team_dashboard_admin_body.value", confirmed: "literal:true" },
    },
    propsJson: '{"label": "Save"}',
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_save_cancel_button",
    nodeKind: "catalog_component",
    componentKey: "button.primitive",
    componentId: "00000000-0000-0000-0001-000000000010",
    componentKind: "action/button",
    orderIndex: 5,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: "team_dashboard_admin_save_confirm_modal",
        statePath: "open",
      },
    ],
    propsJson: '{"label": "Cancel"}',
  },
];

// Verbatim from db/seed_empty.sql's dd025 tensor row (team_dashboard.normal.projection,
// route_key "dashboard#default") -- a single read-only viewer node, no editor/save/mutation
// nodes at all (not merely hidden ones).
const NORMAL_LAYOUT_NODES: LayoutNode[] = [
  {
    nodeId: "team_dashboard_normal_viewer",
    nodeKind: "catalog_component",
    componentKey: "md_viewer.projection",
    componentId: "00000000-0000-0000-0001-000000000021",
    componentKind: "data_display/md_viewer",
    wiringId: "00000000-0000-0000-0000-0000000dd024",
    wiringKey: "team_dashboard.normal.projection.wiring",
    wiringKind: "admin_runtime",
    targetSurface: "manifest",
    targetRef: "team_dashboard.normal.projection",
    orderIndex: 0,
    runtimeInteractions: [],
    propsJson: '{"label": "Rendered preview"}',
    propBindings: { markdown: { source: "emission.data.bodyMarkdown" } },
  },
];

// Synthetic UI-Builder canvas-preview placeholder content (layoutComponentPreview.ts's
// data_display/md_viewer case) -- must never reach either axis's production DOM.
const SYNTHETIC_SAVED_VIEW_MARKERS = [
  "preview.sample_table",
  "preview_record_001",
  "preview_template",
  "preview.v1",
  "preview-saved-view-001",
  "プレビュー サンプルビュー",
  "これは保存済み Markdown ビューのプレビューです",
  "サンプル項目",
  "md-viewer-action-toolbar",
  ">Refresh<",
  ">Clone<",
  ">Rebind<",
];

function toRunnerWiringNodes(layoutNodes: readonly LayoutNode[]): WiringNode[] {
  return layoutNodes
    .filter((n): n is LayoutNode & { nodeId: string } =>
      typeof n.nodeId === "string" && n.nodeId.length > 0
    )
    .map((n) => ({
      nodeId: n.nodeId,
      componentKey: n.componentKey,
      componentKind: n.componentKind,
      stateJson: n.stateJson ?? undefined,
      runtimeInteractions: n.runtimeInteractions ?? undefined,
      parentNodeId: n.parentNodeId,
      visibilityBinding: n.visibilityBinding ?? undefined,
    }));
}

function assertNoSyntheticSavedViewChrome(html: string, context: string): void {
  for (const marker of SYNTHETIC_SAVED_VIEW_MARKERS) {
    assert(
      !html.includes(marker),
      `expected no synthetic UI-Builder canvas-preview Saved View content ("${marker}") in ${context}, got: ${html}`,
    );
  }
}

Deno.test("production path: Admin /admin/team-dashboard (team_dashboard.admin.projection, dd010) renders the real bodyMarkdown bare-markdown + editable Textarea, zero synthetic Saved View chrome, and a full dryRun-preview -> explicit confirm -> confirmed team_dashboard:update dispatch chain (real DOM, real native events)", async () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000dd013",
    layoutNodes: ADMIN_LAYOUT_NODES,
    packageId: "00000000-0000-0000-0000-0000000dd012",
    manifestId: "00000000-0000-0000-0000-0000000dd010",
    data: { bodyMarkdown: REAL_BODY_MARKDOWN },
  };

  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const dispatchedBodies: Record<string, unknown>[] = [];
  // deno-lint-ignore no-explicit-any
  (globalThis as any).fetch = (url: string, init?: RequestInit) => {
    if (url.toString() !== "/api/dispatch") {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
    dispatchedBodies.push(body);
    const requestPayload = body.payload as Record<string, unknown> | undefined;
    const targetRef = typeof requestPayload?.target_ref === "string" ? requestPayload.target_ref : undefined;
    const manifestMatch = targetRef ? /^manifest:([^:]+):/.exec(targetRef) : null;
    const manifestId = manifestMatch ? manifestMatch[1] : "00000000-0000-0000-0000-000000000000";
    return Promise.resolve(
      new Response(
        JSON.stringify({
          success: true,
          errors: [],
          emission: { manifestId, layoutId: "mock-team-dashboard-dispatch", layoutNodes: [] },
        }),
        { status: 200 },
      ),
    );
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(ADMIN_LAYOUT_NODES), localStore);
  const tracker = createLiveNodeValueTracker();
  const { container, cleanup } = setupDom();

  try {
    function renderTree(): void {
      const specs = renderEmission(emission, defaultComponentRegistry, {
        localStateStore: dispatcher,
        payloadFromNodeValues: tracker.snapshot(),
        onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
      });
      assertEquals(
        specs.filter((s) => s.componentType === "error"),
        [],
        "expected zero error specs composing the real dd010 tensor shape",
      );
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }
    localStore.subscribe(renderTree);
    renderTree();
    await flushUpdates();

    // 1. Real bodyMarkdown reaches the viewer as bare markdown (bug fixed by this Bundle:
    // buildProductionCatalogComponentProps's new data_display/md_viewer case).
    const viewerHtml = container.querySelector('[data-node-id="team_dashboard_admin_viewer"]')?.innerHTML ?? "";
    assert(
      viewerHtml.includes('class="md-viewer-bare-markdown-preview"'),
      "expected the real bare-markdown mdViewerPreviewFactory branch to render (props.markdown, not a synthetic savedView)",
    );
    assert(viewerHtml.includes("<h1>Team Dashboard</h1>"), "expected the real bodyMarkdown heading in the viewer");
    assert(viewerHtml.includes("Shared notes go here."), "expected the real bodyMarkdown body text in the viewer");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the initial Admin DOM");

    // 2. Real bodyMarkdown reaches the Textarea's own initial value (bug fixed by this Bundle:
    // resolvePropBindings' scalar branch now mirrors into props.data like the array branch).
    const textareaEl = container.querySelector(
      '[data-node-id="team_dashboard_admin_body"] textarea',
    ) as unknown as { value: string; dispatchEvent: (e: Event) => boolean } | null;
    assert(textareaEl, "expected a real <textarea> for team_dashboard_admin_body");
    assertEquals(textareaEl!.value, REAL_BODY_MARKDOWN, "expected the real bodyMarkdown as the Textarea's initial value");

    // No confirm dialog before any Save click.
    assert(!container.querySelector('[role="dialog"]'), "expected no confirm modal in the DOM before the first Save click");

    // 3. Real native "input" edit on the real <textarea> — never tracker.set() standing in for it.
    const editedMarkdown = "# Team Dashboard\n\nUpdated via a real DOM edit.";
    textareaEl!.value = editedMarkdown;
    textareaEl!.dispatchEvent(new Event("input", { bubbles: true }));
    await flushUpdates();
    assertEquals(tracker.snapshot()["team_dashboard_admin_body"], editedMarkdown);

    // 4. Real click on the real Save button: dryRun-preview dispatch + opens the confirm modal.
    const saveButton = container.querySelector(
      '[data-node-id="team_dashboard_admin_save_button"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(saveButton, "expected a real Save button for team_dashboard_admin_save_button");
    saveButton!.dispatchEvent(new Event("click", { bubbles: true }));

    let opened = false;
    for (let i = 0; i < 40 && !opened; i++) {
      await flushUpdates();
      opened = dispatcher.get("team_dashboard_admin_save_confirm_modal", "open") === true;
    }
    assert(opened, "expected the confirm modal's open state to become true after the real Save click's dryRun dispatch settles");

    assertEquals(dispatchedBodies.length, 1, "expected exactly one dryRun dispatch from the real Save click");
    const dryRunPayload = dispatchedBodies[0].payload as Record<string, unknown>;
    assertEquals(dryRunPayload.target_ref, ADMIN_UPDATE_TARGET_REF);
    assertEquals(dryRunPayload.bodyMarkdown, editedMarkdown, "expected the real edited Textarea value in the dryRun payload");
    assertEquals(dryRunPayload.dryRun, "true");
    assert(!("confirmed" in dryRunPayload), "expected the dryRun payload to carry no confirmed flag");

    const dialogHtml = container.querySelector('[role="dialog"]')?.innerHTML ?? "";
    assert(dialogHtml.includes("Save team dashboard"), "expected the real confirm modal to show its seed-authored title");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the Admin DOM with the confirm modal open");

    // The modal opening re-ran renderEmission() (via the localStateStore-subscribed
    // renderTree()) with the SAME unchanged emission.data.bodyMarkdown — the live-typed edit
    // must still win as the Textarea's own displayed value across that re-render, not be reset
    // back to the emission-derived default (the exact precedence renderEmission.ts's second
    // applyLiveNodeValueOverride pass, added alongside resolvePropBindings' scalar-mirror fix,
    // protects — see projectionShellAdminRuntimeWritePayloadCapture.test.ts round 28 for the
    // sibling proof that a genuine settled-write canonical reread still resets it correctly).
    assertEquals(
      textareaEl!.value,
      editedMarkdown,
      "expected the live-typed edit to survive the re-render triggered by opening the confirm modal",
    );

    // 5. Real click on the real Confirm button: confirmed dispatch, modal closes.
    const confirmButton = container.querySelector(
      '[data-node-id="team_dashboard_admin_save_confirm_button"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(confirmButton, "expected a real Confirm button for team_dashboard_admin_save_confirm_button");
    confirmButton!.dispatchEvent(new Event("click", { bubbles: true }));

    let closed = false;
    for (let i = 0; i < 40 && !closed; i++) {
      await flushUpdates();
      closed = dispatcher.get("team_dashboard_admin_save_confirm_modal", "open") === false;
    }
    assert(closed, "expected the confirm modal to close once the real Confirm dispatch settles");
    assert(!container.querySelector('[role="dialog"]'), "expected the confirm modal to be gone from the DOM after closing");

    assertEquals(dispatchedBodies.length, 2, "expected exactly one new dispatch from the real Confirm click");
    const confirmPayload = dispatchedBodies[1].payload as Record<string, unknown>;
    assertEquals(confirmPayload.target_ref, ADMIN_UPDATE_TARGET_REF, "expected the real Confirm dispatch to carry the SAME team_dashboard:update target_ref");
    assertEquals(confirmPayload.bodyMarkdown, editedMarkdown, "expected the real edited value to survive into the confirmed dispatch");
    assertEquals(confirmPayload.confirmed, "true");
    assert(!("dryRun" in confirmPayload), "expected the confirmed dispatch to carry no dryRun flag");

    // 6. Reopen, then real Cancel click: closes the modal WITHOUT dispatching a third request.
    saveButton!.dispatchEvent(new Event("click", { bubbles: true }));
    opened = false;
    for (let i = 0; i < 40 && !opened; i++) {
      await flushUpdates();
      opened = dispatcher.get("team_dashboard_admin_save_confirm_modal", "open") === true;
    }
    assert(opened, "expected the confirm modal to reopen on a second real Save click");
    const dispatchedBeforeCancel = dispatchedBodies.length;

    const cancelButton = container.querySelector(
      '[data-node-id="team_dashboard_admin_save_cancel_button"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(cancelButton, "expected a real Cancel button for team_dashboard_admin_save_cancel_button");
    cancelButton!.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    assertEquals(
      dispatcher.get("team_dashboard_admin_save_confirm_modal", "open"),
      false,
      "expected a real Cancel click to close the confirm modal",
    );
    assertEquals(dispatchedBodies.length, dispatchedBeforeCancel, "expected Cancel to fire NO new /api/dispatch request");
  } finally {
    render(null, container as unknown as Element);
    cleanup();
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("production path: Normal /dashboard (team_dashboard.normal.projection, dd020) renders ONLY the real bodyMarkdown as read-only bare markdown — zero synthetic Saved View chrome and zero editor/save/mutation controls in the DOM", async () => {
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000dd023",
    layoutNodes: NORMAL_LAYOUT_NODES,
    packageId: "00000000-0000-0000-0000-0000000dd022",
    manifestId: "00000000-0000-0000-0000-0000000dd020",
    data: { bodyMarkdown: REAL_BODY_MARKDOWN },
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(NORMAL_LAYOUT_NODES), localStore);
  const tracker = createLiveNodeValueTracker();
  const { container, cleanup } = setupDom();

  try {
    const specs = renderEmission(emission, defaultComponentRegistry, {
      localStateStore: dispatcher,
      payloadFromNodeValues: tracker.snapshot(),
      onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
    });
    assertEquals(specs.filter((s) => s.componentType === "error"), [], "expected zero error specs composing the real dd020 tensor shape");
    render(h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }), container);
    await flushUpdates();

    // Same shared physical row's real content, rendered read-only.
    const viewerHtml = container.querySelector('[data-node-id="team_dashboard_normal_viewer"]')?.innerHTML ?? "";
    assert(viewerHtml.includes('class="md-viewer-bare-markdown-preview"'), "expected the real bare-markdown branch to render on the Normal axis too");
    assert(viewerHtml.includes("<h1>Team Dashboard</h1>"), "expected the SAME real bodyMarkdown heading as the Admin axis (shared physical row)");
    assert(viewerHtml.includes("Shared notes go here."), "expected the SAME real bodyMarkdown body text as the Admin axis (shared physical row)");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the Normal DOM");

    // Normal read-only boundary: no editor/save/mutation controls exist at all — not merely
    // hidden ones (surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_
    // contract's own seed_contract never includes those nodes).
    assert(!container.querySelector("textarea"), "expected zero <textarea> elements anywhere in the Normal DOM");
    assert(!container.querySelector("button"), "expected zero <button> elements anywhere in the Normal DOM (no Save/Cancel/Confirm control)");
    assert(!container.querySelector('[role="dialog"]'), "expected zero confirm-modal dialogs in the Normal DOM");
    assertEquals(
      specs.length,
      1,
      "expected exactly one rendered leaf on the Normal axis (the read-only viewer) — no editor/save/mutation nodes at all",
    );
  } finally {
    render(null, container as unknown as Element);
    cleanup();
  }
});

Deno.test("regression: UI-Builder canvas authoring preview (previewMode=true) still receives the synthetic Saved View placeholder for data_display/md_viewer, unchanged by this Bundle's production-only fix", () => {
  const previewNode: LayoutNode = {
    nodeId: "preview_md_viewer_node",
    nodeKind: "catalog_component",
    componentKey: "md_viewer.projection",
    componentId: "00000000-0000-0000-0001-000000000021",
    componentKind: "data_display/md_viewer",
    orderIndex: 0,
  };
  const emission: Emission = {
    layoutId: "layout-canvas-preview",
    layoutNodes: [previewNode],
  };
  const specs = renderEmission(emission, defaultComponentRegistry, { previewMode: true });
  assertEquals(specs.filter((s) => s.componentType === "error"), []);
  const props = specs[0].runtimeSpec!.props;
  const savedView = props.savedView as Record<string, unknown> | undefined;
  assert(savedView, "expected UI-Builder canvas preview (previewMode=true) to keep receiving the synthetic savedView placeholder");
  assertEquals(savedView.sourceTableRef, "preview.sample_table");
  assertEquals(savedView.sourceRecordRef, "preview_record_001");
  assertEquals(savedView.templateKey, "preview_template");
});
