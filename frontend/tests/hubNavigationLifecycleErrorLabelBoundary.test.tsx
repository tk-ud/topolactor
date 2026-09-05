// frontend/tests/hubNavigationLifecycleErrorLabelBoundary.test.tsx
//
// Production-composition DOM proof (real fetch-mocked mount driving a real create/deprecate
// action, not a static grep) that HubNavigationAdmin.tsx's lifecycle error path (create / update /
// deprecate / reorder, plus load-time network exceptions) no longer shows raw backend/client
// diagnostic text as always-visible primary content. Round-4 fixed the naming-label projection on
// this same surface; this proof targets the SEPARATE `ValidationErrorPanel`-routed error path,
// whose raw messages interpolate internal vocabulary (related_hub_id, source hub_id,
// hub_relations, topology manifest) that must never be normal-view primary meaning.

import { assert, assertFalse } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import HubNavigationAdmin from "../islands/HubNavigationAdmin.tsx";

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

async function waitFor(predicate: () => boolean, maxIterations = 40): Promise<void> {
  for (let i = 0; i < maxIterations && !predicate(); i++) {
    await flushUpdates();
  }
}

function fireEvent(el: Element, type: string): void {
  el.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event(type, { bubbles: true }));
}

function clickButtonByText(container: Element, text: string): void {
  const button = Array.from(container.querySelectorAll("button")).find((b) => b.textContent === text);
  if (!button) throw new Error(`no <button> with text "${text}" found`);
  fireEvent(button, "click");
}

/** Clicks the real ConfirmDialog's confirm button (this island uses useConfirm, not window.confirm). */
async function acceptConfirmDialog(container: Element): Promise<void> {
  await waitFor(() => container.querySelector('[role="alertdialog"]') !== null);
  const dialog = container.querySelector('[role="alertdialog"]') as Element;
  const confirmButton = dialog.querySelector("button.btn-primary, button.btn-danger") as HTMLButtonElement;
  fireEvent(confirmButton, "click");
  await flushUpdates();
}

const MANIFEST_A = {
  topologyManifestId: "11111111-1111-1111-1111-111111111111",
  manifestKey: "orders.list.screen.read",
  hubId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  hasHubRelations: true,
  hubRelationCount: 1,
  topologySystemName: "orders-list",
  userFacingTopologyLabel: "受注一覧",
};

const HUB_RELATION_A = {
  hubRelationId: "22222222-2222-2222-2222-222222222222",
  topologyManifestId: MANIFEST_A.topologyManifestId,
  relatedHubId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  relatedHubLabel: "遷移先ハブ",
  sequencePosition: 1,
  relationConfig: null,
  status: "active",
};

const HUB_B = { id: MANIFEST_A.hubId, label: "受注ハブ", summary: "受注ハブ概要" };

function buildFetchMock(
  dispatchOverride: (body: { layer?: string; action?: string; payload?: unknown }) => Response | null,
): typeof fetch {
  return (async (url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path === "/api/dispatch") {
      const body = JSON.parse(String(init?.body ?? "{}")) as {
        layer?: string;
        action?: string;
        payload?: unknown;
      };
      const overridden = dispatchOverride(body);
      if (overridden) return overridden;

      if (body.layer === "hub_navigation" && body.action === "list_manifests") {
        return new Response(
          JSON.stringify({ success: true, errors: [], emission: { data: [MANIFEST_A] } }),
          { status: 200 },
        );
      }
      if (body.layer === "content_bundle" && body.action === "list_hubs") {
        return new Response(
          JSON.stringify({ success: true, errors: [], emission: { data: [HUB_B] } }),
          { status: 200 },
        );
      }
      if (body.layer === "hub_navigation" && body.action === "get_hub_relations") {
        return new Response(
          JSON.stringify({ success: true, errors: [], emission: { data: [HUB_RELATION_A] } }),
          { status: 200 },
        );
      }
      return new Response(JSON.stringify({ success: true, errors: [], emission: { data: [] } }), { status: 200 });
    }
    return new Response(JSON.stringify({ success: true }), { status: 200 });
  }) as unknown as typeof fetch;
}

function lifecycleResponse(ok: boolean, message: string, errorCode?: string): Response {
  return new Response(
    JSON.stringify({
      success: true,
      errors: [],
      emission: { data: { ok, hubRelationId: ok ? "new-id" : null, status: ok ? "active" : "error", message, errorCode } },
    }),
    { status: 200 },
  );
}

Deno.test(
  "HubNavigationAdmin (real mount): SELF_LOOP lifecycle failure renders a friendly Japanese primary message, raw internal-vocabulary text only in 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = buildFetchMock((body) => {
      if (body.layer === "hub_navigation" && body.action === "create") {
        return lifecycleResponse(
          false,
          "Self-loop: related_hub_id cannot equal source hub_id.",
          "SELF_LOOP",
        );
      }
      return null;
    });

    try {
      render(h(HubNavigationAdmin, {}), container);
      await waitFor(() => container.querySelector("select") !== null);

      const manifestSelect = container.querySelector("select") as HTMLSelectElement;
      manifestSelect.value = MANIFEST_A.topologyManifestId;
      fireEvent(manifestSelect, "change");
      await waitFor(() => container.querySelectorAll("select").length > 1);

      // Open the create form and submit.
      clickButtonByText(container, "+ 追加");
      await flushUpdates();

      const selects = container.querySelectorAll("select");
      const destinationSelect = selects[selects.length - 1] as HTMLSelectElement;
      destinationSelect.value = HUB_B.id;
      fireEvent(destinationSelect, "change");
      await flushUpdates();

      clickButtonByText(container, "登録");
      await acceptConfirmDialog(container);
      await waitFor(() => (container.textContent ?? "").includes("自分自身への遷移"));

      const primary = visibleText(container);
      assert(
        primary.includes("自分自身への遷移は登録できません"),
        "a friendly Japanese message must be the always-visible primary error text",
      );
      assertFalse(
        primary.includes("related_hub_id"),
        "the raw internal field name must not appear in always-visible primary text",
      );
      assertFalse(
        primary.includes("Self-loop"),
        "the raw backend diagnostic message must not appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(technical.includes("SELF_LOOP"), "the raw error code must still be reachable in a 技術情報 disclosure");
      assert(
        technical.includes("Self-loop: related_hub_id cannot equal source hub_id."),
        "the raw backend diagnostic message must still be reachable in a 技術情報 disclosure",
      );
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "HubNavigationAdmin (real mount): HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST deprecate failure renders a friendly Japanese primary message, raw hub_relations vocabulary only in disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = buildFetchMock((body) => {
      if (body.layer === "hub_navigation" && body.action === "deprecate") {
        return lifecycleResponse(
          false,
          "Cannot deprecate the last active hub_relations row for this topology manifest -- " +
            "would leave the manifest with zero active hub relations (navigation orphan).",
          "HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST",
        );
      }
      return null;
    });

    try {
      render(h(HubNavigationAdmin, {}), container);
      await waitFor(() => container.querySelector("select") !== null);

      const manifestSelect = container.querySelector("select") as HTMLSelectElement;
      manifestSelect.value = MANIFEST_A.topologyManifestId;
      fireEvent(manifestSelect, "change");
      await waitFor(() =>
        Array.from(container.querySelectorAll("button")).some((b) => b.textContent === "削除")
      );

      clickButtonByText(container, "削除");
      await acceptConfirmDialog(container);
      await waitFor(() => (container.textContent ?? "").includes("削除できません"));

      const primary = visibleText(container);
      assert(
        primary.includes("この設定に残る最後のナビ遷移は削除できません"),
        "a friendly Japanese message must be the always-visible primary error text",
      );
      assertFalse(primary.includes("hub_relations"), "the raw table-name vocabulary must not appear in primary text");
      assertFalse(primary.includes("navigation orphan"), "the raw backend diagnostic phrase must not appear in primary text");

      const technical = technicalDisclosureText(container);
      assert(technical.includes("HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST"));
      assert(technical.includes("navigation orphan"));
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "HubNavigationAdmin (real mount): an unmapped/codeless load failure falls back to one generic friendly sentence, never the raw exception text",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = ((url: string, init?: RequestInit) => {
      const path = url.toString();
      if (path === "/api/dispatch") {
        const body = JSON.parse(String(init?.body ?? "{}")) as { layer?: string; action?: string };
        if (body.layer === "hub_navigation" && body.action === "list_manifests") {
          return Promise.resolve(
            new Response(
              JSON.stringify({
                success: false,
                errors: [{ message: "relation hint: unexpected upstream shape from topology_manifests join" }],
              }),
              { status: 200 },
            ),
          );
        }
        return Promise.resolve(new Response(JSON.stringify({ success: true, errors: [], emission: { data: [] } }), { status: 200 }));
      }
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }) as unknown as typeof fetch;

    try {
      render(h(HubNavigationAdmin, {}), container);
      await waitFor(() => (container.textContent ?? "").includes("処理に失敗しました"));

      const primary = visibleText(container);
      assert(
        primary.includes("処理に失敗しました"),
        "an unmapped/codeless error must fall back to the single generic friendly sentence",
      );
      assertFalse(
        primary.includes("topology_manifests join"),
        "the raw exception/backend message must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(technical.includes("topology_manifests join"));
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);
