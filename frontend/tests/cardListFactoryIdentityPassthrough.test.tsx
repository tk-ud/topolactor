/**
 * cardListFactoryIdentityPassthrough.test.tsx
 *
 * Regression test for a real bug caught during audit: admin-normal-surface-projection-seed-ssot.yaml
 * hub_relation_navigation_binding.selected_link_payload_delivery declares that hubRelationId /
 * topologyManifestId / relatedHubId are "carried through onto the rendered CardListItem" (via
 * propBindingResolver.ts navigationLinksToCardItems), so they are available at the point a
 * hub_relation_link_list card is actually rendered/selected. That claim was false:
 * runtimeComponentFactory.ts's cardListFactory rebuilt each item from only
 * {id,title,body,footer,variant} before ever handing it to <CardList>, silently dropping the three
 * identity fields before the rendered item / onSelect emission ever saw them. Fixed by spreading
 * the source item first so passthrough fields survive alongside the normalized known fields.
 */
import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { RUNTIME_COMPONENT_FACTORIES } from "../runtime/runtimeComponentFactory.ts";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const cardListFactory = RUNTIME_COMPONENT_FACTORIES.find((f) =>
  f.componentKinds.includes("display/card_list")
)!.render;

Deno.test("cardListFactory: hubRelationId/topologyManifestId/relatedHubId survive into the rendered onSelect payload (selected_link_payload_delivery)", async () => {
  const { container, cleanup } = setupDom();
  try {
    let captured: unknown = null;
    const spec: RuntimeComponentSpec = {
      componentId: "hub_relation_link_list",
      componentType: "display/card_list",
      props: {
        items: [
          {
            id: 1,
            title: "credential-management",
            hubRelationId: "hr-1111",
            topologyManifestId: "tm-2222",
            relatedHubId: "hub-3333",
          },
        ],
      },
      eventBinding: { select: { eventType: "select" } },
      calcTriggerCallback: (value) => {
        captured = value;
      },
    };

    const result = cardListFactory(spec);
    if (!result.ok) throw new Error(result.error);
    render(h("div", {}, result.node), container);
    await flushUpdates();

    const card = container.querySelector("section") as HTMLElement;
    card.click();
    await flushUpdates();

    const payload = captured as { index: number; item: Record<string, unknown> };
    assertEquals(payload.item.hubRelationId, "hr-1111");
    assertEquals(payload.item.topologyManifestId, "tm-2222");
    assertEquals(payload.item.relatedHubId, "hub-3333");
    // normalized fields still map correctly alongside the passthrough identity fields.
    assertEquals(payload.item.title, "credential-management");
  } finally {
    cleanup();
  }
});
