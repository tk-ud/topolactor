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
  confirmProjectionEntryEmission,
  isDefaultProjectionEntry,
  parseProjectionEntrySelection,
  resolveProjectionEntryAxes,
} from "../runtime/projectionEntry.ts";

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
  assert(axes.target !== "default", "route-selected entry must not dispatch the default target");
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
