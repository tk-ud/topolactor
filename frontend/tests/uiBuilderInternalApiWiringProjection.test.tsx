// frontend/tests/uiBuilderInternalApiWiringProjection.test.tsx
//
// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
// lane_storage_boundary.lanes.package_internal_api_wiring_lane / setting_category_taxonomy
// (内部API is part of the wiring inspector classification and "must not be hidden outside the
// inspector taxonomy while claiming backend-side completion").
//
// Before this round, WiringGraphPanel's `internalApiWirings` projection input existed and was
// fully implemented (buildWiringGraphProjection projects it into a category:"internal_api" edge),
// but the ONLY production mount of WiringGraphPanel (frontend/islands/UiBuilderAdmin.tsx, "配線
// ビュー" canvas mode) never passed it — the capability was real but structurally disconnected from
// production composition, so 内部API never actually appeared in the wiring inspector taxonomy view.
//
// This proves two things with real production code, not a synthetic helper call:
// 1. The REAL useEffectivePackageWiringKind hook (mounted in a live DOM, hitting a mocked
//    ui_topology:get_package_wiring fetch — the SAME action PackageWiringEditor itself reads)
//    resolves targetSurface/targetRef alongside wiringKind.
// 2. UiBuilderAdmin.tsx's actual <WiringGraphPanel /> call site now forwards internalApiWirings,
//    and that prop is DERIVED from the same hook/package-wiring row rather than a hardcoded empty
//    array — i.e. the projection capability and the production authoring composition are the same
//    taxonomy, not two disconnected surfaces.

import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { useEffectivePackageWiringKind } from "../lib/uiBuilderEventAuthoringHooks.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function HookHarness(
  { packageId, onResult }: {
    packageId: string;
    onResult: (r: { wiringKind: string | null; targetSurface: string | null; targetRef: string | null }) => void;
  },
) {
  const result = useEffectivePackageWiringKind(packageId);
  onResult(result);
  return h("div", null, JSON.stringify(result));
}

Deno.test("useEffectivePackageWiringKind: resolves targetSurface/targetRef alongside wiringKind from the SAME ui_topology:get_package_wiring row PackageWiringEditor reads", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = (_url: string | URL | Request, init?: RequestInit) => {
      const req = JSON.parse((init?.body as string) ?? "{}");
      if (req.layer === "ui_topology" && req.action === "get_package_wiring") {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              success: true,
              emission: {
                data: {
                  wiringKind: "manifest",
                  targetSurface: "manifest",
                  targetRef: "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
                },
              },
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    };
    let latest: { wiringKind: string | null; targetSurface: string | null; targetRef: string | null } | null = null;
    render(
      h(HookHarness, {
        packageId: "pkg-1",
        onResult: (r) => {
          latest = r;
        },
      }),
      container,
    );
    await flushUpdates();
    await flushUpdates();
    if (!latest) throw new Error("hook must have produced a result");
    const result = latest as { wiringKind: string | null; targetSurface: string | null; targetRef: string | null };
    assertEquals(result.wiringKind, "manifest");
    assertEquals(result.targetSurface, "manifest");
    assertEquals(
      result.targetRef,
      "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
    );
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("UiBuilderAdmin production composition: the real WiringGraphPanel mount forwards internalApiWirings derived from useEffectivePackageWiringKind, not a hardcoded empty array", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  // The wiring-canvas call site must forward the prop.
  assert(
    /<WiringGraphPanel[\s\S]{0,800}internalApiWirings=\{internalApiWirings\}/.test(source),
    "the production <WiringGraphPanel /> mount must pass internalApiWirings",
  );
  // internalApiWirings must be derived from the SAME package-wiring authority
  // (useEffectivePackageWiringKind / packageWiringTargetSurface / packageWiringTargetRef), not a
  // literal [] — otherwise the prop would exist syntactically while staying permanently empty.
  assert(
    source.includes(
      "const { targetSurface: packageWiringTargetSurface, targetRef: packageWiringTargetRef } =",
    ),
    "internalApiWirings must be sourced from useEffectivePackageWiringKind's targetSurface/targetRef",
  );
  assert(
    /const internalApiWirings: InternalApiWiringInput\[\] = packageWiringTargetSurface === "manifest"/
      .test(source),
    "internalApiWirings must be built from the package wiring row's targetSurface==='manifest' case",
  );
});
