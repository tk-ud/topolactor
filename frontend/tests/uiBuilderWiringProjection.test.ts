/**
 * SSOT-conformance proof for docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * (design authority). This proof asserts the SSOT trigger vocabulary and setting
 * category taxonomy, and fails if implementation collapses categories. The audit
 * report is the revision basis for that SSOT vocabulary, not the proof target.
 *
 * required_proof coverage:
 * - runtimeInteractions -> wiring projection round-trip (view only; rehydrate)
 * - valid / invalid drag-drop wiring edit (typed patch / explicit error, no mutation)
 * - lifecycle dispatch without confirmation or idempotency policy fails close
 * - high-frequency dispatch without debounceMs fails close
 * - trigger outside SSOT vocabulary (including load / loaded) fails close
 * - actionType outside taxonomy fails close (no catch-all)
 * - setting category taxonomy = UI監視割当 / UI状態更新 / 副作用設定 / 内部API /
 *   外部API連携 / 外部インスタンス連携 exactly
 * - direct / detectable indirect side-effect loops fail close (debounce is not proof)
 * - selectable write targets exclude dependency_closure(trigger_source)
 * - UI監視割当 declarations are distinct from UI状態更新 mutations
 * - lifecycle / dispatch interactions are preview-inert
 */

import { assert, assertEquals } from "jsr:@std/assert";
import {
  ALL_WIRING_TRIGGERS,
  appendResolvedPayloadToIdempotencyKey,
  applyWiringDropEdit,
  buildWiringGraphProjection,
  classifyTrigger,
  computeDispatchIdempotencyKey,
  dependencyClosureOfTriggerSource,
  deriveUiWatchBindings,
  findRuntimeInteractionPolicyErrors,
  findSideEffectCycleErrors,
  HIGH_FREQUENCY_TRIGGERS,
  isBackendOrExternalDispatchAction,
  isHighFrequencyTrigger,
  isLifecycleTrigger,
  isPreviewInertInteraction,
  isValidDebounceMs,
  LIFECYCLE_IDEMPOTENCY_POLICIES,
  selectableWriteTargets,
  TRIGGER_VOCABULARY,
  WIRING_SETTING_CATEGORIES,
  type WiringNode,
  wiringSettingCategoryOf,
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
      stateJson: JSON.stringify({ open: false }),
    },
    {
      nodeId: "n-input",
      componentKey: "form_input/text",
      componentKind: "form_input/text",
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "dispatchInstanceOperation",
          instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
          sideEffectNone: true,
        },
      ],
    },
  ];
}

// ─── trigger vocabulary（SSOT trigger_vocabulary） ───────────────────────────

Deno.test("trigger vocabulary: SSOT groups; initial_mount is lifecycle; load is outside", () => {
  // lifecycle = runtime synthetic triggers only.
  assertEquals(classifyTrigger("initial_mount"), "lifecycle");
  assertEquals(classifyTrigger("route_enter"), "lifecycle");
  assertEquals(classifyTrigger("initial_display"), "lifecycle");
  // click belongs to pointer in the SSOT vocabulary.
  assertEquals(classifyTrigger("click"), "pointer");
  assertEquals(classifyTrigger("mouseon"), "pointer");
  assertEquals(classifyTrigger("keydown"), "keyboard");
  assertEquals(classifyTrigger("change"), "form");
  // load / loaded / DOM onLoad are NOT lifecycle vocabulary (fail close).
  assertEquals(classifyTrigger("load"), null);
  assertEquals(classifyTrigger("loaded"), null);
  assertEquals(classifyTrigger("onLoad"), null);
  assertEquals(classifyTrigger("not_a_trigger"), null);
  // ALL_WIRING_TRIGGERS is exactly the union of the SSOT groups.
  const union = Object.values(TRIGGER_VOCABULARY).flat();
  assertEquals([...ALL_WIRING_TRIGGERS].sort(), [...union].sort());
});

Deno.test("trigger classification: lifecycle and high-frequency sets", () => {
  assertEquals(isLifecycleTrigger("initial_mount"), true);
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

// ─── setting category taxonomy（catch-all拒否） ──────────────────────────────

Deno.test("setting category taxonomy: exactly the six SSOT categories; no legacy catch-all", () => {
  assertEquals([...WIRING_SETTING_CATEGORIES].sort(), [
    "external_api_integration",
    "external_instance_integration",
    "internal_api",
    "side_effect_setting",
    "ui_state_update",
    "ui_watch_binding",
  ]);
  // No implementation-derived catch-all identifiers in the taxonomy.
  for (const c of WIRING_SETTING_CATEGORIES) {
    assert(
      c !== ("legacy" as string),
      "legacy must not be a taxonomy category",
    );
    assert(
      c !== ("overlay" as string),
      "implementation id must not be taxonomy",
    );
  }
  // UI状態更新 = declared-slot mutations; 外部API連携 / 外部インスタンス連携 = typed dispatch.
  assertEquals(
    wiringSettingCategoryOf({ actionType: "openModal" }),
    "ui_state_update",
  );
  assertEquals(
    wiringSettingCategoryOf({ actionType: "setState" }),
    "ui_state_update",
  );
  assertEquals(
    wiringSettingCategoryOf({ actionType: "dispatchExternalPort" }),
    "external_api_integration",
  );
  assertEquals(
    wiringSettingCategoryOf({ actionType: "dispatchInstanceOperation" }),
    "external_instance_integration",
  );
  // Unknown actionType classifies as null (fails close) — never rounded into a bucket.
  assertEquals(wiringSettingCategoryOf({ actionType: "somethingElse" }), null);
});

Deno.test("policy: actionType outside taxonomy fails close (no catch-all rounding)", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{ trigger: "click", actionType: "mysteryAction" }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("ACTION_OUTSIDE_VOCABULARY"));
});

// ─── action authority vs effect fields (PR #599 review round 6): actionType is a
// closed-vocabulary action AUTHORITY matched by exact identity only; payloadFrom/
// outputProp are effect DATA and never promote an unrecognized actionType past
// ACTION_OUTSIDE_VOCABULARY. Round 5's field-presence fallback (classifying ANY
// actionType carrying payloadFrom/outputProp as side_effect_setting) conflated the
// two and is reverted — admin_runtime payload binding data now lives OUTSIDE
// runtimeInteractions entirely (renderEmission.ts node.dispatchPayloadFromByTrigger),
// so it is never authored as a runtimeInteractions[] entry at all. ─────────────────

Deno.test("wiringSettingCategoryOf: an unknown actionType classifies as null even when it carries payloadFrom", () => {
  assertEquals(
    wiringSettingCategoryOf({ actionType: "somethingUnknown" }),
    null,
  );
});

Deno.test("policy: an unknown actionType + payloadFrom still fails ACTION_OUTSIDE_VOCABULARY (effect fields are not an authority substitute)", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "click",
      actionType: "somethingUnknown",
      payloadFrom: { groupName: "node:name_input.value" },
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("ACTION_OUTSIDE_VOCABULARY"));
});

Deno.test("policy: an unknown actionType + outputProp still fails ACTION_OUTSIDE_VOCABULARY (effect fields are not an authority substitute)", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "click",
      actionType: "somethingUnknown",
      outputProp: "result",
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("ACTION_OUTSIDE_VOCABULARY"));
});

Deno.test("policy: bindRuntimeDispatchPayload (a stray leftover actionType, no longer meaningful) still fails ACTION_OUTSIDE_VOCABULARY like any other unknown actionType", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "click",
      actionType: "bindRuntimeDispatchPayload",
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("ACTION_OUTSIDE_VOCABULARY"));
});

// ─── UI監視割当（宣言）と UI状態更新（更新）の分離 ───────────────────────────

Deno.test("UI監視割当: declarations derive from state slots and are distinct from mutations", () => {
  const modal = fixtureNodes()[1];
  const bindings = deriveUiWatchBindings(modal);
  assertEquals(bindings, [{
    kind: "declaration",
    nodeId: "n-modal",
    stateKey: "open",
    initialValue: false,
  }]);
  // Declarations are a separate category from mutations: a declaration entry is
  // kind:"declaration", while a statePath write classifies as ui_state_update.
  assertEquals(
    wiringSettingCategoryOf({ actionType: "openModal" }),
    "ui_state_update",
  );
  // stateJson-less node yields no declarations — presence of propsJson/stateJson
  // elsewhere is never treated as taxonomy-complete proof.
  assertEquals(deriveUiWatchBindings({ nodeId: "x" }), []);
  assertEquals(
    deriveUiWatchBindings({ nodeId: "x", stateJson: "not json" }),
    [],
  );
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
  // UI監視割当 declarations are projected alongside edges.
  assertEquals(projection.watchBindings.length, 1);
  assertEquals(projection.watchBindings[0].stateKey, "open");

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
  const stateUpdateEdge = projection.edges[0];
  assertEquals(stateUpdateEdge.sourceNodeId, "n-button");
  assertEquals(stateUpdateEdge.category, "ui_state_update");
  assertEquals(stateUpdateEdge.targetKind, "node");
  assertEquals(stateUpdateEdge.targetRef, "n-modal");
  assertEquals(stateUpdateEdge.effect, "openModal(open)");
  assertEquals(stateUpdateEdge.hasSideEffect, false);

  const externalApiEdge = projection.edges[1];
  assertEquals(externalApiEdge.category, "external_api_integration");
  assertEquals(externalApiEdge.targetRef, "external-port:access_port:port-1");
  // 副作用設定 fields (payloadFrom / outputProp) surface as effect metadata.
  assertEquals(externalApiEdge.hasSideEffect, true);

  const externalInstanceEdge = projection.edges[2];
  assertEquals(externalInstanceEdge.category, "external_instance_integration");
  assertEquals(
    externalInstanceEdge.targetRef,
    "instance-port:db_instance_port:inst-1:op-1",
  );
  // Explicit no-side-effect selection keeps effect metadata false.
  assertEquals(externalInstanceEdge.hasSideEffect, false);

  // Rebuilding from the same authority yields the same projection (rehydrate).
  assertEquals(
    JSON.stringify(buildWiringGraphProjection(nodes)),
    JSON.stringify(projection),
  );
});

Deno.test("wiring projection: 内部API package lane projects as internal_api category (view only)", () => {
  const nodes = fixtureNodes();
  const projection = buildWiringGraphProjection(nodes, [
    {
      wiringKey: "employees_search",
      targetRef: "manifest:abc:employees_search",
    },
  ]);
  const internal = projection.edges.find((e) => e.category === "internal_api");
  assert(internal, "internal_api edge must be projected inside the taxonomy");
  assertEquals(internal!.targetKind, "internal_api");
  assertEquals(internal!.targetRef, "manifest:abc:employees_search");
  // Projection only: the package-lane edge carries no persistence index.
  assertEquals(internal!.interactionIndex, -1);
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
    assert(selfDrop.error.startsWith("WIRING_DROP_SELF_TARGET"));
  }

  const unknownSource = applyWiringDropEdit(nodes, "n-missing", "n-modal");
  assertEquals(unknownSource.ok, false);
  if (!unknownSource.ok) {
    assert(unknownSource.error.startsWith("WIRING_DROP_SOURCE_NOT_FOUND"));
  }

  const unknownTarget = applyWiringDropEdit(nodes, "n-button", "n-missing");
  assertEquals(unknownTarget.ok, false);
  if (!unknownTarget.ok) {
    assert(unknownTarget.error.startsWith("WIRING_DROP_TARGET_NOT_FOUND"));
  }

  const nonWirable = applyWiringDropEdit(nodes, "n-button", "n-input");
  assertEquals(nonWirable.ok, false);
  if (!nonWirable.ok) {
    assert(nonWirable.error.startsWith("WIRING_DROP_TARGET_NOT_WIRABLE"));
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
      sideEffectNone: true,
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE"));

  // debounceMs (positive integer) satisfies the high-frequency policy.
  nodes[0].runtimeInteractions![0].debounceMs = 300;
  assertEquals(findRuntimeInteractionPolicyErrors(nodes), []);
});

Deno.test("policy: lifecycle (initial_mount) dispatch requires explicit confirmation AND idempotency policy", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n1",
    componentKey: "action/button",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchInstanceOperation",
      instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
      sideEffectNone: true,
    }],
  }];
  const errors = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(errors.length, 2);
  assert(
    errors.some((e) => e.includes("LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION")),
  );
  assert(
    errors.some((e) =>
      e.includes("LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY")
    ),
  );

  // Confirmation alone is not enough — idempotency policy is independent.
  nodes[0].runtimeInteractions![0].lifecycleDispatchConfirmed = true;
  const stillMissing = findRuntimeInteractionPolicyErrors(nodes);
  assertEquals(stillMissing.length, 1);
  assert(
    stillMissing[0].includes("LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY"),
  );

  // initial_mount must not re-dispatch on rerender: once_per_mount is a valid policy.
  assert(LIFECYCLE_IDEMPOTENCY_POLICIES.includes("once_per_mount"));
  nodes[0].runtimeInteractions![0].idempotencyPolicy = "once_per_mount";
  assertEquals(findRuntimeInteractionPolicyErrors(nodes), []);

  // An unknown idempotency policy value fails close.
  nodes[0].runtimeInteractions![0].idempotencyPolicy = "whenever";
  assert(
    findRuntimeInteractionPolicyErrors(nodes)[0]
      .includes("LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY"),
  );
});

Deno.test("policy: trigger outside SSOT vocabulary fails close (load / loaded included)", () => {
  for (const trigger of ["dblclick", "load", "loaded"]) {
    const nodes: WiringNode[] = [{
      nodeId: "n1",
      componentKey: "action/button",
      runtimeInteractions: [{
        trigger,
        actionType: "openModal",
        targetNodeId: "n2",
      }],
    }];
    const errors = findRuntimeInteractionPolicyErrors(nodes);
    assertEquals(errors.length, 1, trigger);
    assert(errors[0].includes("TRIGGER_OUTSIDE_VOCABULARY"), trigger);
  }
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

// ─── side-effect cycle policy（fail close; debounce は loop-safety proof ではない） ──

Deno.test("side-effect cycle policy: direct self-loop (watched A -> writes A) fails close", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n-a",
    componentKey: "form_input/text",
    runtimeInteractions: [{
      trigger: "change",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
      outputProp: "value",
      debounceMs: 500,
    }],
  }];
  // debounceMs is present — it must NOT clear the loop error (not loop-safety proof).
  const cycleErrors = findSideEffectCycleErrors(nodes);
  assertEquals(cycleErrors.length, 1);
  assert(cycleErrors[0].includes("SIDE_EFFECT_DIRECT_SELF_LOOP"));
  assert(
    findRuntimeInteractionPolicyErrors(nodes).some((e) =>
      e.includes("SIDE_EFFECT_DIRECT_SELF_LOOP")
    ),
    "cycle errors must block the same validate/apply path",
  );
});

Deno.test("side-effect cycle policy: detectable indirect loop (A -> B, B -> A) fails close", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "n-a",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "setState",
        targetNodeId: "n-b",
        statePath: "open",
      }],
    },
    {
      nodeId: "n-b",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "setState",
        targetNodeId: "n-a",
        statePath: "open",
      }],
    },
  ];
  const errors = findSideEffectCycleErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("SIDE_EFFECT_INDIRECT_LOOP"));
});

Deno.test("side-effect cycle policy: selectable_write_targets excludes dependency_closure(trigger_source)", () => {
  const nodes: WiringNode[] = [
    // n-b writes into n-a on value change: writing to n-b from an n-a-triggered
    // effect would loop, so n-b is inside dependency_closure(n-a).
    {
      nodeId: "n-b",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "setState",
        targetNodeId: "n-a",
        statePath: "open",
      }],
    },
    { nodeId: "n-a", componentKey: "form_input/text" },
    {
      nodeId: "n-c",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
    },
  ];
  const closure = dependencyClosureOfTriggerSource(nodes, "n-a");
  assert(closure.has("n-a"), "trigger source itself is always excluded");
  assert(
    closure.has("n-b"),
    "node writing into trigger source is in the closure",
  );
  assertEquals(selectableWriteTargets(nodes, "n-a"), ["n-c"]);
});

// ─── side-effect cycle policy: localStateMutation targetRef writes (round-7 gap) ──
//
// A localStateMutation entry authors its write target via `targetRef`
// ("ui-local:<nodeId>.<stateKey>"), never a separate `targetNodeId` field (SSOT
// wiring_lane_contract.lanes.internal_instance_wiring / react-schema-topology-seed-
// translator-ssot.yaml). The dependency graph this policy derives from
// runtimeInteractions must see a targetRef write exactly as it sees a
// targetNodeId write — the SAME resolved target node either way — since
// resolveUiStateUpdateMutation (runtime/uiEventEffectRunner.ts) treats them
// identically at write time. Before this fix, interactionWriteTargets only
// recognized targetNodeId-shaped writes, so a direct or indirect loop authored
// entirely through targetRef went undetected by both the authoring candidate
// filter and this fail-close gate.

Deno.test("side-effect cycle policy: a localStateMutation targetRef direct self-loop (watched A -> targetRef ui-local:A.x) fails close", () => {
  const nodes: WiringNode[] = [{
    nodeId: "n-a",
    componentKey: "form_input/text",
    runtimeInteractions: [{
      trigger: "change",
      actionType: "localStateMutation",
      targetRef: "ui-local:n-a.flag",
    }],
  }];
  const cycleErrors = findSideEffectCycleErrors(nodes);
  assertEquals(cycleErrors.length, 1);
  assert(cycleErrors[0].includes("SIDE_EFFECT_DIRECT_SELF_LOOP"));
  assert(
    findRuntimeInteractionPolicyErrors(nodes).some((e) =>
      e.includes("SIDE_EFFECT_DIRECT_SELF_LOOP")
    ),
    "cycle errors must block the same validate/apply path",
  );
});

Deno.test("side-effect cycle policy: an indirect loop through a targetRef write (A -> B via targetRef, B -> A via targetNodeId) fails close", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "n-a",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "localStateMutation",
        targetRef: "ui-local:n-b.flag",
      }],
    },
    {
      nodeId: "n-b",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "setState",
        targetNodeId: "n-a",
        statePath: "open",
      }],
    },
  ];
  const errors = findSideEffectCycleErrors(nodes);
  assertEquals(errors.length, 1);
  assert(errors[0].includes("SIDE_EFFECT_INDIRECT_LOOP"));
});

Deno.test("side-effect cycle policy: selectable_write_targets excludes dependency_closure derived through a targetRef write edge", () => {
  const nodes: WiringNode[] = [
    // n-b writes into n-a via a localStateMutation targetRef (not targetNodeId) on
    // value change: writing to n-b from an n-a-triggered effect would loop, so n-b
    // must be inside dependency_closure(n-a) exactly as a targetNodeId write would be.
    {
      nodeId: "n-b",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "localStateMutation",
        targetRef: "ui-local:n-a.open",
      }],
    },
    { nodeId: "n-a", componentKey: "form_input/text" },
    {
      nodeId: "n-c",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
    },
  ];
  const closure = dependencyClosureOfTriggerSource(nodes, "n-a");
  assert(
    closure.has("n-b"),
    "node writing into trigger source via targetRef is in the closure, same as a targetNodeId write",
  );
  assertEquals(selectableWriteTargets(nodes, "n-a"), ["n-c"]);
});

// ─── preview inert boundary ──────────────────────────────────────────────────

Deno.test("preview inert: lifecycle triggers and dispatch actions are inert in preview", () => {
  // initial_mount is runtime synthetic — inert in preview, never DOM onLoad.
  assertEquals(
    isPreviewInertInteraction({
      trigger: "initial_mount",
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

// ─── retry_safe_dispatch_idempotency: computeDispatchIdempotencyKey ──────────

Deno.test("isValidDebounceMs: rejects non-positive-integer and accepts positive integers", () => {
  assertEquals(isValidDebounceMs(500), true);
  assertEquals(isValidDebounceMs(1), true);
  assertEquals(isValidDebounceMs(0), false);
  assertEquals(isValidDebounceMs(-1), false);
  assertEquals(isValidDebounceMs(1.5), false);
  assertEquals(isValidDebounceMs("500"), false);
  assertEquals(isValidDebounceMs(undefined), false);
});

Deno.test("computeDispatchIdempotencyKey: identical identity produces the identical key (stable across reload/reconnect/runner recreation)", () => {
  const input = {
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-fetch",
    interactionIndex: 0,
    trigger: "initial_mount",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  };
  const keyA = computeDispatchIdempotencyKey(input);
  const keyB = computeDispatchIdempotencyKey({ ...input });
  assertEquals(
    keyA,
    keyB,
    "the SAME authored interaction identity must always produce the SAME key — a fresh random value per call would defeat retry-safety",
  );
});

Deno.test("computeDispatchIdempotencyKey: distinct identity (nodeId / interactionIndex / trigger / actionType / targetRef) produces distinct keys", () => {
  const base = {
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-fetch",
    interactionIndex: 0,
    trigger: "initial_mount",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  };
  const baseline = computeDispatchIdempotencyKey(base);
  assert(
    computeDispatchIdempotencyKey({ ...base, nodeId: "n-other" }) !== baseline,
  );
  assert(
    computeDispatchIdempotencyKey({ ...base, interactionIndex: 1 }) !==
      baseline,
  );
  assert(
    computeDispatchIdempotencyKey({ ...base, trigger: "route_enter" }) !==
      baseline,
  );
  assert(
    computeDispatchIdempotencyKey({
      ...base,
      actionType: "dispatchInstanceOperation",
    }) !== baseline,
  );
  assert(
    computeDispatchIdempotencyKey({
      ...base,
      targetRef: "external-port:access_port:port-2",
    }) !== baseline,
  );
});

Deno.test("appendResolvedPayloadToIdempotencyKey: distinct resolved payload content produces distinct keys from the same identity base", () => {
  const base = computeDispatchIdempotencyKey({
    layoutId: "layout-1",
    nodeId: "n-button",
    interactionIndex: 0,
    trigger: "click",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  });
  const keyWithPayloadA = appendResolvedPayloadToIdempotencyKey(base, {
    subject: "hello",
  });
  const keyWithPayloadB = appendResolvedPayloadToIdempotencyKey(base, {
    subject: "goodbye",
  });
  assert(
    keyWithPayloadA !== keyWithPayloadB,
    "two distinct user actions with different resolved payload content on the same button must not collide on the same idempotency key",
  );
  // Same payload, regardless of key insertion order, produces the same key
  // (order-independent canonicalization) — this is what makes the key stable
  // across independently-rebuilt bindings.
  const keyReordered = appendResolvedPayloadToIdempotencyKey(base, {
    subject: "hello",
  });
  assertEquals(keyWithPayloadA, keyReordered);
});

// ─── projection_authority_runtime_interaction_identity: runtimeInteractionId ──
// PR577 follow-up implementation. SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
// lifecycle_policy.projection_authority_runtime_interaction_identity.

Deno.test("computeDispatchIdempotencyKey: absent runtimeInteractionId is byte-identical to the pre-existing nodeId+interactionIndex formula", () => {
  const input = {
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-fetch",
    interactionIndex: 2,
    trigger: "initial_mount",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  };
  const withoutIdField = computeDispatchIdempotencyKey(input);
  const withExplicitUndefined = computeDispatchIdempotencyKey({
    ...input,
    runtimeInteractionId: undefined,
  });
  assertEquals(
    withoutIdField,
    withExplicitUndefined,
    "absent runtimeInteractionId must not change the key format from before this field existed (backward-compatible structural fallback)",
  );
});

Deno.test("computeDispatchIdempotencyKey: present runtimeInteractionId REPLACES nodeId+interactionIndex as the identity component", () => {
  const base = {
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-fetch",
    interactionIndex: 0,
    trigger: "initial_mount",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  };
  const withoutId = computeDispatchIdempotencyKey(base);
  const withId = computeDispatchIdempotencyKey({
    ...base,
    runtimeInteractionId: "aaaaaaaa-0000-0000-0000-000000000001",
  });
  assert(
    withoutId !== withId,
    "an assigned runtimeInteractionId must change the computed key relative to the structural-only fallback",
  );
});

Deno.test("computeDispatchIdempotencyKey: runtimeInteractionId survives reordering — same id produces the same key even when nodeId/interactionIndex change", () => {
  const before = computeDispatchIdempotencyKey({
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-a",
    interactionIndex: 0,
    runtimeInteractionId: "aaaaaaaa-0000-0000-0000-000000000001",
    trigger: "click",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  });
  // Simulates the SAME authored interaction after the author reordered
  // runtimeInteractions on the node (interactionIndex changed) or the node was
  // renamed (nodeId changed) — the stable id must produce the SAME key,
  // unlike the pre-existing positional fallback.
  const afterReorder = computeDispatchIdempotencyKey({
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-a-renamed",
    interactionIndex: 3,
    runtimeInteractionId: "aaaaaaaa-0000-0000-0000-000000000001",
    trigger: "click",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  });
  assertEquals(
    before,
    afterReorder,
    "the SAME runtimeInteractionId must survive nodeId/interactionIndex churn — this is the whole point of the stable identity replacing the positional fallback",
  );
});

Deno.test("computeDispatchIdempotencyKey: distinct runtimeInteractionId values produce distinct keys even with identical nodeId/interactionIndex", () => {
  const shared = {
    layoutId: "layout-1",
    packageId: "pkg-1",
    nodeId: "n-a",
    interactionIndex: 0,
    trigger: "click",
    actionType: "dispatchExternalPort",
    targetRef: "external-port:access_port:port-1",
  };
  const keyA = computeDispatchIdempotencyKey({
    ...shared,
    runtimeInteractionId: "aaaaaaaa-0000-0000-0000-000000000001",
  });
  const keyB = computeDispatchIdempotencyKey({
    ...shared,
    runtimeInteractionId: "bbbbbbbb-0000-0000-0000-000000000002",
  });
  assert(
    keyA !== keyB,
    "two distinct authored interactions must never collide on the same key merely because they share a nodeId/interactionIndex position",
  );
});
