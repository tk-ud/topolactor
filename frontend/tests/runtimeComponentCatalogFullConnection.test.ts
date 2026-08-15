/**
 * runtime_component_catalog_full_connection_bundle
 * Bidirectional integrity: catalog runtimeConnected ↔ RUNTIME_COMPONENT_FACTORIES
 *
 * Completion conditions:
 * - Every catalog entry with runtimeConnected:true has a matching factory
 * - Every factory kind maps to a catalog entry with runtimeConnected:true
 * - No registrationRequired:true entry is runtimeConnected:false (would fail on palette placement)
 * - No dashboard_placement_candidate entry is runtimeConnected:false (would fail when placed)
 * - All newly connected components render a preview vnode without error
 */
import {
  assertEquals,
  assertExists,
  assert,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { COMPONENT_CATALOG_ENTRIES, isRuntimeReachable } from "../components/catalog.ts";
import {
  RUNTIME_COMPONENT_FACTORIES,
} from "../runtime/runtimeComponentFactory.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
  listRuntimeComponentKinds,
} from "../runtime/runtimeComponentRegistry.ts";
import { renderLayoutComponentPreview } from "../runtime/layoutComponentPreview.ts";

Deno.test("catalog/factory bidirectional: all runtimeConnected:true entries have a factory", () => {
  ensureRuntimeComponentRegistryInitialized();
  const connectedEntries = COMPONENT_CATALOG_ENTRIES.filter((e) => e.runtimeConnected);
  for (const entry of connectedEntries) {
    assert(
      hasRuntimeComponentFactory(entry.componentKind),
      `runtimeConnected:true but no factory for: ${entry.componentKey} (${entry.componentKind})`,
    );
  }
});

Deno.test("catalog/factory bidirectional: all factory kinds have a catalog entry with runtimeConnected:true", () => {
  const connectedKinds = new Set(
    COMPONENT_CATALOG_ENTRIES.filter((e) => e.runtimeConnected).map((e) => e.componentKind),
  );
  for (const factory of RUNTIME_COMPONENT_FACTORIES) {
    for (const kind of factory.componentKinds) {
      assert(
        connectedKinds.has(kind),
        `factory kind not in catalog as runtimeConnected:true: ${kind}`,
      );
    }
  }
});

Deno.test("catalog invariant: no registrationRequired:true entry is runtimeConnected:false", () => {
  const violations = COMPONENT_CATALOG_ENTRIES.filter(
    (e) => e.registrationRequired && !e.runtimeConnected,
  );
  assertEquals(
    violations.map((e) => `${e.componentKey}(${e.componentKind})`),
    [],
    "registrationRequired:true + runtimeConnected:false means palette placement will fail with FACTORY_MISSING",
  );
});

Deno.test("catalog invariant: no dashboard_placement_candidate is runtimeConnected:false", () => {
  const violations = COMPONENT_CATALOG_ENTRIES.filter(
    (e) => e.capabilityTags.includes("dashboard_placement_candidate") && !e.runtimeConnected,
  );
  assertEquals(
    violations.map((e) => `${e.componentKey}(${e.componentKind})`),
    [],
    "dashboard_placement_candidate + runtimeConnected:false means DashboardCandidatePalette placement will fail with FACTORY_MISSING",
  );
});

// ─── Preview render tests for newly connected components ────────────────────

const NEW_CONNECTED_KINDS: Array<[string, string]> = [
  ["select.template", "form_input/select"],
  ["checkbox.template", "form_input/checkbox"],
  ["badge.template", "display/badge"],
  ["status_badge.template", "display/status_badge"],
  ["alert.template", "display/alert"],
  ["loading_state.template", "feedback/loading"],
  ["empty_state.template", "feedback/empty"],
  ["error_state.template", "feedback/error"],
  ["json_viewer.template", "data_display/json"],
  ["admin_page_shell.template", "shell/admin_page"],
  ["admin_section.template", "shell/admin_section"],
  ["validation_result_panel.template", "validation/result"],
  ["textarea.template", "form_input/textarea_template"],
  ["tabs.template", "disclosure/tabs"],
  ["tree.template", "data_display/tree"],
  ["md_viewer.projection", "data_display/md_viewer"],
];

for (const [componentKey, componentKind] of NEW_CONNECTED_KINDS) {
  Deno.test(`renderLayoutComponentPreview: ${componentKey} renders without error`, () => {
    ensureRuntimeComponentRegistryInitialized();
    const result = renderLayoutComponentPreview({ componentKey, componentKind });
    assert(result.ok, `${componentKey} preview failed: ${!result.ok ? `${result.code}: ${result.reason}` : ""}`);
    if (result.ok) assertExists(result.node);
  });
}

Deno.test("tree_node.template: registrationRequired:false and runtimeConnected:false (sub-component, not standalone placement)", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "tree_node.template");
  assertExists(entry, "tree_node.template must exist in catalog");
  assertEquals(entry!.registrationRequired, false, "tree_node must not be in placement palette");
  assertEquals(entry!.runtimeConnected, false, "tree_node is a sub-component, not standalone runtime");
});

Deno.test("authoring/md_translation: registrationRequired:false and runtimeConnected:false (admin authoring surface, not canvas component)", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_translation_authoring_surface.authoring");
  assertExists(entry, "md_translation authoring entry must exist in catalog");
  assertEquals(entry!.registrationRequired, false, "md_translation must not appear in placement palette");
  assertEquals(entry!.runtimeConnected, false, "md_translation is admin-only authoring surface, not a canvas component");
});

// ─── Route-composition reachability: a second physically-observable reachability fact ──────────
// runtimeConnected strictly means "factory/constructor reachable via RUNTIME_COMPONENT_FACTORIES"
// and that meaning is NOT changed here. A component can also be physically mounted at runtime
// because a Fresh route (or a parent component already reachable from one) mounts it directly —
// that mount claim is verified below against the actual file contents, not taken on the strength of
// catalog `notes` prose. 2026-07-15 gate0 audit: this section verifies a physical-mount fact only.
// It is NOT a Gate 0 exception mechanism, NOT seed-readiness evidence, and passing these tests is
// NOT completion/registration evidence for any surface — every existing_route_composition entry
// remains an open Gate 0 question (hardcode-allowed vs. seed-required) judged per-entry against
// docs/design/runtime-orchestration-ssot.yaml, independent of whether this test suite is green.

// componentKey -> the exported identifier that must appear in the file at routeCompositionFile.
const EXISTING_ROUTE_COMPOSITION_MOUNT_IDENTIFIERS: Record<string, string> = {
  "md_translation_authoring_surface.authoring": "TeamMarkdownAuthoring",
  "saved_view_adjustment_authoring.authoring": "SavedViewAdjustmentAuthoringPanel",
  "credential_management.admin_operation": "AdminUsersRoster",
  "scheduler_job_settings.admin_operation": "SchedulerJobSettingsPanel",
  "hub_navigation_admin.admin_operation": "HubNavigationAdmin",
};

Deno.test("catalog invariant: every runtimeConnected:false entry is either exempt or has a verified physical mount (runtimeReachability:existing_route_composition)", () => {
  // This does not mean a hardcoded route-composition mount is an acceptable end state — it only
  // catches entries that would otherwise be unreachable ghost catalog rows with no verifiable path
  // to runtime at all. Each existing_route_composition entry this catches remains a separate,
  // per-entry Gate 0 question (see docs/design/runtime-orchestration-ssot.yaml), not something this
  // invariant passing resolves.
  const EXEMPT_KEYS = new Set([
    "tree_node.template", // sub-component of tree.template, not a standalone placement
  ]);
  const violations = COMPONENT_CATALOG_ENTRIES.filter((e) =>
    !e.runtimeConnected &&
    !EXEMPT_KEYS.has(e.componentKey) &&
    e.lifecycleStatus !== "code_only_drift" && // catalog-only primitive lineup, not yet an executable slice
    e.runtimeReachability !== "existing_route_composition"
  );
  assertEquals(
    violations.map((e) => e.componentKey),
    [],
    "runtimeConnected:false entries must declare a verifiable reachability path (runtimeReachability:existing_route_composition with routeCompositionFile) or be exempt — otherwise this catalog entry is unreachable from any runtime path at all",
  );
});

for (const [componentKey, mountIdentifier] of Object.entries(EXISTING_ROUTE_COMPOSITION_MOUNT_IDENTIFIERS)) {
  Deno.test(`route-composition reachability: ${componentKey} is actually mounted in its claimed routeCompositionFile`, async () => {
    const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === componentKey);
    assertExists(entry, `catalog entry must exist: ${componentKey}`);
    assertEquals(entry!.runtimeReachability, "existing_route_composition");
    assertExists(entry!.routeCompositionFile, `routeCompositionFile must be set: ${componentKey}`);
    assert(isRuntimeReachable(entry!), `isRuntimeReachable must be true for ${componentKey}`);

    const stat = await Deno.stat(entry!.routeCompositionFile!).catch(() => null);
    assertExists(stat, `routeCompositionFile does not exist on disk: ${entry!.routeCompositionFile}`);

    const source = await Deno.readTextFile(entry!.routeCompositionFile!);
    assert(
      source.includes(mountIdentifier),
      `routeCompositionFile ${entry!.routeCompositionFile} does not reference ${mountIdentifier} for ${componentKey} — route-composition reachability claim is unverified`,
    );
  });
}
