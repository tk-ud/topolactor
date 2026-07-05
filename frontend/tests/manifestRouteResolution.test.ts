import { assert, assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  CONTENTS_ROUTE_MANIFEST_LIST_FILTER,
} from "../lib/manifestRouteResolution.ts";

Deno.test("CONTENTS_ROUTE_MANIFEST_LIST_FILTER matches SSOT contents_draft_picker", () => {
  assertEquals(CONTENTS_ROUTE_MANIFEST_LIST_FILTER.contentsType, "contents");
  assertEquals(CONTENTS_ROUTE_MANIFEST_LIST_FILTER.requiresTopologySystemName, true);
});

Deno.test("manifestRouteResolution: list filter excludes bundle_projection via contentsType", async () => {
  const src = await Deno.readTextFile(
    new URL("../lib/manifestRouteResolution.ts", import.meta.url),
  );
  assert(src.includes("CONTENTS_ROUTE_MANIFEST_LIST_FILTER"));
  assert(src.includes('contentsType: "contents"'));
  assert(src.includes("requiresTopologySystemName: true"));
  assert(src.includes("listAdminManifests(CONTENTS_ROUTE_MANIFEST_LIST_FILTER)"));
  assertFalse(
    src.includes("listAdminManifests()"),
    "must not list all manifests without contents filter",
  );
});

Deno.test("manifestRouteResolution: localStorage label does not skip topology resolution", async () => {
  const src = await Deno.readTextFile(
    new URL("../lib/manifestRouteResolution.ts", import.meta.url),
  );
  const loadStart = src.indexOf("export async function loadManifestRouteOptions");
  const loadEnd = src.indexOf("export async function resolveManifestRouteOptionForRouteKey");
  assert(loadStart >= 0 && loadEnd > loadStart);
  const loadBody = src.slice(loadStart, loadEnd);
  assert(loadBody.includes("getStoredScreenLabel"));
  assert(loadBody.includes("item.topologySystemName"));
  assertFalse(
    loadBody.includes("if (stored) continue"),
    "stored screen label must not short-circuit manifest list iteration",
  );
});

Deno.test("UiBuilderAdmin ManifestRouteEntry uses contents route manifest filter", async () => {
  const src = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  const entryStart = src.indexOf("function ManifestRouteEntry(");
  const entryEnd = src.indexOf("\nfunction BucketPackageRouteFields(");
  assert(entryStart >= 0 && entryEnd > entryStart);
  const entryBody = src.slice(entryStart, entryEnd);
  assert(entryBody.includes("CONTENTS_ROUTE_MANIFEST_LIST_FILTER"));
  assert(entryBody.includes("listAdminManifests(CONTENTS_ROUTE_MANIFEST_LIST_FILTER)"));
  assertFalse(
    entryBody.includes("listAdminManifests()"),
    "ManifestRouteEntry must not list all manifests",
  );
});
