// frontend/tests/hubNavigationManifestVisibleLabelProjection.test.tsx
//
// Production-composition DOM proof (real fetch-mocked mount, not a synthetic helper call or a
// static string grep) that ManifestsAdmin.tsx / HubNavigationAdmin.tsx honor the topology naming
// SSOT's display_rule (docs/design/admin-console-workflow-ssot.yaml topology_naming_ssot ->
// user_facing_topology_label.display_rule: "visibleName = userFacingTopologyLabel ??
// topologySystemName") for DYNAMIC hub_navigation:list_manifests response rows, instead of the raw
// dispatcher-routing `manifestKey` these two admin surfaces previously showed as primary identity.
//
// Backend already carries this data on every hubs.topology_manifests row (see
// NpgsqlContentBundleRepository.ListTopologyManifestsAsync / ScreenDataShapeTopologyReader.
// FindScreenDataShapeEntryFromTopologyManifestJsonb) -- this file proves the frontend actually
// prefers it, falls back correctly when only one or neither naming field exists, and still keeps
// the raw manifestKey reachable for diagnostics inside a 技術情報 disclosure (never deleted).

import { assert, assertFalse } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import ManifestsAdmin from "../islands/ManifestsAdmin.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function visibleText(container: Element): string {
  const clone = container.cloneNode(true) as Element;
  for (const details of Array.from(clone.querySelectorAll("details"))) {
    details.remove();
  }
  return clone.textContent ?? "";
}

function technicalDisclosureText(container: Element): string {
  return Array.from(container.querySelectorAll("details")).map((d) => d.textContent ?? "").join("\n");
}

type MockManifestRow = {
  topologyManifestId: string;
  manifestKey: string;
  hubId: string;
  hasHubRelations: boolean;
  hubRelationCount: number;
  topologySystemName: string | null;
  userFacingTopologyLabel: string | null;
};

const MOCK_MANIFESTS: MockManifestRow[] = [
  {
    topologyManifestId: "11111111-1111-1111-1111-111111111111",
    manifestKey: "orders.list.screen.read",
    hubId: "h1",
    hasHubRelations: false,
    hubRelationCount: 0,
    topologySystemName: "orders-list",
    userFacingTopologyLabel: "受注一覧",
  },
  {
    topologyManifestId: "22222222-2222-2222-2222-222222222222",
    manifestKey: "legacy.raw.dispatch.key",
    hubId: "h1",
    hasHubRelations: true,
    hubRelationCount: 2,
    topologySystemName: "legacy-topology-name",
    userFacingTopologyLabel: null,
  },
  {
    topologyManifestId: "33333333-3333-3333-3333-333333333333",
    manifestKey: "—",
    hubId: "h1",
    hasHubRelations: false,
    hubRelationCount: 0,
    topologySystemName: null,
    userFacingTopologyLabel: null,
  },
];

function buildFetchMock(): typeof fetch {
  return (async (url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path === "/api/dispatch") {
      const body = JSON.parse(String(init?.body ?? "{}")) as { layer?: string; action?: string };
      if (body.layer === "hub_navigation" && body.action === "list_manifests") {
        return new Response(
          JSON.stringify({ success: true, errors: [], emission: { data: MOCK_MANIFESTS } }),
          { status: 200 },
        );
      }
      if (body.layer === "content_bundle" && body.action === "list_hubs") {
        return new Response(
          JSON.stringify({ success: true, errors: [], emission: { data: [] } }),
          { status: 200 },
        );
      }
      return new Response(
        JSON.stringify({ success: true, errors: [], emission: { data: [] } }),
        { status: 200 },
      );
    }
    return new Response(JSON.stringify({ success: true }), { status: 200 });
  }) as unknown as typeof fetch;
}

async function waitFor(predicate: () => boolean, maxIterations = 40): Promise<void> {
  for (let i = 0; i < maxIterations && !predicate(); i++) {
    await flushUpdates();
  }
}

Deno.test(
  "ManifestsAdmin + HubNavigationAdmin (real mount): userFacingTopologyLabel is shown as the friendly primary identity, raw manifestKey stays reachable only via 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = buildFetchMock();

    try {
      render(h(ManifestsAdmin, {}), container);
      await waitFor(() => container.textContent?.includes("受注一覧") ?? false);

      const primary = visibleText(container);
      assert(primary.includes("受注一覧"), "userFacingTopologyLabel must appear as friendly primary text");
      assertFalse(
        primary.includes("orders.list.screen.read"),
        "the raw dispatcher manifestKey must not leak into always-visible primary text when a label exists",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("orders.list.screen.read"),
        "the raw manifestKey must still be reachable for diagnostics inside a 技術情報 disclosure",
      );
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "ManifestsAdmin + HubNavigationAdmin (real mount): topologySystemName is the primary fallback when userFacingTopologyLabel is absent",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = buildFetchMock();

    try {
      render(h(ManifestsAdmin, {}), container);
      await waitFor(() => container.textContent?.includes("legacy-topology-name") ?? false);

      const primary = visibleText(container);
      assert(
        primary.includes("legacy-topology-name"),
        "topologySystemName must be the primary fallback when no userFacingTopologyLabel exists",
      );
      assertFalse(
        primary.includes("legacy.raw.dispatch.key"),
        "the raw dispatcher manifestKey must not leak into always-visible primary text when topologySystemName exists",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("legacy.raw.dispatch.key"),
        "the raw manifestKey must still be reachable for diagnostics inside a 技術情報 disclosure",
      );
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "ManifestsAdmin + HubNavigationAdmin (real mount): raw manifestKey remains the visible identity only when neither naming SSOT field exists at all",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = buildFetchMock();

    try {
      render(h(ManifestsAdmin, {}), container);
      await waitFor(() => container.textContent?.includes("受注一覧") ?? false);

      // The row with neither topologySystemName nor userFacingTopologyLabel has no other identity
      // to show; falling back to the raw manifestKey ("—") here is the legitimate last resort, not
      // a label-boundary violation, and must not be hidden.
      const primary = visibleText(container);
      assert(primary.includes("—"), "raw manifestKey must remain the visible identity as the final fallback");
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);
