import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { HubNavigationManifestItem } from "../api/adminApi.ts";
import { hubNavigationTargetManifestOptions } from "../lib/hubNavigationPicker.ts";
import { UX_HUB_NAVIGATION_MANIFEST_UNNAMED_LABEL } from "../content/adminUxTerms.ts";

function manifest(
  partial: Partial<HubNavigationManifestItem> & Pick<HubNavigationManifestItem, "topologyManifestId">,
): HubNavigationManifestItem {
  return {
    manifestKey: "k",
    hubId: "h",
    hasHubRelations: false,
    hubRelationCount: 0,
    ...partial,
  };
}

Deno.test("hubNavigationTargetManifestOptions: one option per topology manifest, valued by topologyManifestId (the target the backend receives), never a hub id", () => {
  const options = hubNavigationTargetManifestOptions([
    manifest({ topologyManifestId: "m-1", hubId: "hub-shared", userFacingTopologyLabel: "受注一覧" }),
    manifest({ topologyManifestId: "m-2", hubId: "hub-shared", topologySystemName: "orders-detail" }),
  ]);
  // Two manifests under the same hub stay two distinct, individually selectable targets.
  assertEquals(options.map((o) => o.value), ["m-1", "m-2"]);
});

Deno.test("hubNavigationTargetManifestOptions: label follows the source selector's visibleName rule, fail-close placeholder instead of a raw id", () => {
  const options = hubNavigationTargetManifestOptions([
    manifest({ topologyManifestId: "m-1", userFacingTopologyLabel: "受注一覧", topologySystemName: "orders-list" }),
    manifest({ topologyManifestId: "m-2", topologySystemName: "orders-detail" }),
    manifest({ topologyManifestId: "m-3" }),
  ]);
  assertEquals(options.map((o) => o.label), ["受注一覧", "orders-detail", UX_HUB_NAVIGATION_MANIFEST_UNNAMED_LABEL]);
});

Deno.test("listContentHubs: rejects non-array emission.data (explicit API boundary)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({
      success: true,
      emission: { data: { manifestId: "not-a-list" } },
    }), { status: 200 }));
  try {
    const { listContentHubs } = await import("../api/adminApi.ts");
    await assertRejects(
      () => listContentHubs(),
      Error,
      "content_bundle:list_hubs: emission.data must be an array",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("listHubNavigationManifests: throws on success:false (not empty list)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({
      success: false,
      errors: [{ message: "HUB_NAV_LIST_FAILED" }],
    }), { status: 200 }));
  try {
    const { listHubNavigationManifests } = await import("../api/adminApi.ts");
    await assertRejects(
      () => listHubNavigationManifests(),
      Error,
      "HUB_NAV_LIST_FAILED",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("listHubNavigationManifests: throws when success:true but emission.data is missing", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({
      success: true,
      emission: null,
    }), { status: 200 }));
  try {
    const { listHubNavigationManifests } = await import("../api/adminApi.ts");
    await assertRejects(
      () => listHubNavigationManifests(),
      Error,
      "hub_navigation:list_manifests: emission.data must be an array",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("getHubRelationsByManifest: throws on success:false (not empty list)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({
      success: false,
      errors: [{ message: "HUB_RELATIONS_FAILED" }],
    }), { status: 200 }));
  try {
    const { getHubRelationsByManifest } = await import("../api/adminApi.ts");
    await assertRejects(
      () => getHubRelationsByManifest("manifest-1"),
      Error,
      "HUB_RELATIONS_FAILED",
    );
  } finally {
    globalThis.fetch = original;
  }
});
