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
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  parseVisualLayoutPatchJson,
} from "../runtime/visualLayoutUtils.ts";
import { parsePayloadFromSource } from "../runtime/payloadFromResolver.ts";

// ─── Seed file registry ───────────────────────────────────────────────────────
// Add new seed SQL paths here to include them in all 6 contract checks.

const SEED_FILES = [
  "db/hub_search_preset_seed.sql",
  "db/physical_search_crud_aggregate_preset_seed.sql",
  "db/physical_details_inline_editor_md_generator_preset_seed.sql",
  "db/aggregate_dashboard_preset_seed.sql",
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
  const sql = await Deno.readTextFile("db/migrations/aggregate_dashboard_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/migrations/aggregate_dashboard_preset_seed.sql");
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
  const sql = await Deno.readTextFile("db/migrations/physical_details_inline_editor_md_generator_preset_seed.sql");
  const snapshot = extractCompileSnapshot(sql, "db/migrations/physical_details_inline_editor_md_generator_preset_seed.sql");
  const historyNode = snapshot.layoutPatchJson.nodes.find((node) => node.nodeId === "details_history_list") as Record<string, unknown> | undefined;
  assert(historyNode, "details_history_list must exist");
  const bindings = historyNode.propBindings as Record<string, { source: string }> | undefined;
  assert(bindings?.entries?.source === "emission.data.history", "audit diff drawer entries bind emission.data.history");
  assert(
    !snapshot.unresolvedJson.some((item) => item.knownGapRef === "logs_diff_record_history_binding"),
    "logs_diff_record_history_binding must not remain in unresolved_json",
  );
});
