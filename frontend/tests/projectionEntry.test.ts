/**
 * projectionEntry.test.ts — production projection entry route/package/manifest
 * awareness proof.
 *
 * Acceptance (bundle ui-projection-surface-architecture-reinforcement):
 * the production projection entry must not be fixed to default/screen_list/Search —
 * arbitrary UI Builder applied topology is selectable via route target axes or
 * explicit manifest target_ref, with explicit package confirmation.
 * The default/screen_list/Search fixed path is asserted ONLY as the no-selection
 * fallback and is NOT treated as arbitrary-topology proof.
 */
import {
  assert,
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  adoptResolvedManifestIdentity,
  confirmProjectionEntryEmission,
  isDefaultProjectionEntry,
  parseProjectionEntrySelection,
  resolveHubNavigationLinks,
  resolveProjectionEntryAxes,
} from "../runtime/projectionEntry.ts";
import type { Emission, HubNavigationSequenceItem } from "../api/dispatch.ts";

const MANIFEST_ID = "aaaaaaaa-0000-0000-0000-000000000001";
const PACKAGE_ID = "bbbbbbbb-0000-0000-0000-000000000002";

// ── selection parsing ────────────────────────────────────────────────────────

Deno.test("projectionEntry: empty search yields default selection", () => {
  const parsed = parseProjectionEntrySelection("");
  assertEquals(parsed.ok, true);
  if (parsed.ok) {
    assertEquals(isDefaultProjectionEntry(parsed.selection), true);
  }
});

Deno.test("projectionEntry: ?route= selects an arbitrary route target", () => {
  const parsed = parseProjectionEntrySelection("?route=orders.list");
  assertEquals(parsed.ok, true);
  if (parsed.ok) {
    assertEquals(parsed.selection.routeTarget, "orders.list");
    assertEquals(isDefaultProjectionEntry(parsed.selection), false);
  }
});

Deno.test("projectionEntry: ?manifest= selects an explicit applied manifest", () => {
  const parsed = parseProjectionEntrySelection(`?manifest=${MANIFEST_ID}`);
  assertEquals(parsed.ok, true);
  if (parsed.ok) {
    assertEquals(parsed.selection.manifestId, MANIFEST_ID);
  }
});

Deno.test("projectionEntry: malformed ?manifest= fails close (no silent default fallback)", () => {
  const parsed = parseProjectionEntrySelection("?manifest=not-a-uuid");
  assertEquals(parsed.ok, false);
  if (!parsed.ok) {
    assert(parsed.error.includes("PROJECTION_ENTRY_MANIFEST_INVALID"));
  }
});

Deno.test("projectionEntry: malformed ?package= fails close", () => {
  const parsed = parseProjectionEntrySelection("?package=broken");
  assertEquals(parsed.ok, false);
  if (!parsed.ok) {
    assert(parsed.error.includes("PROJECTION_ENTRY_PACKAGE_INVALID"));
  }
});

// ── axes resolution ──────────────────────────────────────────────────────────

Deno.test("projectionEntry: route selection resolves to that target axis — NOT fixed to default", () => {
  const axes = resolveProjectionEntryAxes({ routeTarget: "orders.list" });
  assertEquals(axes.target, "orders.list");
  assertEquals(axes.layer, "screen_list");
  assertEquals(axes.action, "Search");
  assert(
    axes.target !== "default",
    "route-selected entry must not dispatch the default target",
  );
});

Deno.test("projectionEntry: manifest selection resolves to payload.target_ref (backend manifest resolution authority)", () => {
  const axes = resolveProjectionEntryAxes({ manifestId: MANIFEST_ID });
  assertEquals(
    axes.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
  );
});

Deno.test("projectionEntry: route + manifest combine (target axis + target_ref)", () => {
  const axes = resolveProjectionEntryAxes({
    routeTarget: "orders.list",
    manifestId: MANIFEST_ID,
  });
  assertEquals(axes.target, "orders.list");
  assertEquals(
    axes.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
  );
});

// ── manifest_key selection (generic route→Manifest identity resolution) ─────

Deno.test("projectionEntry: manifestKey selection resolves to a manifest_key-shaped payload.target_ref", () => {
  const axes = resolveProjectionEntryAxes({
    manifestKey: "team_dashboard.normal.projection",
  });
  assertEquals(
    axes.payload?.target_ref,
    "manifest_key:team_dashboard.normal.projection:projection_entry",
  );
});

Deno.test("projectionEntry: manifestId takes priority over manifestKey when both are somehow present", () => {
  const axes = resolveProjectionEntryAxes({
    manifestId: MANIFEST_ID,
    manifestKey: "team_dashboard.normal.projection",
  });
  assertEquals(
    axes.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
  );
});

Deno.test("projectionEntry: manifestKey alone is not a default entry", () => {
  assertEquals(
    isDefaultProjectionEntry({ manifestKey: "team_dashboard.admin.projection" }),
    false,
  );
});

Deno.test("projectionEntry: no selection keeps the default entry axes (fallback only, not arbitrary-topology proof)", () => {
  const axes = resolveProjectionEntryAxes({});
  assertEquals(axes.target, "default");
  assertEquals(axes.layer, "screen_list");
  assertEquals(axes.action, "Search");
  assertEquals(axes.payload, undefined);
});

// ── package confirmation ─────────────────────────────────────────────────────

Deno.test("projectionEntry: explicit package selection mismatch is an explicit error (no silent render)", () => {
  const confirmation = confirmProjectionEntryEmission(
    { packageId: PACKAGE_ID },
    { componentIds: [], packageId: "cccccccc-0000-0000-0000-000000000003" },
  );
  assertEquals(confirmation.ok, false);
  if (!confirmation.ok) {
    assert(confirmation.error.includes("PROJECTION_ENTRY_PACKAGE_MISMATCH"));
  }
});

Deno.test("projectionEntry: matching package selection confirms", () => {
  const confirmation = confirmProjectionEntryEmission(
    { packageId: PACKAGE_ID },
    { componentIds: [], packageId: PACKAGE_ID },
  );
  assertEquals(confirmation.ok, true);
});

Deno.test("projectionEntry: no package selection never blocks (backend package resolution stays authoritative)", () => {
  const confirmation = confirmProjectionEntryEmission(
    {},
    { componentIds: [], packageId: PACKAGE_ID },
  );
  assertEquals(confirmation.ok, true);
});

// ── explicit manifest confirmation (initial dispatch, before any identity is adopted) ───────

Deno.test("projectionEntry: explicit ?manifest= selection mismatch on the INITIAL dispatch is an explicit error, never rendered — backend target_ref resolution is never trusted unverified", () => {
  const confirmation = confirmProjectionEntryEmission(
    { manifestId: MANIFEST_ID },
    { componentIds: [], manifestId: "dddddddd-0000-0000-0000-000000000004" },
  );
  assertEquals(confirmation.ok, false);
  if (!confirmation.ok) {
    assert(confirmation.error.includes("PROJECTION_ENTRY_MANIFEST_MISMATCH"));
  }
});

Deno.test("projectionEntry: matching explicit ?manifest= selection confirms on the initial dispatch, with no adoptedManifestId option needed yet", () => {
  const confirmation = confirmProjectionEntryEmission(
    { manifestId: MANIFEST_ID },
    { componentIds: [], manifestId: MANIFEST_ID },
  );
  assertEquals(confirmation.ok, true);
});

Deno.test("projectionEntry: no explicit manifest selection never blocks on manifestId alone (bare-entry / route selection stays backend authority)", () => {
  const confirmation = confirmProjectionEntryEmission(
    {},
    { componentIds: [], manifestId: MANIFEST_ID },
  );
  assertEquals(confirmation.ok, true);
});

// ── ProjectionShell wiring (source-scan, same style as projectionAuthBoundary) ─

Deno.test("projection entry surface: ProjectionShell resolves initial axes from the entry selection, not a hardcoded default block", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("parseProjectionEntrySelection"),
    "ProjectionShell must parse the route/package/manifest entry selection",
  );
  assert(
    src.includes("resolveProjectionEntryAxes"),
    "ProjectionShell must resolve initial dispatch axes from the entry selection",
  );
  assert(
    src.includes("confirmProjectionEntryEmission"),
    "ProjectionShell must confirm explicit package selection against emission.packageId",
  );
  assert(
    !src.includes('target: "default"'),
    "ProjectionShell must not hardcode the default target axes inline — axes come from projectionEntry resolution",
  );
});

Deno.test("projection entry surface: SSE refresh merges identity into the entry payload (pinned target_ref survives)", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("...(storedAxes.payload ?? {})"),
    "SSE refresh must merge identity fields into the stored entry payload instead of replacing it",
  );
});

Deno.test("projection entry surface: ProjectionShell accepts a manifestKey prop as a default-entry fill-in, mirroring manifestId", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("manifestKey?: string"),
    "ProjectionShellProps must declare an optional manifestKey prop",
  );
  assert(
    src.includes("manifestKey") && src.includes("entrySelection"),
    "ProjectionShell must merge manifestKey into the default-entry selection the same way manifestId is merged",
  );
});

// ── PR577 follow-up: SSE refresh identity preservation ──────────────────────
// Blocking fix: SSE refresh must not silently retarget a route-selected entry
// via payload.manifest_id, must keep a manifest-pinned target_ref, and must
// re-confirm an explicit package selection after refresh (not only on the
// initial dispatch).

Deno.test("projection entry surface: route-selected entry keeps its target axis unconditionally across refresh (never overwritten by SSE manifest_id)", () => {
  const routeAxes = resolveProjectionEntryAxes({ routeTarget: "orders.list" });
  // Simulate the refresh-time axes rebuild: storedAxes carried forward as-is,
  // SSE manifest_id must never become the next axes.target.
  const simulatedSseManifestId = "cccccccc-0000-0000-0000-000000000009";
  const rebuiltAxes = { ...routeAxes };
  assertEquals(rebuiltAxes.target, "orders.list");
  assert(
    rebuiltAxes.target !== simulatedSseManifestId,
    "route-selected entry target must never become the SSE-reported manifest id",
  );
});

Deno.test("projection entry surface: manifest-pinned target_ref is independent of axes.target and survives regardless", () => {
  const pinnedAxes = resolveProjectionEntryAxes({ manifestId: MANIFEST_ID });
  assertEquals(pinnedAxes.target, "default");
  assertEquals(
    pinnedAxes.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
  );
  // Even if axes.target were left untouched (as ProjectionShell now guarantees),
  // backend manifest resolution takes the target_ref bypass path first — the
  // pinned identity does not depend on target at all.
});

Deno.test("projection entry surface: package confirmation applies to a refreshed emission the same way as the initial one", () => {
  const selection = { packageId: PACKAGE_ID };
  const initialEmission = { componentIds: [], packageId: PACKAGE_ID };
  const refreshedMismatchEmission = {
    componentIds: [],
    packageId: "dddddddd-0000-0000-0000-000000000004",
  };
  assertEquals(
    confirmProjectionEntryEmission(selection, initialEmission).ok,
    true,
  );
  const refreshResult = confirmProjectionEntryEmission(
    selection,
    refreshedMismatchEmission,
  );
  assertEquals(refreshResult.ok, false);
  if (!refreshResult.ok) {
    assert(refreshResult.error.includes("PROJECTION_ENTRY_PACKAGE_MISMATCH"));
  }
});

Deno.test("projection entry surface: ProjectionShell re-confirms package selection after SSE refresh, not only on initial dispatch", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  const occurrences = src.split("confirmProjectionEntryEmission(").length - 1;
  assert(
    occurrences >= 2,
    "confirmProjectionEntryEmission must be called for both the initial dispatch and the SSE refresh emission",
  );
  assert(
    src.includes("PROJECTION_ENTRY_MISMATCH_ON_REFRESH"),
    "a refresh-time package OR adopted-manifest-identity mismatch must be logged explicitly (explicit_error_log_retain_old_dom)",
  );
  assert(
    src.includes("adoptedManifestId"),
    "the refresh confirmation must also check the adopted manifest identity (adoptResolvedManifestIdentity), not only packageId",
  );
});

Deno.test("projection entry surface: SSE payload forwards manifest_id as identity context only, never as topology/layout judgment input", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("identityPayload.manifest_id = payload.manifest_id"),
    "manifest_id must be forwarded into the identity payload, not consumed as a routing/target decision",
  );
});

// ── credential-management hub relation / navigation bundle: NavigationSequence ─
// The backend now attaches emission.navigationSequence ("current hub relation" candidates)
// and emission.manifestId ("current topology phase") for any manifest dispatch, not only
// topology_transform_runtime. The frontend must monitor and connect these fields through to a
// render path (not treat backend emission attachment alone as render-reached) — see
// docs/design/db-schema.yaml manifest_hub_chain / hubs_hub_relations_sequence.

function navItem(
  partial: Partial<HubNavigationSequenceItem> & { relatedHubId: string },
): HubNavigationSequenceItem {
  return {
    hubRelationId: `${partial.relatedHubId}-relation`,
    topologyManifestId: "source-manifest",
    relatedHubLabel: partial.relatedHubId,
    sequencePosition: 1,
    ...partial,
  };
}

Deno.test("resolveHubNavigationLinks: empty/absent navigationSequence yields no links", () => {
  assertEquals(resolveHubNavigationLinks(undefined), []);
  assertEquals(resolveHubNavigationLinks([]), []);
});

Deno.test("resolveHubNavigationLinks: an item with targetManifestId becomes a resolvable ?manifest= link", () => {
  const links = resolveHubNavigationLinks([
    navItem({
      relatedHubId: "hub-a",
      relatedHubLabel: "Hub A",
      sequencePosition: 1,
      targetManifestId: MANIFEST_ID,
    }),
  ]);
  assertEquals(links.length, 1);
  const link = links[0];
  assertEquals(link.resolvable, true);
  if (link.resolvable) {
    assertEquals(link.href, `?manifest=${MANIFEST_ID}`);
    assertEquals(link.label, "Hub A");
  }
});

Deno.test("resolveHubNavigationLinks: an item with no targetManifestId (ambiguous or unregistered hub) is explicitly unresolvable, not silently dropped or guessed", () => {
  const links = resolveHubNavigationLinks([
    navItem({
      relatedHubId: "hub-b",
      relatedHubLabel: "Hub B",
      sequencePosition: 2,
      targetManifestId: null,
    }),
  ]);
  assertEquals(links.length, 1);
  assertEquals(links[0].resolvable, false);
  assertEquals(links[0].label, "Hub B");
});

Deno.test("resolveHubNavigationLinks: sorts by sequencePosition (manifest-scoped UI transition order, hubs.hub_relations.sequence_position)", () => {
  const links = resolveHubNavigationLinks([
    navItem({
      relatedHubId: "hub-c",
      relatedHubLabel: "C",
      sequencePosition: 3,
      targetManifestId: MANIFEST_ID,
    }),
    navItem({
      relatedHubId: "hub-a",
      relatedHubLabel: "A",
      sequencePosition: 1,
      targetManifestId: MANIFEST_ID,
    }),
    navItem({
      relatedHubId: "hub-b",
      relatedHubLabel: "B",
      sequencePosition: 2,
      targetManifestId: MANIFEST_ID,
    }),
  ]);
  assertEquals(links.map((l) => l.label), ["A", "B", "C"]);
});

Deno.test("hub navigation round-trip: a resolvable link's href feeds back through parseProjectionEntrySelection + resolveProjectionEntryAxes into the SAME target manifest's dispatch axes — navigationSequence connects to the next projection dispatch, not just a rendered label", () => {
  const targetManifestId = "cccccccc-1111-2222-3333-444444444444";
  const links = resolveHubNavigationLinks([
    navItem({
      relatedHubId: "hub-target",
      relatedHubLabel: "Target hub",
      sequencePosition: 1,
      targetManifestId,
    }),
  ]);
  const link = links[0];
  assertEquals(link.resolvable, true);
  if (!link.resolvable) return;

  // link.href is exactly what ProjectionShell renders as <a href={link.href}>; simulate the
  // browser navigation landing back on the entry mount with that search string.
  const parsed = parseProjectionEntrySelection(link.href);
  assertEquals(parsed.ok, true);
  if (!parsed.ok) return;
  assertEquals(parsed.selection.manifestId, targetManifestId);

  const axes = resolveProjectionEntryAxes(parsed.selection);
  assertEquals(
    axes.payload?.target_ref,
    `manifest:${targetManifestId}:projection_entry`,
    "the hub navigation link must resolve to a dispatch targeting the SAME manifest the backend resolved as TargetManifestId — not a different or default entry",
  );
});

Deno.test("hub navigation round-trip: an unresolvable link (no targetManifestId) has no href to feed into the next dispatch at all", () => {
  const links = resolveHubNavigationLinks([
    navItem({
      relatedHubId: "hub-ambiguous",
      relatedHubLabel: "Ambiguous hub",
      sequencePosition: 1,
      targetManifestId: null,
    }),
  ]);
  const link = links[0];
  assertEquals(link.resolvable, false);
  assertEquals("href" in link, false);
});

Deno.test("projection entry surface: ProjectionShell reads emission.navigationSequence / emission.manifestId and renders a hub navigation path (not silently ignored)", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("resolveHubNavigationLinks"),
    "ProjectionShell must resolve NavigationSequence into navigable links via projectionEntry.ts, not read the raw field ad hoc",
  );
  assert(
    src.includes("emission?.navigationSequence") ||
      src.includes("emission.navigationSequence"),
    "ProjectionShell must read emission.navigationSequence",
  );
  assert(
    src.includes("emission?.manifestId") || src.includes("emission.manifestId"),
    "ProjectionShell must surface emission.manifestId (current topology phase identity)",
  );
});

Deno.test("projection entry surface: unresolvable hub navigation links are not rendered as clickable navigation", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/ProjectionShell.tsx", import.meta.url),
  );
  assert(
    src.includes("data-hub-navigation-unresolvable"),
    "an unresolvable link (no targetManifestId) must render as a distinguishable non-navigable marker, not a normal link",
  );
  assert(
    src.includes("data-hub-navigation-resolvable"),
    "a resolvable link must be distinguishable from an unresolvable one",
  );
});

// ── adoptResolvedManifestIdentity (frontend current dispatch identity) ─────

Deno.test("adoptResolvedManifestIdentity: bare entry with no explicit manifest pins refresh axes to Emission.ManifestId — replaying default axes verbatim is not identity sync", () => {
  const initialAxes = resolveProjectionEntryAxes({});
  assertEquals(initialAxes.payload, undefined);

  const emission: Emission = { manifestId: MANIFEST_ID };
  const adopted = adoptResolvedManifestIdentity(initialAxes, emission);

  assertEquals(
    adopted.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
    "a bare entry's refresh axes must be pinned to the manifest the initial dispatch actually resolved",
  );
  assertEquals(
    adopted.target,
    initialAxes.target,
    "target/layer/action axes are unchanged by adoption",
  );
});

Deno.test("adoptResolvedManifestIdentity: explicit ?manifest= selection is left unchanged — adoption must not override an explicit user selection", () => {
  const selection = parseProjectionEntrySelection(`?manifest=${MANIFEST_ID}`);
  assert(selection.ok);
  const initialAxes = resolveProjectionEntryAxes(selection.selection);

  const otherManifestId = "cccccccc-0000-0000-0000-000000000003";
  const emission: Emission = { manifestId: otherManifestId };
  const adopted = adoptResolvedManifestIdentity(initialAxes, emission);

  assertEquals(
    adopted.payload?.target_ref,
    `manifest:${MANIFEST_ID}:projection_entry`,
    "an explicit ?manifest= selection must survive adoption unchanged, even if the resolved emission carries a different manifestId",
  );
});

Deno.test("adoptResolvedManifestIdentity: absent Emission.ManifestId (e.g. dev bypass path) leaves axes unchanged — nothing to adopt", () => {
  const initialAxes = resolveProjectionEntryAxes({});
  const emission: Emission = {};
  const adopted = adoptResolvedManifestIdentity(initialAxes, emission);
  assertEquals(adopted, initialAxes);
});

Deno.test("confirmProjectionEntryEmission: refresh after bare entry must confirm the SAME manifestId as the initial dispatch, not only packageId", () => {
  const initialEmission: Emission = { manifestId: MANIFEST_ID };
  const okResult = confirmProjectionEntryEmission(
    {},
    initialEmission,
    { adoptedManifestId: MANIFEST_ID },
  );
  assert(
    okResult.ok,
    "refresh resolving the SAME adopted manifest must be confirmed ok",
  );

  const driftedEmission: Emission = {
    manifestId: "dddddddd-0000-0000-0000-000000000004",
  };
  const mismatchResult = confirmProjectionEntryEmission(
    {},
    driftedEmission,
    { adoptedManifestId: MANIFEST_ID },
  );
  assert(
    !mismatchResult.ok,
    "refresh resolving a DIFFERENT manifest than the one adopted after initial dispatch must be an explicit error, never silently rendered",
  );
  if (!mismatchResult.ok) {
    assert(
      mismatchResult.error.startsWith("PROJECTION_ENTRY_MANIFEST_MISMATCH"),
    );
  }
});

Deno.test("credential-management bundle: no dedicated credential route/panel was added (existing topology manifest reuse only)", async () => {
  const routesDir = new URL("../routes/admin/", import.meta.url);
  const entries: string[] = [];
  for await (const entry of Deno.readDir(routesDir)) {
    entries.push(entry.name);
  }
  const forbidden = entries.filter((name) => /credential/i.test(name));
  assertEquals(
    forbidden,
    [],
    `no dedicated credential-management admin route may be added; found: ${
      forbidden.join(", ")
    }`,
  );
});
