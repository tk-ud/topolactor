/**
 * Production-composition boundary proof for the Team Dashboard Admin/Normal canonical
 * shared-dashboard surfaces (docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * surface_axes.admin.surfaces.team_dashboard / surface_axes.normal.surfaces.dashboard.
 * team_dashboard_canonical_shared_contract). The LayoutNode[] fixtures below are a checked-in
 * snapshot of the REAL Emission.LayoutNodes a live dispatch produces today, byte-identical to
 * frontend/tests/fixtures/team_dashboard_admin_composed_layout_nodes.json /
 * team_dashboard_normal_composed_layout_nodes.json, which backend/tests/
 * Topolactor.Integration.Tests/TeamDashboardHubRelationUiProjectionLiveDbTests.cs's own
 * DispatchAsync_{Admin,Normal}Manifest_ProjectionEntry_ComposedFromPhysicallyAdoptedSchema_
 * MatchesCheckedInFixtureSnapshot tests independently assert against a real live-DB dispatch —
 * the same paired-proof pattern (backend snapshot + frontend DOM consumption) manifest 092's own
 * frontend/tests/fixtures/manifest_0092_bare_entry_layout_nodes.json already established. If
 * team-dashboard's seed content ever drifts from this shape, the BACKEND test fails first, never
 * leaving this frontend fixture silently stale.
 *
 * UPDATED (team-dashboard-physical-layout-adoption round, implementation_change): Owner
 * "Judgment B" is now physically applied -- dd013 (admin.projection.layout) / dd023
 * (normal.projection.layout) persist the CLEAN react_schema_topology_seed_translator.py
 * layoutAdoptionCandidates output as components_layout_design.layout_schema_json.records[], the
 * PRIMARY structural authority (docs/design/react-schema-topology-seed-translator-ssot.yaml
 * storage_adoption_contract.structural_authority_precedence_contract); dd015/dd025's own tensor
 * rows are reshaped into the DERIVED runtime carrier that same contract requires (componentKey/
 * componentKind/label no longer live there -- LayoutSchemaTensorComposer.Compose resolves them
 * from the schema tree directly; runtimeInteractions are keyed by each Action/Modal's own
 * resolved schema parent -- the owning Section for team_dashboard_admin_save_button, the owning
 * Modal for its Confirm/Cancel children -- never a per-leaf tensor-authored identity anymore).
 * The composed LayoutNode[] below is therefore structurally different from the pre-adoption
 * tensor-only shape in two visible ways this round's own tests newly cover: (1) Category
 * ("team_dashboard"/"team_dashboard_normal") and Section ("team_dashboard_admin_editor"/
 * "team_dashboard_normal_viewer_section") now exist as real structural_node LayoutNode entries
 * and DO render a visible label (LayoutProjectionTree.tsx's own generic structural_node branch,
 * `<p class="structural-node-label">{label}</p>`) -- not a team-dashboard-specific behavior, the
 * same generic rendering every other schema-composed manifest (092, admin-enum) already uses for
 * its own Category/Section headers; (2) every leaf now carries a real ParentNodeId resolved from
 * the schema tree (Field/Action/Modal -> their owning Section; Confirm/Cancel -> their owning
 * Modal), where the pre-adoption tensor-only shape had every leaf at the DOM root except
 * Confirm/Cancel (Round 2's own tensor_container_parent_contract patch). The FINAL rendered DOM
 * shape for every assertion this file already made -- real bodyMarkdown, real Japanese labels, a
 * fully non-mutating read-only Normal axis, Confirm/Cancel absent while closed, contained inside
 * the open Modal's own subtree, machine vocabulary never visible -- is unchanged; only the
 * INTERNAL composition source changed, driven through the real renderEmission() ->
 * LayoutProjectionTree -> runtimeComponentFactory -> real DOM pipeline (happy-dom + Preact
 * render(), real native DOM events -- never renderToString or dispatcher.set() standing in for a
 * real interaction) exactly as before.
 *
 * Round 1 fix (production-projection-boundary Bundle): frontend/runtime/renderEmission.ts's
 * buildProductionCatalogComponentProps() had no explicit case for componentKind=
 * "data_display/md_viewer", so its default branch fell through to
 * buildLayoutPreviewPlaceholderProps() (frontend/runtime/layoutComponentPreview.ts) -- the SAME
 * synthetic UI-Builder canvas-preview savedView (preview.sample_table / preview_record_001 /
 * preview_template / preview.v1) authoring/canvas preview legitimately uses -- even in
 * PRODUCTION (non-preview) rendering, silently outranking a real propBindings.markdown value
 * under mdViewerPreviewFactory's documented (unchanged, correct) savedView>markdown priority.
 * Fixed generically by componentKind (never by team_dashboard/route/manifest/node-id) -- see the
 * last Deno.test below for the same-mechanism regression proof against UI-Builder canvas
 * authoring preview (previewMode=true), which must keep the synthetic placeholder unchanged.
 * Team Markdown Dashboard Saved View (frontend/tests/teamMarkdownSavedView.test.ts) renders
 * MdViewer directly as a Preact component, entirely outside this renderEmission/
 * runtimeComponentFactory catalog-component pathway, so it is structurally unaffected.
 *
 * Round 1 also fixed frontend/runtime/propBindingResolver.ts's resolvePropBindings() scalar
 * branch (form_input/search_input, form_input/textarea_template, data_display/md_viewer),
 * which set only the top-level props[propName], never mirroring into props.data[propName] the
 * way the array branch already did -- so the Admin Textarea's real propBindings.value binding
 * was silently shadowed by the stale "" placeholder default. renderEmission.ts additionally
 * re-applies applyLiveNodeValueOverride after resolvePropBindings so an in-progress edit still
 * wins over a same-render propBindings snapshot (preserving the existing settled-write-resets/
 * passive-refresh-preserves contract, projectionShellAdminRuntimeWritePayloadCapture.test.ts
 * round 28). Both proven below (Admin test, steps 2-3).
 *
 * Round 2 fix (this Bundle -- structural hierarchy + authored-label + display-language
 * boundary): a follow-up audit found TWO further generic gaps between the canonical
 * react_schema source and the tensor-only production projection, both fixed in
 * .agent/scripts/react_schema_topology_seed_translator.py (never in frontend runtime, and
 * never as a team_dashboard-specific branch):
 *
 * 1. tensor_container_parent_contract: the canonical react_schema authors Confirm/Cancel as
 *    Modal.children (see the [modal]...[/modal] DSL block), but split_flat_records_into_
 *    adoption_candidates' tensorAdoptionCandidates builder never emitted parentNodeId at all,
 *    so both Actions were flattened to permanent root-level siblings -- reachable in the DOM
 *    even while their owning Modal's own `open` was false, unlike manifest 092's own
 *    already-proven Confirm/Cancel-inside-Modal seed rows (frontend/tests/fixtures/
 *    manifest_0092_bare_entry_layout_nodes.json), which DO carry parentNodeId. Fixed by
 *    propagating parentNodeId generically whenever a tensor-adopted Action/WorkflowStep's own
 *    react_schema parent record_type is itself tensor-adopted as a container (today, only
 *    Modal) -- the SAME authoredChildren/footer-slot containment contract
 *    LayoutProjectionTree.tsx / runtimeComponentFactory.ts modalFactory already established for
 *    schema-composed layouts, now reachable from the tensor-only path too. Proven below (Admin
 *    test, steps 1 and 4): Confirm/Cancel are entirely absent from the DOM while the modal is
 *    closed, appear ONLY inside the modal's own subtree once it opens, and disappear again
 *    (unmount, not merely hide) once it closes.
 * 2. propsJson.data nesting: every Field/Action tensor_nodes.append() call site emitted a FLAT
 *    {"label": ...} propsJson, but frontend/runtime/renderEmission.ts's mergeNodeLocalProps
 *    shallow-merges propsJson over the TOP LEVEL of defaultProps, never into its nested `data`
 *    object -- so buttonFactory/textareaTemplateFactory's own `props.data.label` read never saw
 *    it, and every tensor-adopted button's visible label silently regressed to its shared
 *    componentKey string ("button.primitive") in production. Fixed by nesting under
 *    {"data": {"label": ...}} at generation time, matching what the factories actually read
 *    (the Modal's own propsJson already used this nesting correctly -- unchanged). Proven below
 *    (Admin test) via the real Japanese authored labels reaching the DOM.
 *
 * Round 2 also Japanese-ized Team Dashboard's user-facing authored content at its canonical
 * source (the same two translator input DSL files) -- machine identity (nodeId/componentKey/
 * manifest key/targetRef) is untouched throughout: "Rendered preview"->"プレビュー" (Field
 * label -- inert for md_viewer's own bare-markdown mode per that Field's own generation
 * comment, so intentionally not DOM-asserted here), "Markdown body"->"Markdown本文" (Textarea
 * label, DOM-asserted), "Save"->"保存" (both the preview and the in-modal Confirm button's own
 * label, DOM-asserted), "Cancel"->"キャンセル" (DOM-asserted), "Save confirmation dialog"->
 * "保存確認" (the Modal's own rendered title, DOM-asserted), and the Modal's body naturally
 * embeds "チームダッシュボード" (DOM-asserted). Category/Section labels ("Team Dashboard"/
 * "Team dashboard note") were translated at the source too for authoring consistency; AT THE TIME
 * of Round 2, this surface was still tensor-only (Category/Section never became tensor nodes at
 * all), so those labels stayed metadata-only and this file did not assert they reached the DOM.
 * SUPERSEDED by the team-dashboard-physical-layout-adoption round (see the UPDATED note above):
 * once dd013/dd023's schema tree became the PRIMARY structural authority, Category/Section compose
 * as real structural_node LayoutNode entries and DO render their (Japanese, source-translated)
 * labels in the DOM -- this file now asserts exactly that (the `structuralLabels` checks in both
 * the Admin and Normal Deno.test blocks below), the same generic structural_node rendering every
 * other schema-composed manifest already exhibits, not new team-dashboard-specific behavior.
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

// Real Japanese authored content, sourced from the canonical translator input DSL
// (team-dashboard-admin.input.json) and reachable through generate-react-schema ->
// generate-topology-seed with zero post-generation patches -- see this file's own header.
const JA = {
  categoryLabel: "チームダッシュボード",
  adminSectionLabel: "チームダッシュボード本文",
  normalSectionLabel: "チームダッシュボード本文",
  viewerLabel: "プレビュー",
  save: "保存",
  cancel: "キャンセル",
  confirmTitle: "保存確認",
  confirmBody: "編集したMarkdownをチームダッシュボードの共有本文として保存します。",
  markdownBodyLabel: "Markdown本文",
};

// Wiring identity fields (wiringId/wiringKey/wiringKind/targetSurface/targetRef) every
// catalog_component node in this layout inherits uniformly from its single ui_wiring_registry
// row (dd014) -- structural_node entries (Category/Section) never carry these at all, matching
// the real composed shape exactly (see the fixture referenced below).
const ADMIN_WIRING = {
  wiringId: "00000000-0000-0000-0000-0000000dd014",
  wiringKey: "team_dashboard.admin.projection.wiring",
  wiringKind: "admin_runtime",
  targetSurface: "manifest",
  targetRef: "team_dashboard.admin.projection",
};
const ADMIN_UPDATE_TARGET_REF =
  "manifest:00000000-0000-0000-0000-0000000dd010:team_dashboard:update";
const CONFIRM_MODAL_ID = "team_dashboard_admin_save_confirm_modal";
const ADMIN_SECTION_ID = "team_dashboard_admin_editor";

// Byte-identical to frontend/tests/fixtures/team_dashboard_admin_composed_layout_nodes.json --
// a checked-in snapshot of the REAL Emission.LayoutNodes a live dispatch against the physically
// adopted dd013 (PRIMARY schema) + dd015 (DERIVED tensor carrier) produces today, independently
// asserted by backend/tests/Topolactor.Integration.Tests/TeamDashboardHubRelationUiProjection
// LiveDbTests.cs's own DispatchAsync_AdminManifest_ProjectionEntry_ComposedFromPhysicallyAdopted
// Schema_MatchesCheckedInFixtureSnapshot test -- see this file's own header for the full
// before/after shape explanation.
const ADMIN_LAYOUT_NODES: LayoutNode[] = [
  {
    nodeId: "team_dashboard",
    nodeKind: "structural_node",
    orderIndex: 0,
    recordType: "topology_ui_category",
    label: JA.categoryLabel,
  },
  {
    nodeId: ADMIN_SECTION_ID,
    nodeKind: "structural_node",
    parentNodeId: "team_dashboard",
    orderIndex: 1,
    recordType: "topology_ui_section",
    label: JA.adminSectionLabel,
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_viewer",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-000000000021",
    parentNodeId: ADMIN_SECTION_ID,
    componentKind: "data_display/md_viewer",
    orderIndex: 2,
    propsJson: `{"data": {"label": "${JA.viewerLabel}"}}`,
    propBindings: { markdown: { source: "emission.data.bodyMarkdown" } },
    label: JA.viewerLabel,
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_body",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-00000000001f",
    parentNodeId: ADMIN_SECTION_ID,
    componentKind: "form_input/textarea_template",
    orderIndex: 3,
    propsJson: `{"data": {"label": "${JA.markdownBodyLabel}"}}`,
    propBindings: { value: { source: "emission.data.bodyMarkdown" } },
    label: JA.markdownBodyLabel,
  },
  {
    ...ADMIN_WIRING,
    nodeId: "team_dashboard_admin_save_button",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-000000000010",
    parentNodeId: ADMIN_SECTION_ID,
    componentKind: "action/button",
    orderIndex: 4,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "openModal",
        targetNodeId: CONFIRM_MODAL_ID,
        statePath: "open",
      },
    ],
    dispatchTargetRefByTrigger: { click: ADMIN_UPDATE_TARGET_REF },
    dispatchPayloadFromByTrigger: {
      click: { bodyMarkdown: "node:team_dashboard_admin_body.value", dryRun: "literal:true" },
    },
    label: JA.save,
  },
  {
    ...ADMIN_WIRING,
    // structural_authority_precedence_contract.interaction_ownership_and_addressing_contract:
    // ParentNodeId is now resolved from the PRIMARY schema tree (dd013), not a tensor-side
    // parentNodeId stamp -- reachable in the DOM only while team_dashboard_admin_save_confirm_
    // modal's own `open` is true (Modal.tsx returns null entirely when closed, taking its whole
    // subtree with it).
    parentNodeId: ADMIN_SECTION_ID,
    nodeId: CONFIRM_MODAL_ID,
    nodeKind: "catalog_component",
    componentId: CONFIRM_MODAL_ID,
    componentKind: "disclosure/modal",
    orderIndex: 5,
    runtimeInteractions: [
      {
        trigger: "toggle",
        actionType: "closeModal",
        targetNodeId: CONFIRM_MODAL_ID,
        statePath: "open",
      },
    ],
    propsJson: `{"data": {"open": false, "title": "${JA.confirmTitle}", "body": "${JA.confirmBody}"}}`,
    label: JA.confirmTitle,
  },
  {
    ...ADMIN_WIRING,
    parentNodeId: CONFIRM_MODAL_ID,
    nodeId: "team_dashboard_admin_save_confirm_button",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-000000000010",
    componentKind: "action/button",
    orderIndex: 6,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: CONFIRM_MODAL_ID,
        statePath: "open",
      },
    ],
    dispatchTargetRefByTrigger: { click: ADMIN_UPDATE_TARGET_REF },
    dispatchPayloadFromByTrigger: {
      click: { bodyMarkdown: "node:team_dashboard_admin_body.value", confirmed: "literal:true" },
    },
    label: JA.save,
  },
  {
    ...ADMIN_WIRING,
    parentNodeId: CONFIRM_MODAL_ID,
    nodeId: "team_dashboard_admin_save_cancel_button",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-000000000010",
    componentKind: "action/button",
    orderIndex: 7,
    runtimeInteractions: [
      {
        trigger: "click",
        actionType: "closeModal",
        targetNodeId: CONFIRM_MODAL_ID,
        statePath: "open",
      },
    ],
    label: JA.cancel,
  },
];

const NORMAL_SECTION_ID = "team_dashboard_normal_viewer_section";

// Byte-identical to frontend/tests/fixtures/team_dashboard_normal_composed_layout_nodes.json --
// same checked-in-snapshot pattern as ADMIN_LAYOUT_NODES above, this axis's own
// DispatchAsync_NormalManifest_ProjectionEntry_ComposedFromPhysicallyAdoptedSchema_
// MatchesCheckedInFixtureSnapshot test. Category > Section > read-only viewer only -- no editor/
// save/mutation nodes at all (not merely hidden ones).
const NORMAL_LAYOUT_NODES: LayoutNode[] = [
  {
    nodeId: "team_dashboard_normal",
    nodeKind: "structural_node",
    orderIndex: 0,
    recordType: "topology_ui_category",
    label: JA.categoryLabel,
  },
  {
    nodeId: NORMAL_SECTION_ID,
    nodeKind: "structural_node",
    parentNodeId: "team_dashboard_normal",
    orderIndex: 1,
    recordType: "topology_ui_section",
    label: JA.normalSectionLabel,
  },
  {
    nodeId: "team_dashboard_normal_viewer",
    nodeKind: "catalog_component",
    componentId: "00000000-0000-0000-0001-000000000021",
    parentNodeId: NORMAL_SECTION_ID,
    componentKind: "data_display/md_viewer",
    wiringId: "00000000-0000-0000-0000-0000000dd024",
    wiringKey: "team_dashboard.normal.projection.wiring",
    wiringKind: "admin_runtime",
    targetSurface: "manifest",
    targetRef: "team_dashboard.normal.projection",
    orderIndex: 2,
    propsJson: `{"data": {"label": "${JA.viewerLabel}"}}`,
    propBindings: { markdown: { source: "emission.data.bodyMarkdown" } },
    label: JA.viewerLabel,
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

// Machine vocabulary that must never leak into a rendered visible label -- componentKey is the
// exact regressed value round 2's propsJson.data nesting fix closes (buttonFactory falling back
// to it when props.data.label was never actually reachable).
const MACHINE_VOCABULARY_MARKERS = [
  "button.primitive",
  "md_viewer.projection",
  "textarea.template",
  "modal.template",
  "team_dashboard.admin.projection",
  "team_dashboard.normal.projection",
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

function assertNoMachineVocabularyAsVisibleText(html: string, context: string): void {
  for (const marker of MACHINE_VOCABULARY_MARKERS) {
    assert(
      !html.includes(marker),
      `expected no machine vocabulary ("${marker}") exposed as visible text in ${context}, got: ${html}`,
    );
  }
}

Deno.test("production path: Admin /admin/team-dashboard (team_dashboard.admin.projection, dd010) renders real Japanese authored content + real bodyMarkdown + editable Textarea, keeps Confirm/Cancel structurally contained inside the closed/open Modal (never a permanent root-level sibling), and completes a full dryRun-preview -> explicit confirm -> confirmed team_dashboard:update dispatch chain (real DOM, real native events)", async () => {
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
        "expected zero error specs composing the real, schema-composed dd013/dd015 shape",
      );
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }
    localStore.subscribe(renderTree);
    renderTree();
    await flushUpdates();

    // 1. Real bodyMarkdown reaches the viewer as bare markdown (round 1 fix), and the Modal is
    // closed by default -- WITH its Confirm/Cancel children entirely absent from the DOM (round
    // 2 fix: tensor_container_parent_contract), not merely an invisible/disabled Modal shell
    // with orphaned root-level action buttons sitting beside it.
    const viewerHtml = container.querySelector('[data-node-id="team_dashboard_admin_viewer"]')?.innerHTML ?? "";
    assert(
      viewerHtml.includes('class="md-viewer-bare-markdown-preview"'),
      "expected the real bare-markdown mdViewerPreviewFactory branch to render (props.markdown, not a synthetic savedView)",
    );
    assert(viewerHtml.includes("<h1>Team Dashboard</h1>"), "expected the real bodyMarkdown heading in the viewer");
    assert(viewerHtml.includes("Shared notes go here."), "expected the real bodyMarkdown body text in the viewer");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the initial Admin DOM");
    assert(!container.querySelector('[role="dialog"]'), "expected no confirm modal in the DOM before the first Save click");
    assert(
      !container.querySelector('[data-node-id="team_dashboard_admin_save_confirm_button"]'),
      "expected the Confirm button to be entirely absent from the DOM while its owning Modal is closed",
    );
    assert(
      !container.querySelector('[data-node-id="team_dashboard_admin_save_cancel_button"]'),
      "expected the Cancel button to be entirely absent from the DOM while its owning Modal is closed",
    );

    // Physical schema adoption round: Category/Section now compose as real structural_node
    // LayoutNodes and DO render a visible label -- the same generic structural_node rendering
    // every other schema-composed manifest already exhibits, not new team-dashboard-specific UI.
    const structuralLabels = Array.from(container.querySelectorAll(".structural-node-label")).map((el) => el.textContent);
    assertEquals(
      structuralLabels,
      [JA.categoryLabel, JA.adminSectionLabel],
      "expected the real schema-authored Category/Section labels to render as the generic structural_node group headers",
    );

    // 2. Real bodyMarkdown reaches the Textarea's own initial value (round 1 fix:
    // resolvePropBindings' scalar branch now mirrors into props.data like the array branch), and
    // its real seed-authored Japanese label reaches the DOM (round 2 fix: propsJson.data
    // nesting).
    const textareaWrapper = container.querySelector('[data-node-id="team_dashboard_admin_body"]');
    assert(
      textareaWrapper?.querySelector("label")?.textContent === JA.markdownBodyLabel,
      `expected the Textarea's own real authored Japanese label ("${JA.markdownBodyLabel}")`,
    );
    const textareaEl = textareaWrapper?.querySelector(
      "textarea",
    ) as unknown as { value: string; dispatchEvent: (e: Event) => boolean } | null;
    assert(textareaEl, "expected a real <textarea> for team_dashboard_admin_body");
    assertEquals(textareaEl!.value, REAL_BODY_MARKDOWN, "expected the real bodyMarkdown as the Textarea's initial value");

    // 3. Real native "input" edit on the real <textarea> — never tracker.set() standing in for it.
    const editedMarkdown = "# Team Dashboard\n\nUpdated via a real DOM edit.";
    textareaEl!.value = editedMarkdown;
    textareaEl!.dispatchEvent(new Event("input", { bubbles: true }));
    await flushUpdates();
    assertEquals(tracker.snapshot()["team_dashboard_admin_body"], editedMarkdown);

    // 4. Real click on the real Save button (its own real Japanese label, round 2 fix): dryRun
    // preview dispatch + opens the confirm modal, mounting Confirm/Cancel ONLY inside it.
    const saveButtonEl = container.querySelector('[data-node-id="team_dashboard_admin_save_button"] button');
    assertEquals(saveButtonEl?.textContent, JA.save, "expected the Save button's own real authored Japanese label");
    const saveButton = saveButtonEl as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(saveButton, "expected a real Save button for team_dashboard_admin_save_button");
    saveButton!.dispatchEvent(new Event("click", { bubbles: true }));

    let opened = false;
    for (let i = 0; i < 40 && !opened; i++) {
      await flushUpdates();
      opened = dispatcher.get(CONFIRM_MODAL_ID, "open") === true;
    }
    assert(opened, "expected the confirm modal's open state to become true after the real Save click's dryRun dispatch settles");

    assertEquals(dispatchedBodies.length, 1, "expected exactly one dryRun dispatch from the real Save click");
    const dryRunPayload = dispatchedBodies[0].payload as Record<string, unknown>;
    assertEquals(dryRunPayload.target_ref, ADMIN_UPDATE_TARGET_REF);
    assertEquals(dryRunPayload.bodyMarkdown, editedMarkdown, "expected the real edited Textarea value in the dryRun payload");
    assertEquals(dryRunPayload.dryRun, "true");
    assert(!("confirmed" in dryRunPayload), "expected the dryRun payload to carry no confirmed flag");

    const dialogEl = container.querySelector('[role="dialog"]');
    const dialogHtml = dialogEl?.innerHTML ?? "";
    assert(dialogHtml.includes(JA.confirmTitle), `expected the real confirm modal to show its seed-authored Japanese title ("${JA.confirmTitle}")`);
    assert(dialogHtml.includes(JA.confirmBody), "expected the real confirm modal to show its seed-authored Japanese body, naturally embedding \"チームダッシュボード\"");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the Admin DOM with the confirm modal open");
    assertNoMachineVocabularyAsVisibleText(container.innerHTML, "the Admin DOM with the confirm modal open");

    // Confirm/Cancel now exist ONLY inside the Modal's own subtree — never as a sibling
    // elsewhere in the document (tensor_container_parent_contract's real containment guarantee,
    // not merely "a node with this id exists somewhere").
    assert(dialogEl, "expected a real open dialog");
    const confirmInDialog = dialogEl!.querySelector('[data-node-id="team_dashboard_admin_save_confirm_button"] button');
    const cancelInDialog = dialogEl!.querySelector('[data-node-id="team_dashboard_admin_save_cancel_button"] button');
    assert(confirmInDialog, "expected the Confirm button to be mounted INSIDE the open dialog's own subtree");
    assert(cancelInDialog, "expected the Cancel button to be mounted INSIDE the open dialog's own subtree");
    assertEquals(confirmInDialog!.textContent, JA.save, "expected the in-modal Confirm button's own real authored Japanese label");
    assertEquals(cancelInDialog!.textContent, JA.cancel, "expected the Cancel button's own real authored Japanese label");
    assertEquals(
      container.querySelectorAll('[data-node-id="team_dashboard_admin_save_confirm_button"]').length,
      1,
      "expected exactly one Confirm button in the whole document (inside the dialog, never also a root-level orphan)",
    );
    assertEquals(
      container.querySelectorAll('[data-node-id="team_dashboard_admin_save_cancel_button"]').length,
      1,
      "expected exactly one Cancel button in the whole document (inside the dialog, never also a root-level orphan)",
    );

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

    // 5. Real click on the real (in-modal) Confirm button: confirmed dispatch, modal closes,
    // and Confirm/Cancel unmount along with it (never left as orphaned root-level siblings).
    confirmInDialog!.dispatchEvent(new Event("click", { bubbles: true }));

    let closed = false;
    for (let i = 0; i < 40 && !closed; i++) {
      await flushUpdates();
      closed = dispatcher.get(CONFIRM_MODAL_ID, "open") === false;
    }
    assert(closed, "expected the confirm modal to close once the real Confirm dispatch settles");
    assert(!container.querySelector('[role="dialog"]'), "expected the confirm modal to be gone from the DOM after closing");
    assert(
      !container.querySelector('[data-node-id="team_dashboard_admin_save_confirm_button"]'),
      "expected the Confirm button to unmount (not merely hide) once its owning Modal closes",
    );
    assert(
      !container.querySelector('[data-node-id="team_dashboard_admin_save_cancel_button"]'),
      "expected the Cancel button to unmount (not merely hide) once its owning Modal closes",
    );

    assertEquals(dispatchedBodies.length, 2, "expected exactly one new dispatch from the real Confirm click");
    const confirmPayload = dispatchedBodies[1].payload as Record<string, unknown>;
    assertEquals(confirmPayload.target_ref, ADMIN_UPDATE_TARGET_REF, "expected the real Confirm dispatch to carry the SAME team_dashboard:update target_ref");
    assertEquals(confirmPayload.bodyMarkdown, editedMarkdown, "expected the real edited value to survive into the confirmed dispatch");
    assertEquals(confirmPayload.confirmed, "true");
    assert(!("dryRun" in confirmPayload), "expected the confirmed dispatch to carry no dryRun flag");

    // 6. Reopen, then real Cancel click (inside the reopened dialog): closes the modal WITHOUT
    // dispatching a third request, and Confirm/Cancel unmount again.
    saveButton!.dispatchEvent(new Event("click", { bubbles: true }));
    opened = false;
    for (let i = 0; i < 40 && !opened; i++) {
      await flushUpdates();
      opened = dispatcher.get(CONFIRM_MODAL_ID, "open") === true;
    }
    assert(opened, "expected the confirm modal to reopen on a second real Save click");
    const dispatchedBeforeCancel = dispatchedBodies.length;

    const reopenedDialog = container.querySelector('[role="dialog"]');
    const cancelButton = reopenedDialog?.querySelector(
      '[data-node-id="team_dashboard_admin_save_cancel_button"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(cancelButton, "expected a real Cancel button inside the reopened dialog");
    cancelButton!.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    assertEquals(
      dispatcher.get(CONFIRM_MODAL_ID, "open"),
      false,
      "expected a real Cancel click to close the confirm modal",
    );
    assertEquals(dispatchedBodies.length, dispatchedBeforeCancel, "expected Cancel to fire NO new /api/dispatch request");
    assert(
      !container.querySelector('[data-node-id="team_dashboard_admin_save_cancel_button"]'),
      "expected the Cancel button to unmount once Cancel closes its own owning Modal",
    );
  } finally {
    render(null, container as unknown as Element);
    cleanup();
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

Deno.test("production path: Normal /dashboard (team_dashboard.normal.projection, dd020) renders ONLY the real bodyMarkdown as read-only bare markdown — zero synthetic Saved View chrome, zero machine vocabulary as visible text, and zero editor/save/mutation controls in the DOM", async () => {
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
    assertEquals(specs.filter((s) => s.componentType === "error"), [], "expected zero error specs composing the real, schema-composed dd023/dd025 shape");
    render(h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }), container);
    await flushUpdates();

    // Same shared physical row's real content, rendered read-only, with real Japanese
    // human-facing content only.
    const viewerHtml = container.querySelector('[data-node-id="team_dashboard_normal_viewer"]')?.innerHTML ?? "";
    assert(viewerHtml.includes('class="md-viewer-bare-markdown-preview"'), "expected the real bare-markdown branch to render on the Normal axis too");
    assert(viewerHtml.includes("<h1>Team Dashboard</h1>"), "expected the SAME real bodyMarkdown heading as the Admin axis (shared physical row)");
    assert(viewerHtml.includes("Shared notes go here."), "expected the SAME real bodyMarkdown body text as the Admin axis (shared physical row)");
    assertNoSyntheticSavedViewChrome(container.innerHTML, "the Normal DOM");
    assertNoMachineVocabularyAsVisibleText(container.innerHTML, "the Normal DOM");

    // Physical schema adoption round: Category/Section now compose as real structural_node
    // LayoutNodes and DO render a visible label (LayoutProjectionTree.tsx's generic
    // structural_node branch) -- the same generic behavior every other schema-composed manifest
    // already exhibits for its own Category/Section headers, not new team-dashboard-specific UI.
    const structuralLabels = Array.from(container.querySelectorAll(".structural-node-label")).map((el) => el.textContent);
    assertEquals(
      structuralLabels,
      [JA.categoryLabel, JA.normalSectionLabel],
      "expected the real schema-authored Category/Section labels to render as the generic structural_node group headers",
    );

    // Normal read-only boundary: no editor/save/mutation controls exist at all — not merely
    // hidden ones (surface_axes.normal.surfaces.dashboard.team_dashboard_canonical_shared_
    // contract's own seed_contract never includes those nodes).
    assert(!container.querySelector("textarea"), "expected zero <textarea> elements anywhere in the Normal DOM");
    assert(!container.querySelector("button"), "expected zero <button> elements anywhere in the Normal DOM (no Save/Cancel/Confirm control)");
    assert(!container.querySelector('[role="dialog"]'), "expected zero confirm-modal dialogs in the Normal DOM");
    assertEquals(
      specs.length,
      3,
      "expected exactly three composed nodes on the Normal axis (Category, Section, the read-only viewer) — no editor/save/mutation nodes at all",
    );
  } finally {
    render(null, container as unknown as Element);
    cleanup();
  }
});

Deno.test("regression: UI-Builder canvas authoring preview (previewMode=true) still receives the synthetic Saved View placeholder for data_display/md_viewer, unchanged by this Bundle's production-only fixes", () => {
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
