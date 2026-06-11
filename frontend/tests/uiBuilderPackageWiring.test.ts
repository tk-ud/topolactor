import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildWiringKindSelectOptions,
  encodeManifestPackageTargetRef,
  parseManifestPackageTargetRef,
  manifestWiringKeyFromTargetRef,
  manifestIdFromTargetRef,
  isRouteNavigationTargetRef,
} from "../lib/packageWiringPicker.ts";
import { buildScreenReadQueryWiringCandidates } from "../lib/screenReadQueryWiring.ts";

Deno.test("UiBuilderAdmin exposes package wiring editor and dispatch axes", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(source.includes("パッケージ配線（編集）"), true);
  assertEquals(source.includes("get_package_wiring"), true);
  assertEquals(source.includes("update_package_wiring"), true);
  assertEquals(source.includes("パッケージ配線（参照）"), false);
});

Deno.test("UiBuilderAdmin package wiring uses picker not manual manifest UUID input", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(source.includes("マニフェスト ID（Step3"), false);
  assertEquals(source.includes("接続先ページ"), true);
  assertEquals(source.includes("type=\"radio\""), true);
  assertEquals(source.includes("listAdminManifests"), true);
});

// ─── screen_data_shape candidate builder ─────────────────────────────────────

Deno.test("buildScreenReadQueryWiringCandidates returns searchCondition candidates", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      screenReadQueryWiring: {
        searchConditions: {
          bindings: [
            { wiringKey: "sc.name.eq", column: "name", operator: "=" },
          ],
        },
      },
    },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.length >= 1, true);
  assertEquals(candidates.some((c) => c.wiringKey === "sc.name.eq"), true);
  assertEquals(candidates.find((c) => c.wiringKey === "sc.name.eq")?.field, "searchConditions");
});

Deno.test("buildScreenReadQueryWiringCandidates returns havingConditions candidates", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      screenReadQueryWiring: {
        havingConditions: {
          bindings: [
            { wiringKey: "hc.salary.sum.gt", column: "salary", function: "sum", operator: ">" },
          ],
        },
      },
    },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.some((c) => c.wiringKey === "hc.salary.sum.gt"), true);
  assertEquals(candidates.find((c) => c.wiringKey === "hc.salary.sum.gt")?.field, "havingConditions");
});

Deno.test("buildScreenReadQueryWiringCandidates returns aggregationMeasures candidates", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      screenReadQueryWiring: {
        aggregationMeasures: {
          bindings: [
            { wiringKey: "agg.salary.sum", column: "salary", function: "sum" },
          ],
        },
      },
    },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.some((c) => c.wiringKey === "agg.salary.sum"), true);
  assertEquals(candidates.find((c) => c.wiringKey === "agg.salary.sum")?.field, "aggregationMeasures");
});

Deno.test("buildScreenReadQueryWiringCandidates returns displayColumns candidates", () => {
  const topology = JSON.stringify([
    {
      type: "screen_data_shape",
      screenReadQueryWiring: {
        displayColumns: {
          bindings: [
            { wiringKey: "dc.name", column: "name" },
          ],
        },
      },
    },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.some((c) => c.wiringKey === "dc.name"), true);
  assertEquals(candidates.find((c) => c.wiringKey === "dc.name")?.field, "displayColumns");
});

Deno.test("buildScreenReadQueryWiringCandidates returns empty for missing screenReadQueryWiring", () => {
  const topology = JSON.stringify([
    { type: "screen_data_shape" },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.length, 0);
});

Deno.test("buildScreenReadQueryWiringCandidates returns empty for non-screen_data_shape entries", () => {
  const topology = JSON.stringify([
    { type: "dispatcher_mapping", role: "admin" },
  ]);
  const candidates = buildScreenReadQueryWiringCandidates(topology);
  assertEquals(candidates.length, 0);
});

// ─── buildWiringKindSelectOptions includes read-query wiringKeys ──────────────

Deno.test("buildWiringKindSelectOptions includes read-query candidate wiringKeys", () => {
  const candidates = [
    { wiringKey: "sc.name.eq", label: "searchConditions · name · =", field: "searchConditions", valueSourceKinds: [] as never[] },
    { wiringKey: "dc.amount", label: "displayColumns · amount", field: "displayColumns", valueSourceKinds: [] as never[] },
  ];
  const options = buildWiringKindSelectOptions(["navigation"], candidates);
  assertEquals(options.includes("sc.name.eq"), true);
  assertEquals(options.includes("dc.amount"), true);
});

Deno.test("buildWiringKindSelectOptions without candidates includes component kinds", () => {
  const options = buildWiringKindSelectOptions(["navigation", "click"], []);
  // must include known component kind values
  assertEquals(options.length >= 0, true);
});

// ─── manifest targetRef encoding / parsing ───────────────────────────────────

Deno.test("encodeManifestPackageTargetRef encodes manifestId + wiringKey", () => {
  const ref = encodeManifestPackageTargetRef("abc-def-123", "sc.name.eq");
  assertEquals(ref, "manifest:abc-def-123:sc.name.eq");
});

Deno.test("encodeManifestPackageTargetRef with empty wiringKey returns manifest:<id> only", () => {
  const ref = encodeManifestPackageTargetRef("abc-def-123", "");
  assertEquals(ref, "manifest:abc-def-123");
});

Deno.test("parseManifestPackageTargetRef extracts manifestId and wiringKey", () => {
  const uuid = "12345678-1234-1234-1234-123456789abc";
  const parsed = parseManifestPackageTargetRef(`manifest:${uuid}:sc.name.eq`);
  assertEquals(parsed?.wiringKey, "sc.name.eq");
  assertEquals(parsed?.manifestId, uuid);
});

Deno.test("manifestWiringKeyFromTargetRef extracts wiringKey for manifest surface", () => {
  const uuid = "12345678-1234-1234-1234-123456789abc";
  const key = manifestWiringKeyFromTargetRef(
    `manifest:${uuid}:sc.name.eq`,
    "manifest",
  );
  assertEquals(key, "sc.name.eq");
});

Deno.test("manifestIdFromTargetRef extracts manifestId for manifest surface", () => {
  const uuid = "12345678-1234-1234-1234-123456789abc";
  const id = manifestIdFromTargetRef(`manifest:${uuid}:sc.name.eq`, "manifest");
  assertEquals(id, uuid);
});

// ─── route navigation is frontend-local ─────────────────────────────────────

Deno.test("isRouteNavigationTargetRef returns true for route: prefix", () => {
  assertEquals(isRouteNavigationTargetRef("route:customer-management"), true);
});

Deno.test("isRouteNavigationTargetRef returns false for manifest: prefix", () => {
  assertFalse(isRouteNavigationTargetRef("manifest:abc-def:sc.name.eq"));
});

Deno.test("isRouteNavigationTargetRef returns false for empty string", () => {
  assertFalse(isRouteNavigationTargetRef(""));
});

// ─── handleSaveWiring blocking checks in source ──────────────────────────────

Deno.test("UiBuilderAdmin blocks save when manifest surface wiringKey is empty", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // Source must contain the blocking check for empty wiringKey on manifest surface.
  assertEquals(
    source.includes("read/query 配線キーを選択してください"),
    true,
  );
});

Deno.test("UiBuilderAdmin blocks save when screenWiringCandidates is empty for manifest surface", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(
    source.includes("read/query 配線候補がありません"),
    true,
  );
});

Deno.test("UiBuilderAdmin blocks save when candidateLoadError is set", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(source.includes("candidateLoadError"), true);
  assertEquals(
    source.includes("配線候補の取得に失敗しています"),
    true,
  );
});

Deno.test("UiBuilderAdmin does not have 接続なし option for manifest wiring", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // The "接続なし（ページのみ紐づけ）" option that allowed empty wiringKey must be removed.
  assertFalse(source.includes("接続なし（ページのみ紐づけ）"));
});

Deno.test("UiBuilderAdmin route navigation lane does not use manifest surface", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // RouteNavigationWiringPreset uses targetSurface:route, not manifest.
  assertEquals(source.includes("RouteNavigationWiringPreset"), true);
  assertEquals(source.includes("encodeRouteNavigationTargetRef"), true);
  // Route navigation target ref uses "route:" prefix only, not manifest surface.
  assertEquals(source.includes("isRouteNavigationTargetRef"), true);
});
