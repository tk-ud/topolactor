/**
 * Proof surface for docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml.
 *
 * required_proof coverage:
 * - runtimeInteractions -> wiring projection round-trip (projection derives from
 *   and rehydrates to the same runtimeInteractions authority; view only)
 * - valid drag-drop wiring edit produces a typed runtimeInteraction patch
 * - invalid drag-drop wiring edit produces an explicit error and no draft mutation
 * - lifecycle trigger + backend/external dispatch without confirmation fails close
 * - high-frequency trigger + backend/external dispatch without debounceMs fails close
 * - trigger outside SSOT vocabulary fails close
 * - lifecycle / dispatch interactions are preview-inert
 */

import { assertEquals } from "jsr:@std/assert";
import {
  ALL_WIRING_TRIGGERS,
  applyWiringDropEdit,
  buildWiringGraphProjection,
  classifyTrigger,
  findRuntimeInteractionPolicyErrors,
  HIGH_FREQUENCY_TRIGGERS,
  isBackendOrExternalDispatchAction,
  isHighFrequencyTrigger,
  isLifecycleTrigger,
  isPreviewInertInteraction,
  TRIGGER_VOCABULARY,
  type WiringNode,
} from "../lib/uiBuilderWiringProjection.ts";

function fixtureNodes(): WiringNode[] {
  return [
    {
      nodeId: "n-button",
      componentKey: "action/button",
      componentKind: "action/button",
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "openModal",
          targetNodeId: "n-modal",
          statePath: "open",
        },
        {
          trigger: "click",
          actionType: "dispatchExternalPort",
          portTargetRef: "external-port:access_port:port-1",
          payloadFrom: { amount: "node:n-input.value" },
          outputProp: "result",
        },
      ],
    },
    {
      nodeId: "n-modal",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
    },
    {
      nodeId: "n-input",
      componentKey: "form_input/text",
      componentKind: "form_input/text",
      runtimeInteractions: [
        {
          trigger: "change",
          actionType: "dispatchInstanceOperation",
          instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
        },
      ],
    },
  ];
}

// ─── trigger vocabulary ───────────────────────────────────────────────────────

Deno.test("trigger vocabulary: SSOT groups classify and unknown trigger is outside", () => {
  assertEquals(classifyTrigger("load"), "lifecycle");
  assertEquals(classifyTrigger("route_enter"), "lifecycle");
  assertEquals(classifyTrigger("initial_display"), "lifecycle");
  assertEquals(classifyTrigger("mouseon"), "pointer");
  assertEquals(classifyTrigger("keydown"), "keyboard");
  assertEquals(classifyTrigger("click"), "form");
  assertEquals(classifyTrigger("not_a_trigger"), null);
  // ALL_WIRING_TRIGGERS is exactly the union of the SSOT groups.
  const union = Object.values(TRIGGER_VOCABULARY).flat();
  assertEquals([...ALL_WIRING_TRIGGERS].sort(), [...union].sort());
});

Deno.test("trigger classification: lifecycle and high-frequency sets", () => {
  assertEquals(isLifecycleTrigger("load"), true);
  assertEquals(isLifecycleTrigger("click"), false);
  for (const t of HIGH_FREQUENCY_TRIGGERS) {
    assertEquals(isHighFrequencyTrigger(t), true, t);
  }
  assertEquals(isHighFrequencyTrigger("click"), false);
  assertEquals(isBackendOrExternalDispatchAction("dispatchExternalPort"), true);
  assertEquals(
    isBackendOrExternalDispatchAction("dispatchInstanceOperation"),
    true,
  );
  assertEquals(isBackendOrExternalDispatchAction("openModal"), false);
});

// ─── wiring projection round-trip ────────────────────────────────────────────

Deno.test("wiring projection: derives edges from runtimeInteractions and maps back by index (round-trip)", () => {
  const nodes = fixtureNodes();
  const before = JSON.stringify(nodes);
  const projection = buildWiringGraphProjection(nodes);

  // View only: building the projection never mutates the persistence authority.
  assertEquals(JSON.stringify(nodes), before);

  assertEquals(projection.edges.length, 3);
  assertEquals(projection.unwiredNodeIds, ["n-modal"]);

  // Every edge rehydrates to exactly the runtimeInteraction it projects.
  for (const edge of projection.edges) {
    const source = nodes.find((n) => n.nodeId === edge.sourceNodeId)!;
    const interaction = source.runtimeInteractions![edge.interactionIndex];
    assertEquals(edge.trigger, interaction.trigger);
    if (edge.targetKind === "node") {
      assertEquals(edge.targetRef, interaction.targetNodeId ?? null);
    } else if (edge.targetKind === "external_port") {
      assertEquals(edge.targetRef, interaction.portTargetRef ?? null);
    } else if (edge.targetKind === "instance_port") {
      assertEquals(edge.targetRef, interaction.instanceTargetRef ?? null);
    }
  }

  // source UI node -> event trigger -> setting category -> target/effect shape.
  const overlayEdge = projection.edges[0];
  assertEquals(overlayEdge.sourceNodeId, "n-button");
  assertEquals(overlayEdge.category, "overlay");
  assertEquals(overlayEdge.targetKind, "node");
  assertEquals(overlayEdge.targetRef, "n-modal");
  assertEquals(overlayEdge.effect, "openModal(open)");

  const externalEdge = projection.edges[1];
  assertEquals(externalEdge.category, "external_port");
  assertEquals(externalEdge.targetRef, "external-port:access_port:port-1");

  const instanceEdge = projection.edges[2];
  assertEquals(instanceEdge.category, "instance_operation");
  assertEquals(
    instanceEdge.targetRef,
    "instance-port:db_instance_port:inst-1:op-1",
  );

  // Rebuilding from the same authority yields the same projection (rehydrate).
  assertEquals(
    JSON.stringify(buildWiringGraphProjection(nodes)),
    JSON.stringify(projection),
  );
});

// ─── drag-drop wiring edit ───────────────────────────────────────────────────

Deno.test("drag-drop wiring edit: valid drop appends typed runtimeInteraction patch", () => {
  const nodes = fixtureNodes();
  const result = applyWiringDropEdit(nodes, "n-input", "n-modal");
  if (!result.ok) throw new Error(`expected ok, got: ${result.error}`);
  assertEquals(result.added, {
    trigger: "click",
    actionType: "openModal",
    targetNodeId: "n-modal",
    statePath: "open",
  });
  const patched = result.nodes.find((n) => n.nodeId === "n-input")!;
  assertEquals(patched.runtimeInteractions!.length, 2);
  assertEquals(patched.runtimeInteractions![1], result.added);
  // Input draft is not mutated (draft edit is a new array; undo-friendly).
  assertEquals(
    nodes.find((n) => n.nodeId === "n-input")!.runtimeInteractions!.length,
    1,
  );
});

Deno.test("drag-drop wiring edit: invalid drops fail close with explicit error and no draft mutation", () => {
  const nodes = fixtureNodes();
  const before = JSON.stringify(nodes);

  const selfDrop = applyWiringDropEdit(nodes, "n-button", "n-button");
  assertEquals(selfDrop.ok, false);
  if (!selfDrop.ok) {
    assertEquals(selfDrop.error.startsWith("WIRING_DROP_SELF_TARGET"), true);
  }

  const unknownSource = applyWiringDropEdit(nodes, "n-missing", "n-modal");
  assertEquals(unknownSource.ok, false);
  if (!unknownSource.ok) {
    assertEquals(
      unknownSource.error.startsWith("WIRING_DROP_SOURCE_NOT_FOUND"),
      true,
    );
  }

  const unknownTarget = applyWiringDropEdit(nodes, "n-button", "n-missing");
  assertEquals(unknownTarget.ok, false);
  if (!unknownTarget.ok) {
    assertEquals(
      unknownTarget.error.startsWith("WIRING_DROP_TARGET_NOT_FOUND"),
      true,
    );
  }

  const nonWirable = applyWiringDropEdit(nodes, "n-button", "n-input");
  assertEquals(nonWirable.ok, false);
  if (!nonWirable.ok) {
    assertEquals(
      nonWirable.error.startsWith("WIRING_DROP_TARGET_NOT_WIRABLE"),
      true,
    );
  }

  assertEquals(JSON.stringify(nodes), before);
});

// ─── lifecycle / high-frequency policy (fail close) ──────────────────────────

Deno.test("policy: high-frequency trigger + external dispatch without debounceMs fails close", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "form_input/text",
    runtimeInteractions: [{
      trigger: "input",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assertEquals(
    errors[0].includes("HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE"),
    true,
  );

  // debounceMs (positive integer) satisfies the policy.
  nodes[0].runtimeInteractions![0].debounceMs = 300;
  assertEquals(findRuntimeInteractionPolicyErrors(nodes), []);
});

Deno.test("policy: lifecycle trigger + dispatch without explicit confirmation fails close", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "load",
      actionType: "dispatchInstanceOperation",
      instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assertEquals(
    errors[0].includes("LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION"),
    true,
  );

  nodes[0].runtimeInteractions![0].lifecycleDispatchConfirmed = true;
  assertEquals(findRuntimeInteractionPolicyErrors(nodes), []);
});

Deno.test("policy: trigger outside SSOT vocabulary fails close", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "dblclick",
      actionType: "openModal",
      targetNodeId: "n2",
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assertEquals(errors[0].includes("TRIGGER_OUTSIDE_VOCABULARY"), true);
});

Deno.test("policy: local UI state mutation on high-frequency trigger is allowed by default", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "hover_start",
      actionType: "openDrawer",
      targetNodeId: "n2",
      statePath: "open",
    }],
  }];
  assertEquals(findRuntimeInteractionPolicyErrors(nodes), []);
});

// ─── preview inert boundary ──────────────────────────────────────────────────

Deno.test("preview inert: lifecycle triggers and dispatch actions are inert in preview", () => {
  assertEquals(
    isPreviewInertInteraction({
      trigger: "load",
      actionType: "openModal",
      targetNodeId: "n2",
    }),
    true,
  );
  assertEquals(
    isPreviewInertInteraction({
      trigger: "click",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
    }),
    true,
  );
  assertEquals(
    isPreviewInertInteraction({
      trigger: "click",
      actionType: "openModal",
      targetNodeId: "n2",
    }),
    false,
  );
  const projection = buildWiringGraphProjection(fixtureNodes());
  assertEquals(projection.edges[0].previewInert, false);
  assertEquals(projection.edges[1].previewInert, true);
});
