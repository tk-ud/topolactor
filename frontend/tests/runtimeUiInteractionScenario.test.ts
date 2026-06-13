import {
  buildVisualLayoutPatchJson,
  parseVisualLayoutPatchJson,
  type VisualNodePayload,
} from "../runtime/visualLayoutUtils.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly } from "../runtime/runtimeComponentFactory.ts";
import type { RuntimeLocalStateStore } from "../runtime/runtimeComponentAdapter.ts";

function assert(
  condition: unknown,
  message = "assertion failed",
): asserts condition {
  if (!condition) throw new Error(message);
}

function assertEquals(
  actual: unknown,
  expected: unknown,
  message?: string,
): void {
  if (actual !== expected) {
    throw new Error(
      message ?? `Expected ${String(expected)}, got ${String(actual)}`,
    );
  }
}

class TestLocalStateStore implements RuntimeLocalStateStore {
  #state = new Map<string, unknown>();

  get(targetNodeId: string, statePath: string): unknown {
    return this.#state.get(`${targetNodeId}:${statePath}`);
  }

  set(targetNodeId: string, statePath: string, value: unknown): void {
    this.#state.set(`${targetNodeId}:${statePath}`, value);
  }
}

function dataOpen(spec: unknown): unknown {
  if (typeof spec !== "object" || spec === null || !("runtimeSpec" in spec)) {
    return undefined;
  }
  const runtimeSpec =
    (spec as { runtimeSpec?: { props?: Record<string, unknown> } }).runtimeSpec;
  const props = runtimeSpec?.props;
  const data = props?.data;
  if (typeof data === "object" && data !== null && !Array.isArray(data)) {
    return (data as Record<string, unknown>).open;
  }
  return props?.open;
}

Deno.test("runtime-ui-interaction-wiring scenario: button click opens and modal close closes via layout patch → emission → renderEmission", () => {
  const modalNodeId = "node-modal-target";
  const buttonNode: VisualNodePayload = {
    nodeId: "node-open-button",
    nodeKind: "catalog_component",
    componentKey: "button.primitive",
    componentKind: "action/button",
    componentId: "comp-open-button",
    slotKey: "main",
    orderIndex: 0,
    parentNodeId: null,
    isDraftOnly: false,
    gridCol: 1,
    gridRow: 1,
    x: 0,
    y: 0,
    width: 120,
    height: 40,
    propsJson: JSON.stringify({
      data: { label: "Open modal" },
      eventWirings: [
        {
          eventType: "onClick",
          actionType: "openModal",
          targetNodeId: modalNodeId,
        },
      ],
    }),
  };
  const modalNode: VisualNodePayload = {
    nodeId: modalNodeId,
    nodeKind: "catalog_component",
    componentKey: "modal.primitive",
    componentKind: "disclosure/modal",
    componentId: "comp-modal",
    slotKey: "main",
    orderIndex: 1,
    parentNodeId: null,
    isDraftOnly: false,
    gridCol: 1,
    gridRow: 2,
    x: 0,
    y: 60,
    width: 320,
    height: 200,
    propsJson: JSON.stringify({
      data: { title: "Runtime modal", body: "Opened by button click" },
      eventWirings: [
        {
          eventType: "onClose",
          actionType: "closeModal",
          targetNodeId: modalNodeId,
        },
      ],
    }),
    stateJson: JSON.stringify({ open: false }),
  };

  const patchJson = buildVisualLayoutPatchJson([buttonNode, modalNode]);
  const parsedPatch = parseVisualLayoutPatchJson(patchJson, [
    {
      componentKey: "button.primitive",
      componentKind: "action/button",
      isDraftOnly: false,
    },
    {
      componentKey: "modal.primitive",
      componentKind: "disclosure/modal",
      isDraftOnly: false,
    },
  ]);
  assert(parsedPatch.ok, "layout patch should parse");

  const emission = draftPreviewResultToEmission({
    success: true,
    layoutId: "layout-runtime-ui-interaction",
    packageId: "pkg-runtime-ui-interaction",
    layoutNodes: parsedPatch.value.nodes.map((node) => ({
      nodeId: node.nodeId,
      nodeKind: node.nodeKind,
      componentKey: node.componentKey,
      componentKind: node.componentKind,
      componentId: node.componentId,
      parentNodeId: node.parentNodeId,
      slotKey: node.slotKey ?? undefined,
      orderIndex: node.orderIndex,
      width: node.width,
      height: node.height,
      propsJson: node.propsJson,
      stateJson: node.stateJson,
    })),
  });
  assert(emission, "draft preview result should restore to emission");

  const localStateStore = new TestLocalStateStore();
  let specs = renderEmission(emission, defaultComponentRegistry, {
    localStateStore,
  });
  const button = specs.find((spec) => spec.nodeId === "node-open-button");
  const modal = specs.find((spec) => spec.nodeId === modalNodeId);

  assert(button?.runtimeSpec, "button runtimeSpec should exist");
  assert(modal?.runtimeSpec, "modal runtimeSpec should exist");
  assert(
    button.runtimeSpec.eventBinding.click,
    "button should have click binding",
  );
  assertEquals(
    (button.runtimeSpec.eventBinding.click as { runtimeDispatch?: unknown })
      .runtimeDispatch,
    undefined,
    "local UI interaction must not be backend runtimeDispatch",
  );
  assertEquals(
    (button.runtimeSpec.eventBinding.click as { routeNavigation?: unknown })
      .routeNavigation,
    undefined,
    "local UI interaction must not be routeNavigation",
  );
  assertEquals(
    dataOpen(modal),
    false,
    "stateJson.open is the initial modal state only",
  );

  const openResult = __testOnly.emitBoundEvent(button.runtimeSpec, "click", {});
  assert(openResult.ok, "button click should execute local state mutation");
  assertEquals(localStateStore.get(modalNodeId, "open"), true);

  specs = renderEmission(emission, defaultComponentRegistry, {
    localStateStore,
  });
  const openedModal = specs.find((spec) => spec.nodeId === modalNodeId);
  assert(openedModal?.runtimeSpec, "opened modal runtimeSpec should exist");
  assertEquals(
    dataOpen(openedModal),
    true,
    "re-render should project modal final open state",
  );

  const closeResult = __testOnly.emitBoundEvent(
    openedModal.runtimeSpec,
    "toggle",
    { open: false },
  );
  assert(closeResult.ok, "modal close should execute local state mutation");
  assertEquals(localStateStore.get(modalNodeId, "open"), false);

  specs = renderEmission(emission, defaultComponentRegistry, {
    localStateStore,
  });
  const closedModal = specs.find((spec) => spec.nodeId === modalNodeId);
  assertEquals(
    dataOpen(closedModal),
    false,
    "close action should project modal final closed state",
  );
});
