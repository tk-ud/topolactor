/**
 * presetSeedLineContract.test.ts
 *
 * SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml preset_seed_test_policy
 *
 * Generic gate applied to ALL registered preset seed SQL files.
 * Adding a new preset seed does not require a new per-seed test file — register
 * the new seed path in SEED_FILES and all 6 contracts apply automatically.
 *
 * Per-seed individual assertions are limited to regression pins for past SSOT
 * violations (hub:search wiring, query→keyword field — see mockPresetIntake.test.ts).
 *
 * Contracts (one test per seed per contract = 4 seeds × 6 contracts = 24 tests):
 *   1. layout_patch_json — parseVisualLayoutPatchJson-compatible; non-empty nodes; required fields
 *   2. active topology boundary — activeTopologyWrite === false in package_membership_candidate_json
 *   3. wiring candidates — valid status; banned targetRefs absent; pending+empty targetRef explicit
 *   4. payloadFrom — recognized patterns only; node:<nodeId>.value nodeId exists in layout
 *   5. propBindings — source starts with "emission.data"
 *   6. unresolved_json — each item has identifier, reason, and knownGapRef (silent drop prohibited)
 */

import {
  assert,
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  parseVisualLayoutPatchJson,
} from "../runtime/visualLayoutUtils.ts";
import { parsePayloadFromSource } from "../runtime/payloadFromResolver.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import type { Emission } from "../api/dispatch.ts";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import { createLiveNodeValueTracker } from "../runtime/liveNodeValueTracker.ts";

// ─── Seed file registry ───────────────────────────────────────────────────────
// Add new seed SQL paths here to include them in all 6 contract checks.

const SEED_FILES = [
  "db/hub_search_preset_seed.sql",
  "db/physical_search_crud_aggregate_preset_seed.sql",
  "db/physical_details_inline_editor_md_generator_preset_seed.sql",
  "db/aggregate_dashboard_preset_seed.sql",
  "db/file_attachment_crud_preset_seed.sql",
  "db/file_storage_export_job_preset_seed.sql",
  "db/export_sftp_transfer_preset_seed.sql",
];

// Regression pin: target refs that are specifically banned as observed past violations.
// BANNED_TARGET_REFS is intentionally a static regression pin, not a vocabulary list.

// Target refs that are never SSOT-authorized.
const BANNED_TARGET_REFS = new Set(["hub:search"]);

// Required fields on every layout node per the SSOT layout_patch_json shape.
const REQUIRED_NODE_FIELDS = [
  "nodeId",
  "nodeKind",
  "componentKey",
  "componentKind",
  "parentNodeId",
  "orderIndex",
] as const;

// ─── Runtime vocabulary derivation ───────────────────────────────────────────
// Derive authorized content_bundle: action refs from the registered switch in
// AdminRuntime.cs rather than maintaining a parallel hardcoded list here.
// The test validates seed wiring against the actual runtime surface.

let _cachedContentBundleRefs: Set<string> | null = null;

async function getAuthorizedContentBundleRefs(): Promise<Set<string>> {
  if (_cachedContentBundleRefs) return _cachedContentBundleRefs;
  const csSource = await Deno.readTextFile("backend/runtime/AdminRuntime.cs");
  const refs = new Set<string>();
  const re = /"(content_bundle:[^"]+)"/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(csSource)) !== null) refs.add(m[1]);
  assert(refs.size > 0, "AdminRuntime.cs must register at least one content_bundle: action");
  _cachedContentBundleRefs = refs;
  return refs;
}

// ─── Compile snapshot extraction ──────────────────────────────────────────────

type CompileSnapshot = {
  layoutPatchJson: { nodes: Record<string, unknown>[] };
  packageMembershipCandidateJson: Record<string, unknown>;
  wiringCandidateJson: Record<string, unknown>[];
  unresolvedJson: Record<string, unknown>[];
};

/**
 * Extracts the 5 compile snapshot JSONB fields from a seed SQL file.
 *
 * The compile snapshot INSERT is always the last INSERT in the file and contains
 * exactly 5 $$...$$::jsonb blocks in order: layout_patch_json,
 * package_membership_candidate_json, wiring_candidate_json, style_candidate_json,
 * unresolved_json. Slicing from the INSERT start isolates it from the preceding
 * wiring candidate binding_json blocks.
 */
function extractCompileSnapshot(sql: string, seedFile: string): CompileSnapshot {
  const sectionStart = sql.indexOf("INSERT INTO topology.mock_preset_compile_snapshot");
  assert(sectionStart !== -1, `${seedFile}: compile snapshot INSERT not found`);
  const section = sql.slice(sectionStart);

  const blocks: unknown[] = [];
  const re = /\$\$([\s\S]*?)\$\$::jsonb/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(section)) !== null) {
    blocks.push(JSON.parse(m[1].trim()));
  }

  assert(
    blocks.length >= 5,
    `${seedFile}: expected ≥5 jsonb blocks in compile snapshot section, got ${blocks.length}`,
  );

  return {
    layoutPatchJson: blocks[0] as CompileSnapshot["layoutPatchJson"],
    packageMembershipCandidateJson: blocks[1] as CompileSnapshot["packageMembershipCandidateJson"],
    wiringCandidateJson: blocks[2] as CompileSnapshot["wiringCandidateJson"],
    // blocks[3] = style_candidate_json (not validated here)
    unresolvedJson: blocks[4] as CompileSnapshot["unresolvedJson"],
  };
}

// ─── Contract 1: layout_patch_json structure ─────────────────────────────────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: layout_patch_json is parseVisualLayoutPatchJson-compatible with non-empty nodes`,
    async () => {
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);
      const layoutJson = JSON.stringify(snapshot.layoutPatchJson);

      const parsed = parseVisualLayoutPatchJson(layoutJson, []);
      assert(
        parsed.ok,
        `parseVisualLayoutPatchJson failed: ${!parsed.ok ? parsed.error : ""}`,
      );
      if (!parsed.ok) return;

      assert(parsed.value.nodes.length > 0, "layout_patch_json nodes must be non-empty");

      for (const node of parsed.value.nodes) {
        const n = node as unknown as Record<string, unknown>;
        for (const field of REQUIRED_NODE_FIELDS) {
          assert(field in n, `node "${n.nodeId}" missing required field "${field}"`);
        }
      }
    },
  );
}

// ─── Contract 2: active topology boundary ────────────────────────────────────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: activeTopologyWrite is false in package_membership_candidate_json`,
    async () => {
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);
      const pkg = snapshot.packageMembershipCandidateJson;

      assert(
        pkg.activeTopologyWrite === false || pkg.activeTopology === false,
        `activeTopologyWrite must be false, got: ${JSON.stringify(pkg)}`,
      );
    },
  );
}

// ─── Contract 3: wiring candidate status, refs, and pending gap visibility ────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: wiring candidates — valid status, no banned refs, pending gaps explicit`,
    async () => {
      // Derive allowed content_bundle: actions from AdminRuntime.cs (the runtime surface).
      const authorizedContentBundleRefs = await getAuthorizedContentBundleRefs();
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);

      const VALID_STATUSES = new Set(["pending", "confirmed", "rejected"]);

      for (const wc of snapshot.wiringCandidateJson) {
        const nodeId = wc.nodeId as string;
        const status = wc.status as string;
        const targetRef = (wc.targetRef ?? "") as string;
        const binding = (wc.binding ?? {}) as Record<string, unknown>;

        assert(VALID_STATUSES.has(status), `wc "${nodeId}": invalid status "${status}"`);

        for (const banned of BANNED_TARGET_REFS) {
          assert(targetRef !== banned, `wc "${nodeId}": banned targetRef "${banned}"`);
        }

        // pending + empty targetRef must have knownGapRef or an explanatory note
        if (status === "pending" && targetRef === "") {
          const hasExplicitGap =
            "knownGapRef" in wc ||
            "knownGapRef" in binding ||
            (typeof binding.note === "string" && binding.note.length > 0);
          assert(
            hasExplicitGap,
            `wc "${nodeId}": pending with empty targetRef but no knownGapRef or note`,
          );
        }

        // content_bundle: refs must be registered in AdminRuntime.cs
        if (targetRef.startsWith("content_bundle:")) {
          assert(
            authorizedContentBundleRefs.has(targetRef),
            `wc "${nodeId}": content_bundle action "${targetRef}" is not registered in AdminRuntime.cs`,
          );
        }

        // route: prefix is navigation — no backend dispatch check (allowed)
      }
    },
  );
}

// ─── Contract 4: payloadFrom recognized patterns and nodeId existence ─────────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: payloadFrom uses only recognized patterns; node: nodeIds exist in layout`,
    async () => {
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);

      const layoutNodeIds = new Set(
        snapshot.layoutPatchJson.nodes.map((n) => n.nodeId as string),
      );

      for (const wc of snapshot.wiringCandidateJson) {
        const binding = (wc.binding ?? {}) as Record<string, unknown>;
        const payloadFrom = binding.payloadFrom as Record<string, string> | undefined;
        if (!payloadFrom) continue;

        for (const [field, source] of Object.entries(payloadFrom)) {
          const parsed = parsePayloadFromSource(source);
          assert(
            parsed.kind !== "unresolved_ref",
            `wc "${wc.nodeId}" payloadFrom["${field}"] = "${source}" is unresolved_ref — not a recognized pattern`,
          );
          if (parsed.kind === "node_value") {
            assert(
              layoutNodeIds.has(parsed.nodeId),
              `wc "${wc.nodeId}" payloadFrom["${field}"] references nodeId "${parsed.nodeId}" not in layout nodes`,
            );
          }
        }
      }
    },
  );
}

// ─── Contract 5: propBindings source prefix ───────────────────────────────────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: propBindings sources start with "emission.data"`,
    async () => {
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);

      for (const node of snapshot.layoutPatchJson.nodes) {
        const n = node as Record<string, unknown>;
        const propBindings = n.propBindings as
          | Record<string, { source: string }>
          | undefined;
        if (!propBindings) continue;

        for (const [prop, binding] of Object.entries(propBindings)) {
          assert(
            typeof binding.source === "string" &&
              binding.source.startsWith("emission.data"),
            `node "${n.nodeId}" propBindings.${prop}.source "${binding.source}" must start with "emission.data"`,
          );
        }
      }
    },
  );
}

// ─── Contract 6: unresolved_json visibility ───────────────────────────────────

for (const seedFile of SEED_FILES) {
  Deno.test(
    `[preset-line] ${seedFile}: unresolved_json items have identifier, reason, and knownGapRef`,
    async () => {
      const sql = await Deno.readTextFile(seedFile);
      const snapshot = extractCompileSnapshot(sql, seedFile);

      for (const item of snapshot.unresolvedJson) {
        const u = item as Record<string, unknown>;

        assert(
          "nodeId" in u || "sourceObjectId" in u,
          `unresolved item must have nodeId or sourceObjectId: ${JSON.stringify(u)}`,
        );
        assert(
          typeof u.reason === "string" && u.reason.length > 0,
          `unresolved item must have a non-empty reason: ${JSON.stringify(u)}`,
        );
        assert(
          "knownGapRef" in u,
          `unresolved item must have knownGapRef (silent drop prohibited): ${JSON.stringify(u)}`,
        );
      }
    },
  );
}

Deno.test("[preset-line] aggregate_dashboard.v1 aggregation display nodes bind data without catalog gap", async () => {
  const sql = await Deno.readTextFile("db/aggregate_dashboard_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/aggregate_dashboard_preset_seed.sql");
  const byId = new Map(snapshot.layoutPatchJson.nodes.map((node) => [String(node.nodeId), node]));

  const table = byId.get("dashboard_aggregation_table") as Record<string, unknown> | undefined;
  const stats = byId.get("dashboard_stats_panel") as Record<string, unknown> | undefined;
  assert(table, "dashboard_aggregation_table must exist");
  assert(stats, "dashboard_stats_panel must exist");
  const tableBindings = table.propBindings as Record<string, { source: string }> | undefined;
  const statsBindings = stats.propBindings as Record<string, { source: string }> | undefined;
  assert(tableBindings?.data?.source === "emission.data.aggregationResults", "aggregation table binds emission.data.aggregationResults");
  assert(statsBindings?.data?.source === "emission.data", "hub stats panel binds full emission.data");
  assert(
    !snapshot.unresolvedJson.some((item) => item.knownGapRef === "aggregation_preview_table_prop_binding_capability"),
    "aggregation_preview_table_prop_binding_capability must not remain in unresolved_json",
  );
});

Deno.test("[preset-line] physical_details_inline_editor history binds emission.data.history without backend gap", async () => {
  const sql = await Deno.readTextFile("db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const historyNode = snapshot.layoutPatchJson.nodes.find((node) => node.nodeId === "details_history_list") as Record<string, unknown> | undefined;
  assert(historyNode, "details_history_list must exist");
  const bindings = historyNode.propBindings as Record<string, { source: string }> | undefined;
  assert(bindings?.entries?.source === "emission.data.history", "audit diff drawer entries bind emission.data.history");
  assert(
    !snapshot.unresolvedJson.some((item) => item.knownGapRef === "logs_diff_record_history_binding"),
    "logs_diff_record_history_binding must not remain in unresolved_json",
  );
});

Deno.test("[preset-line] physical_details_inline_editor_md_generator: Markdown authoring field + safe preview node survive the compiler pipeline intact", async () => {
  const sql = await Deno.readTextFile("db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const byId = new Map(snapshot.layoutPatchJson.nodes.map((node) => [String(node.nodeId), node]));

  // The preset's own role claims "Markdown saved view generation surface" — this proves the
  // layout_patch_json (the preset compiler's own output, not just descriptive SSOT/source text)
  // actually carries the corresponding nodes, not just the name/role claim.
  const markdownBody = byId.get("details_markdown_body") as Record<string, unknown> | undefined;
  assert(markdownBody, "details_markdown_body (Markdown authoring field) must exist in compiled layout_patch_json");
  assertEquals(
    markdownBody!.componentKey,
    "textarea.template",
    "Markdown authoring field reuses the existing generic textarea component — no bespoke editor component",
  );

  const markdownPreview = byId.get("details_markdown_preview") as Record<string, unknown> | undefined;
  assert(markdownPreview, "details_markdown_preview (safe Markdown preview) must exist in compiled layout_patch_json");
  assertEquals(
    markdownPreview!.componentKey,
    "md_viewer.projection",
    "Markdown preview reuses the existing md_viewer.projection component — no bespoke renderer component",
  );
  const previewBindings = markdownPreview!.propBindings as Record<string, { source: string }> | undefined;
  assert(
    previewBindings?.markdown?.source?.startsWith("emission.data"),
    "Markdown preview binds a live emission.data source, not a static/hardcoded string",
  );

  // Both new nodes are children of the same details_tabs the preset's other tabs already use —
  // confirms this is a genuine third tab, not a disconnected/orphaned node pair.
  assertEquals(markdownBody!.parentNodeId, "details_tabs");
  assertEquals(markdownPreview!.parentNodeId, "details_tabs");

  const tabsNode = byId.get("details_tabs") as Record<string, unknown> | undefined;
  const tabsPropsJson = JSON.parse(String(tabsNode?.propsJson ?? "{}")) as { tabs?: { key: string }[] };
  assert(
    tabsPropsJson.tabs?.some((t) => t.key === "markdown"),
    "details_tabs must declare the markdown tab, not just host orphaned child nodes",
  );
});

Deno.test("[preset-line] physical_details_inline_editor_md_generator: Markdown authoring value carrier (initial bind + save payloadFrom) is complete", async () => {
  const sql = await Deno.readTextFile("db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const byId = new Map(snapshot.layoutPatchJson.nodes.map((node) => [String(node.nodeId), node]));

  // Initial/read value: details_markdown_body must bind its displayed value from emission.data,
  // never a static/hardcoded initial value.
  const markdownBody = byId.get("details_markdown_body") as Record<string, unknown> | undefined;
  assert(markdownBody, "details_markdown_body must exist");
  const bodyBindings = markdownBody!.propBindings as Record<string, { source: string }> | undefined;
  assertEquals(
    bodyBindings?.value?.source,
    "emission.data.markdownBody",
    "details_markdown_body's displayed value must bind to a live emission.data source",
  );

  // Save payload: details_save_button's payloadFrom must carry the Markdown field's current
  // tracked value through the SAME generic node:<id>.value mechanism entityId already uses --
  // not a Markdown-specific carrier, not a duplicated/parallel write path.
  const saveWiring = snapshot.wiringCandidateJson.find((wc) => wc.nodeId === "details_save_button") as
    | { binding?: { payloadFrom?: Record<string, string> } }
    | undefined;
  assert(saveWiring, "details_save_button wiring candidate must exist");
  assertEquals(
    saveWiring!.binding?.payloadFrom?.markdownBody,
    "node:details_markdown_body.value",
    "details_save_button payloadFrom must carry details_markdown_body's live tracked value",
  );
  assertEquals(
    saveWiring!.binding?.payloadFrom?.entityId,
    "event.record.id",
    "the pre-existing entityId payloadFrom entry must be unchanged (additive fix, not a rewrite)",
  );
});

Deno.test("[preset-line] physical_details_inline_editor_md_generator: markdown_body_field_binding_generic_placeholder is a compile-snapshot unresolved authoring-carrier entry, not source-text-only", async () => {
  const sql = await Deno.readTextFile("db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/physical_details_inline_editor_md_generator_preset_seed.sql");

  const entry = snapshot.unresolvedJson.find((item) =>
    (item as Record<string, unknown>).knownGapRef === "markdown_body_field_binding_generic_placeholder"
  ) as Record<string, unknown> | undefined;
  assert(
    entry,
    "markdown_body_field_binding_generic_placeholder must propagate into compile_snapshot.unresolved_json " +
      "(the carrier UIBuilder/authors actually observe), not remain source_snapshot_json.knownGaps-only",
  );
  assert("nodeId" in entry! || "sourceObjectId" in entry!, "unresolved entry must identify a node");
  assert(
    typeof entry!.reason === "string" && entry!.reason.length > 0,
    "unresolved entry must have a non-empty reason",
  );
  // Resolution metadata per cross_preset_authoring_boundary.author_resolution_map.propBindings --
  // proves this is classified as author-selection-pending authoring work (a UIBuilder surface CAN
  // author and persist the value), not an unclassified/backend/catalog gap.
  assertEquals(entry!.authoringSurface, "Design Inspector propBindings tab");
  assertEquals(entry!.saveAction, "layout_patch:apply");
  assertEquals(entry!.persistedField, "topology.ui_topology_tensor.layout_patch_json.nodes[].propBindings");
});

// ─── Production-equivalent value-flow proof (not a node-existence check) ──────
//
// Proves the seed's OWN declared node/propBindings/payloadFrom content (read from the real SQL
// file, not a hand-typed duplicate) actually carries a live-edited Markdown value from a rendered
// Textarea's initial bind, through a change event, into a save dispatch's request payload — via
// the SAME generic mechanisms every other seeded write action uses (renderEmission propBindings
// resolution, liveNodeValueTracker, payloadFromResolver's node:<id>.value). The two seed nodes
// are reused as-is; only the wiringKind/dispatchTargetRefByTrigger fields a real post-apply
// tensor row would carry (absent from this preset's own draft compile_snapshot schema) are added
// here to simulate "this preset applied to a real route", per the actual admin_runtime dispatch
// convention (manifest:<uuid>:<layer>:<action>) every other seeded surface (team_markdown,
// enum_dictionary) already uses in production.

Deno.test("[preset-line] physical_details_inline_editor_md_generator: Textarea initial bind -> change -> save click carries the raw Markdown string into the dispatch payload (not HTML-converted)", async () => {
  ensureRuntimeComponentRegistryInitialized();
  schedulerTestOnly.resetCommandQueue();

  const sql = await Deno.readTextFile("db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/physical_details_inline_editor_md_generator_preset_seed.sql");
  const byId = new Map(snapshot.layoutPatchJson.nodes.map((node) => [String(node.nodeId), node]));
  const saveWiring = snapshot.wiringCandidateJson.find((wc) => wc.nodeId === "details_save_button") as
    | { binding?: { payloadFrom?: Record<string, string> } }
    | undefined;
  assertExists(saveWiring?.binding?.payloadFrom, "seed must declare details_save_button's payloadFrom");

  const manifestId = "00000000-0000-0000-0000-0000000ad900";
  const markdownBodyNode = byId.get("details_markdown_body") as Record<string, unknown>;
  const saveButtonNode = byId.get("details_save_button") as Record<string, unknown>;
  assertExists(markdownBodyNode, "details_markdown_body must exist in the real compiled seed");
  assertExists(saveButtonNode, "details_save_button must exist in the real compiled seed");

  const initialMarkdown = "# Original\n\nOriginal body.";
  const editedMarkdown = "# Edited\n\n<script>window.__pwned = true;</script> and more text.";

  // Simulates "this preset applied to a real admin_runtime-wired route" — wiringKind/
  // dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger are real-tensor-only fields this
  // preset's own draft compile_snapshot schema does not carry (see module doc comment above).
  // The payloadFrom VALUE is copied verbatim from the seed's own declared wiring candidate, not
  // hand-invented, so this test fails if the seed's payloadFrom content ever regresses.
  // componentId (distinct from nodeId) is only assigned once a preset's draft layout_patch_json
  // is actually applied to a real ui_topology_tensor row -- this preset's own draft compile_snapshot
  // schema never carries it (see extractCompileSnapshot's nodes, which only have nodeId/componentKey/
  // componentKind). Same "comp-<nodeId>-001" convention used by other seed-driven end-to-end tests
  // (see projectionShellAdminRuntimeWritePayloadCapture.test.ts buildConfirmModalLayoutNodes).
  const emission: Emission = {
    layoutId: "layout-physical-details-md-value-flow",
    layoutNodes: [
      {
        ...markdownBodyNode,
        componentId: "comp-details-markdown-body-001",
        wiringKind: "admin_runtime",
      },
      {
        ...saveButtonNode,
        componentId: "comp-details-save-button-001",
        wiringKind: "admin_runtime",
        targetSurface: "manifest",
        dispatchTargetRefByTrigger: {
          click: `manifest:${manifestId}:content_bundle:update_entity_draft`,
        },
        dispatchPayloadFromByTrigger: {
          click: saveWiring!.binding!.payloadFrom,
        },
      },
      // deno-lint-ignore no-explicit-any
    ] as any,
    data: { markdownBody: initialMarkdown },
  };

  const tracker = createLiveNodeValueTracker();
  const specs = renderEmission(emission, {}, {
    payloadFromNodeValues: tracker.snapshot(),
    onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
  });

  // 1. Initial bind: the rendered Textarea's resolved `value` prop must come from
  // emission.data.markdownBody (propBindings resolution), never a static/hardcoded default.
  const markdownBodySpec = specs.find((s) => s.runtimeSpec?.componentId === "comp-details-markdown-body-001");
  assertExists(markdownBodySpec?.runtimeSpec, "details_markdown_body runtimeSpec must exist");
  assertEquals(
    (markdownBodySpec!.runtimeSpec!.props as Record<string, unknown>).value,
    initialMarkdown,
    "Textarea's initial value must bind to emission.data.markdownBody",
  );

  // 2. Change event: typing into the Textarea updates the live node-value tracker via the SAME
  // generic Lane 3 mechanism every other seeded typed field uses — no Markdown-specific state.
  const changeResult = factoryTestOnly.emitBoundEvent(
    markdownBodySpec!.runtimeSpec!,
    "change",
    { value: editedMarkdown },
  );
  assertEquals(changeResult.ok, true, "change event on the Markdown Textarea must succeed");
  assertEquals(
    tracker.snapshot()["details_markdown_body"],
    editedMarkdown,
    "the live node-value tracker must hold the just-typed Markdown string verbatim",
  );

  // 3. Save click: capture the real dispatch request body and assert it carries the RAW Markdown
  // string (never converted to/through HTML — save authority is raw source, security/rendering
  // authority is the Viewer, and those two responsibilities must stay separate).
  let capturedBody: Record<string, unknown> | null = null;
  const originalFetch = globalThis.fetch;
  globalThis.fetch = ((_url: string, init?: RequestInit) => {
    capturedBody = JSON.parse(String(init?.body ?? "{}"));
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, errors: [] }), { status: 200 }),
    );
  }) as typeof fetch;
  try {
    const saveButtonSpec = specs.find((s) => s.runtimeSpec?.componentId === "comp-details-save-button-001");
    assertExists(saveButtonSpec?.runtimeSpec, "details_save_button runtimeSpec must exist");
    // The save button's own payloadFrom resolution reads the tracker via
    // spec.payloadFromNodeValues -- re-supply the CURRENT snapshot (same backing object
    // liveNodeValueTracker.snapshot() always returns) so fire-time resolution sees step 2's update.
    const saveResult = factoryTestOnly.emitBoundEvent(
      { ...saveButtonSpec!.runtimeSpec!, payloadFromNodeValues: tracker.snapshot() },
      "click",
      { record: { id: "test-record-1" } },
    );
    assertEquals(saveResult.ok, true, "click on the save button must succeed");

    for (let i = 0; i < 20 && capturedBody === null; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    assertExists(capturedBody, "the api_command_lane request body must have been captured");
    const payload = (capturedBody as Record<string, unknown>).payload as Record<string, unknown>;
    assertEquals(
      payload.markdownBody,
      editedMarkdown,
      "dispatch payload.markdownBody must be the raw, unmodified Markdown source string",
    );
    // The pre-existing entityId carrier must survive unchanged alongside the new field.
    assertEquals(payload.entityId, "test-record-1");
    assert(
      typeof payload.markdownBody === "string" &&
        payload.markdownBody.includes("<script>") &&
        !String(capturedBody).includes("&lt;script&gt;"),
      "the saved payload must carry the literal Markdown source (including any raw-looking text) " +
        "unescaped and unconverted -- HTML-escaping/sanitizing is the Viewer's responsibility, not save's",
    );
  } finally {
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});
