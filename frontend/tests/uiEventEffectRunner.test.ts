/**
 * Proof surface for the runtime state/effect runner boundary.
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * component_runtime_state_effect_boundary / lifecycle_policy / side_effect_cycle_policy
 *
 * Proves (runtime side, complementing the authoring gates):
 * - initial_mount is a runtime synthetic lifecycle trigger emitted once by the
 *   runner (not DOM onLoad); idempotencyPolicy prevents re-dispatch on rerender
 * - preview keeps the lifecycle/effect runner inert
 * - UI監視割当 declared state slots connect to the runtime store BEFORE
 *   mutation/effect; undeclared-slot writes fail close in the state dispatcher
 * - UI状態更新 executes only through the runtime state dispatcher
 * - 副作用設定 executes through the loop guard: a dependency graph with a
 *   direct/indirect loop fails close at runtime too (debounce is not proof)
 * - 外部API連携 / 外部インスタンス連携 dispatch rides the injected dispatch lanes
 *   with typed references; unconfirmed lifecycle dispatch never executes
 * - enqueueInstanceOperationDispatchCommand rejects non instance-port refs
 */

import { assert, assertEquals } from "jsr:@std/assert";
import {
  applyGuardedLocalStateMutation,
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
  createRuntimeStateDispatcher,
  createUiEventEffectRunner,
  type NotifyingRuntimeLocalStateStore,
  parseUiLocalTargetRef,
  predeclareProjectionState,
  resolveUiStateUpdateMutation,
  resolveUiStateUpdateMutationValue,
} from "../runtime/uiEventEffectRunner.ts";
import { enqueueInstanceOperationDispatchCommand } from "../runtime/frontendScheduler.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { createLiveNodeValueTracker } from "../runtime/liveNodeValueTracker.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import {
  computeDispatchIdempotencyKey,
  type WiringNode,
} from "../lib/uiBuilderWiringProjection.ts";

function modalNodes(): WiringNode[] {
  return [
    {
      nodeId: "n-banner",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
      stateJson: JSON.stringify({ open: false }),
    },
    {
      nodeId: "n-root",
      componentKey: "layout/box",
      componentKind: "layout/box",
      runtimeInteractions: [
        {
          trigger: "initial_mount",
          actionType: "openModal",
          targetNodeId: "n-banner",
          statePath: "open",
        },
      ],
    },
  ];
}

Deno.test("parseUiLocalTargetRef: parses 'ui-local:<nodeId>.<stateKey>' into {targetNodeId, statePath}", () => {
  assertEquals(
    parseUiLocalTargetRef(
      "ui-local:instance_settings_import_form.template_download_trigger",
    ),
    {
      targetNodeId: "instance_settings_import_form",
      statePath: "template_download_trigger",
    },
  );
});

Deno.test("parseUiLocalTargetRef: malformed/non-matching/absent targetRef resolves to undefined — never guessed", () => {
  assertEquals(parseUiLocalTargetRef(undefined), undefined);
  assertEquals(parseUiLocalTargetRef(""), undefined);
  assertEquals(parseUiLocalTargetRef("not-ui-local-shaped"), undefined);
  assertEquals(
    parseUiLocalTargetRef("ui-local:missing-dot-separator"),
    undefined,
  );
});

Deno.test("resolveUiStateUpdateMutation: localStateMutation resolves targetNodeId/statePath from targetRef (no separate targetNodeId/statePath fields) and writes true — SSOT wiring_lane_contract.lanes.internal_instance_wiring", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "localStateMutation",
    targetRef:
      "ui-local:instance_settings_import_form.template_download_trigger",
  });
  assertEquals(resolved, {
    targetNodeId: "instance_settings_import_form",
    statePath: "template_download_trigger",
    action: "set",
    value: true,
  });
});

Deno.test("resolveUiStateUpdateMutation: an explicit targetNodeId/statePath always wins over targetRef parsing", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "localStateMutation",
    targetNodeId: "explicit-node",
    statePath: "explicit-path",
    targetRef: "ui-local:other-node.other-path",
  });
  assertEquals(resolved?.targetNodeId, "explicit-node");
  assertEquals(resolved?.statePath, "explicit-path");
});

Deno.test("resolveUiStateUpdateMutation: localStateMutation with no targetNodeId and no targetRef resolves to null (nothing to mutate)", () => {
  assertEquals(
    resolveUiStateUpdateMutation({ actionType: "localStateMutation" }),
    null,
  );
});

Deno.test("localStateMutation end-to-end: predeclare (from targetRef) -> resolve -> guarded write -> real state change — matches manifest 092's json_template_download/json_import shape (target is the OWNING FORM's own node, a sibling of the Action, not the Action itself)", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "instance_settings_import_form",
      componentKey: "structural_node",
      componentKind: undefined,
    },
    {
      nodeId: "json_template_download",
      componentKey: "action/button",
      componentKind: "action/button",
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "localStateMutation",
          targetRef:
            "ui-local:instance_settings_import_form.template_download_trigger",
        },
      ],
    },
  ];
  const store = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(nodes, store);

  // Predeclared (from the targetRef-resolved identity) before any mutation can run.
  assert(
    dispatcher.isDeclared(
      "instance_settings_import_form",
      "template_download_trigger",
    ),
  );
  assertEquals(
    dispatcher.get(
      "instance_settings_import_form",
      "template_download_trigger",
    ),
    undefined,
  );

  const interaction = nodes[1].runtimeInteractions![0];
  const resolved = resolveUiStateUpdateMutation(interaction);
  assert(resolved, "localStateMutation must resolve via its targetRef");
  const applyResult = applyGuardedLocalStateMutation(dispatcher, resolved!);
  assert(
    applyResult.ok,
    `expected the guarded write to succeed; got: ${
      JSON.stringify(applyResult)
    }`,
  );
  assertEquals(
    dispatcher.get(
      "instance_settings_import_form",
      "template_download_trigger",
    ),
    true,
  );
});

Deno.test("predeclareProjectionState: a localStateMutation target that resolves via targetRef to a real node in the list IS predeclared (parity with targetNodeId-based UI状態更新 actions)", () => {
  const nodes: WiringNode[] = [
    { nodeId: "owning_form" },
    {
      nodeId: "trigger_action",
      runtimeInteractions: [
        {
          trigger: "click",
          actionType: "localStateMutation",
          targetRef: "ui-local:owning_form.some_flag",
        },
      ],
    },
  ];
  const dispatcher = createRuntimeStateDispatcher(
    createRuntimeLocalStateStore(),
  );
  const declared = predeclareProjectionState(nodes, dispatcher);
  assert(
    declared.some((s) =>
      s.nodeId === "owning_form" && s.stateKey === "some_flag"
    ),
    `expected owning_form.some_flag to be predeclared; got: ${
      JSON.stringify(declared)
    }`,
  );
});

Deno.test("runner: UI監視割当 slots are declared at creation, before any mutation/effect", () => {
  const runner = createUiEventEffectRunner({ nodes: modalNodes() });
  assertEquals(runner.declaredSlots, [{
    nodeId: "n-banner",
    stateKey: "open",
  }]);
  // Declaration connected the initial value to the runtime store.
  assertEquals(runner.stateDispatcher.get("n-banner", "open"), false);
});

Deno.test("state dispatcher: undeclared slot writes fail close; declared writes succeed", () => {
  const dispatcher = createRuntimeStateDispatcher(
    createRuntimeLocalStateStore(),
  );
  const refused = dispatcher.set("n-x", "open", true);
  assertEquals(refused.ok, false);
  if (!refused.ok) {
    assert(refused.error.includes("RUNTIME_STATE_SLOT_NOT_DECLARED"));
  }

  dispatcher.declare("n-x", "open", false);
  assertEquals(dispatcher.get("n-x", "open"), false);
  const accepted = dispatcher.set("n-x", "open", true);
  assertEquals(accepted.ok, true);
  assertEquals(dispatcher.get("n-x", "open"), true);
});

Deno.test("runner: initial_mount fires UI状態更新 once through the dispatcher; rerender does not re-fire", () => {
  const runner = createUiEventEffectRunner({ nodes: modalNodes() });
  const first = runner.emitLifecycle("initial_mount");
  assertEquals(first.ok, true);
  assertEquals(first.inert, false);
  assertEquals(first.executed, ["n-root#0"]);
  assertEquals(runner.stateDispatcher.get("n-banner", "open"), true);

  // Simulate rerender / SSE refresh: the fired-registry keeps it idempotent.
  runner.stateDispatcher.set("n-banner", "open", false);
  const again = runner.emitLifecycle("initial_mount");
  assertEquals(again.executed, []);
  assertEquals(runner.stateDispatcher.get("n-banner", "open"), false);
});

Deno.test("runner: preview mode keeps the lifecycle/effect runner inert", () => {
  const runner = createUiEventEffectRunner({
    nodes: modalNodes(),
    previewMode: true,
  });
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result, { ok: true, inert: true, executed: [], errors: [] });
  // No state writes happened.
  assertEquals(runner.stateDispatcher.get("n-banner", "open"), false);
});

Deno.test("runner: confirmed initial_mount dispatch executes once via injected lane; once_per_mount blocks re-dispatch", () => {
  const externalCalls: Array<{ portTargetRef: string }> = [];
  const nodes: WiringNode[] = [{
    nodeId: "n-root",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "once_per_mount",
      sideEffectNone: true,
    }],
  }];
  const runner = createUiEventEffectRunner({
    nodes,
    dispatchExternalPort: (spec) => externalCalls.push(spec),
  });
  assertEquals(runner.emitLifecycle("initial_mount").executed, ["n-root#0"]);
  assertEquals(externalCalls.length, 1);
  assertEquals(
    externalCalls[0].portTargetRef,
    "external-port:access_port:port-1",
  );
  // Rerender / repeated mount emission: no re-dispatch.
  assertEquals(runner.emitLifecycle("initial_mount").executed, []);
  assertEquals(runner.emitLifecycle("route_enter").executed, []);
  assertEquals(externalCalls.length, 1);
});

Deno.test("runner: refetch_on_route_enter re-dispatches on route_enter only", () => {
  const calls: string[] = [];
  const nodes: WiringNode[] = [{
    nodeId: "n-root",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "route_enter",
      actionType: "dispatchInstanceOperation",
      instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "refetch_on_route_enter",
      sideEffectNone: true,
    }],
  }];
  const runner = createUiEventEffectRunner({
    nodes,
    dispatchInstanceOperation: (spec) => calls.push(spec.instanceTargetRef),
  });
  assertEquals(runner.emitLifecycle("route_enter").executed, ["n-root#0"]);
  assertEquals(runner.emitLifecycle("route_enter").executed, ["n-root#0"]);
  assertEquals(calls, [
    "instance-port:db_instance_port:inst-1:op-1",
    "instance-port:db_instance_port:inst-1:op-1",
  ]);
});

Deno.test("runner: unconfirmed / non-idempotent lifecycle dispatch fails close and never executes", () => {
  const calls: unknown[] = [];
  const nodes: WiringNode[] = [{
    nodeId: "n-root",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
      sideEffectNone: true,
    }],
  }];
  const runner = createUiEventEffectRunner({
    nodes,
    dispatchExternalPort: (spec) => calls.push(spec),
  });
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.ok, false);
  assertEquals(result.executed, []);
  assertEquals(calls.length, 0);
  assert(
    result.errors.some((e) =>
      e.includes("LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION")
    ),
  );
  assert(
    result.errors.some((e) =>
      e.includes("LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY")
    ),
  );
});

Deno.test("runner: per-interaction fail-close correlation is by structural identity (nodeId+interactionIndex), never by display label — two nodes sharing the identical friendly label at the same interaction index never cross-contaminate", () => {
  const calls: unknown[] = [];
  // Both nodes resolve to the SAME wiringNodeDisplayLabel ("box", from
  // componentKey "layout/box"'s last path segment) and both author their
  // dispatch at runtimeInteractions index 0 — the exact shape a label-text
  // string-prefix correlation (e.g. `${label} #${idx+1}:`) cannot distinguish
  // between. n-invalid is missing confirmation/idempotency and must fail
  // close; n-valid is fully confirmed and must execute untouched by n-invalid's
  // violation.
  const nodes: WiringNode[] = [
    {
      nodeId: "n-invalid",
      componentKey: "layout/box",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-invalid",
        sideEffectNone: true,
      }],
    },
    {
      nodeId: "n-valid",
      componentKey: "layout/box",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-valid",
        lifecycleDispatchConfirmed: true,
        idempotencyPolicy: "once_per_mount",
        sideEffectNone: true,
      }],
    },
  ];
  const runner = createUiEventEffectRunner({
    nodes,
    dispatchExternalPort: (spec) => calls.push(spec),
  });
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.ok, false);
  // n-valid's dispatch executed despite sharing n-invalid's display label and
  // interaction index — never blocked by a same-label neighbor's violation.
  assertEquals(result.executed, ["n-valid#0"]);
  assertEquals(calls.length, 1);
  assertEquals(
    (calls[0] as { portTargetRef: string }).portTargetRef,
    "external-port:access_port:port-valid",
  );
  // Exactly n-invalid's two violations reported — not duplicated onto n-valid,
  // and n-valid contributes none of its own.
  assertEquals(result.errors.length, 2);
  assert(
    result.errors.every((e) => e.startsWith("box #1:")),
  );
  assert(
    result.errors.some((e) => e.includes("LIFECYCLE_DISPATCH_REQUIRES_CONFIRMATION")),
  );
  assert(
    result.errors.some((e) =>
      e.includes("LIFECYCLE_DISPATCH_REQUIRES_IDEMPOTENCY_POLICY")
    ),
  );
});

Deno.test("runner: loop guard fails close at runtime — cyclic graph executes nothing (debounce is not proof)", () => {
  const calls: unknown[] = [];
  const nodes: WiringNode[] = [
    {
      nodeId: "n-a",
      componentKey: "form_input/text",
      runtimeInteractions: [
        {
          trigger: "change",
          actionType: "dispatchExternalPort",
          portTargetRef: "external-port:access_port:port-1",
          outputProp: "value",
          debounceMs: 500,
        },
        {
          trigger: "initial_mount",
          actionType: "openModal",
          targetNodeId: "n-banner",
          statePath: "open",
        },
      ],
    },
    {
      nodeId: "n-banner",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
      stateJson: JSON.stringify({ open: false }),
    },
  ];
  const runner = createUiEventEffectRunner({
    nodes,
    dispatchExternalPort: (spec) => calls.push(spec),
  });
  assert(runner.cycleErrors.length > 0, "direct self-loop must be detected");
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.ok, false);
  assertEquals(result.executed, []);
  assert(result.errors.some((e) => e.includes("SIDE_EFFECT_DIRECT_SELF_LOOP")));
  assertEquals(calls.length, 0);
  // The blocked mutation never reached the store either.
  assertEquals(runner.stateDispatcher.get("n-banner", "open"), false);
});

Deno.test("instance dispatch lane: non instance-port target refs fail close before the command lane", async () => {
  const result = await enqueueInstanceOperationDispatchCommand({
    instanceTargetRef: "external-port:access_port:port-1",
    payload: {},
  });
  assertEquals(result.success, false);
  assertEquals(
    result.errors?.[0]?.code,
    "INSTANCE_OPERATION_TARGET_REF_INVALID",
  );
});

Deno.test("renderEmission: dispatchInstanceOperation projects an instanceOperationDispatch event binding", () => {
  const emission = {
    componentIds: [],
    data: {},
    layoutId: "layout-1",
    layoutNodes: [{
      nodeId: "n-1",
      componentId: "c-1",
      componentKey: "action/button",
      componentKind: "action/button",
      nodeKind: "catalog_component",
      slotKey: "main",
      orderIndex: 0,
      propsJson: JSON.stringify({ label: "実行" }),
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchInstanceOperation",
        instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
        payloadFrom: { amount: "event.value" },
        outputProp: "result",
      }],
    }],
    // deno-lint-ignore no-explicit-any
  } as any;
  type SpecWithRuntime = {
    runtimeSpec?: { eventBinding?: Record<string, unknown> };
  };
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertEquals(specs.length, 1);
  const binding = (specs[0] as SpecWithRuntime).runtimeSpec?.eventBinding
    ?.click as Record<string, unknown> | undefined;
  assert(binding, "click binding must exist");
  assertEquals(binding.instanceOperationDispatch, {
    instanceTargetRef: "instance-port:db_instance_port:inst-1:op-1",
    payloadFrom: { amount: "event.value" },
    outputProp: "result",
    debounceMs: undefined,
    idempotencyKeyBase: computeDispatchIdempotencyKey({
      layoutId: "layout-1",
      packageId: undefined,
      nodeId: "n-1",
      interactionIndex: 0,
      trigger: "click",
      actionType: "dispatchInstanceOperation",
      targetRef: "instance-port:db_instance_port:inst-1:op-1",
    }),
  });
  // Preview stays inert: the dispatch binding is not built in previewMode.
  const previewSpecs = renderEmission(emission, defaultComponentRegistry, {
    previewMode: true,
  });
  const previewBinding = (previewSpecs[0] as SpecWithRuntime).runtimeSpec
    ?.eventBinding
    ?.click as Record<string, unknown> | undefined;
  assertEquals(previewBinding?.instanceOperationDispatch, undefined);
});

// ─── store update -> projection rerender 接続（rendered props反映まで検査） ────

/** Shared fixture: modal node + root node wiring initial_mount -> openModal. */
function modalEmission(): Parameters<typeof renderEmission>[0] {
  return {
    componentIds: [],
    data: {},
    layoutId: "layout-1",
    layoutNodes: [
      {
        nodeId: "n-modal",
        componentId: "c-modal",
        componentKey: "disclosure/modal",
        componentKind: "disclosure/modal",
        nodeKind: "catalog_component",
        slotKey: "main",
        orderIndex: 0,
        propsJson: JSON.stringify({ title: "案内", open: false }),
        stateJson: JSON.stringify({ open: false }),
      },
      {
        nodeId: "n-root",
        componentId: "c-root",
        componentKey: "action/button",
        componentKind: "action/button",
        nodeKind: "catalog_component",
        slotKey: "main",
        orderIndex: 1,
        propsJson: JSON.stringify({ label: "開く" }),
        runtimeInteractions: [{
          trigger: "click",
          actionType: "openModal",
          targetNodeId: "n-modal",
          statePath: "open",
        }],
      },
    ],
    // deno-lint-ignore no-explicit-any
  } as any;
}

function runnerNodesFromEmission(): WiringNode[] {
  return [
    {
      nodeId: "n-modal",
      componentKey: "disclosure/modal",
      componentKind: "disclosure/modal",
      stateJson: JSON.stringify({ open: false }),
    },
    {
      nodeId: "n-root",
      componentKey: "action/button",
      componentKind: "action/button",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "openModal",
        targetNodeId: "n-modal",
        statePath: "open",
      }],
    },
  ];
}

function modalOpenProp(specs: ReturnType<typeof renderEmission>): unknown {
  const modal = specs.find((s) =>
    (s as { nodeId?: string }).nodeId === "n-modal"
  ) as { runtimeSpec?: { props?: { data?: Record<string, unknown> } } };
  return modal?.runtimeSpec?.props?.data?.open;
}

Deno.test("rerender接続: initial_mount UI状態更新 -> store notification -> rendered runtimeSpec props に反映", () => {
  const store: NotifyingRuntimeLocalStateStore = createRuntimeLocalStateStore();
  const emission = modalEmission();
  // ProjectionShell equivalent: dispatcher created (predeclared) before the
  // first render, and reused by both renderEmission and the runner.
  const dispatcher = createProjectionStateDispatcher(
    runnerNodesFromEmission(),
    store,
  );

  // ProjectionShell equivalent: render, subscribe (before lifecycle emission), emit.
  let specs = renderEmission(emission, defaultComponentRegistry, {
    localStateStore: dispatcher,
  });
  let notified = 0;
  store.subscribe(() => {
    notified++;
    specs = renderEmission(emission, defaultComponentRegistry, {
      localStateStore: dispatcher,
    });
  });
  const runner = createUiEventEffectRunner({
    nodes: runnerNodesFromEmission(),
    dispatcher,
  });
  assertEquals(modalOpenProp(specs), false);

  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.ok, true);
  assert(notified >= 1, "store update must notify the rerender hook");
  // Rendered runtimeSpec props — not just the store Map — reflect UI状態更新.
  assertEquals(modalOpenProp(specs), true);
});

Deno.test("rerender接続: notification再renderがあっても initial_mount dispatch は再実行されない", () => {
  const store = createRuntimeLocalStateStore();
  const externalCalls: unknown[] = [];
  const nodes: WiringNode[] = [
    ...runnerNodesFromEmission(),
    {
      nodeId: "n-fetch",
      componentKey: "layout/box",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-1",
        lifecycleDispatchConfirmed: true,
        idempotencyPolicy: "once_per_mount",
        sideEffectNone: true,
      }],
    },
  ];
  const runner = createUiEventEffectRunner({
    nodes,
    store,
    dispatchExternalPort: (spec) => externalCalls.push(spec),
  });
  // Simulate the rerender hook re-entering lifecycle emission on every store change
  // (worst case): fired-registry must still keep the dispatch single-shot.
  store.subscribe(() => {
    runner.emitLifecycle("initial_mount");
  });
  runner.emitLifecycle("initial_mount");
  assertEquals(externalCalls.length, 1);
  assertEquals(runner.stateDispatcher.get("n-modal", "open"), true);
});

Deno.test("rerender接続: previewMode では store更新も notification も起きない", () => {
  const store = createRuntimeLocalStateStore();
  let notified = 0;
  store.subscribe(() => notified++);
  const runner = createUiEventEffectRunner({
    nodes: runnerNodesFromEmission(),
    store,
    previewMode: true,
  });
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.inert, true);
  // Watch-binding declaration wrote only the initial value; the inert emission
  // must not mutate. Reset counter after creation-time declaration writes:
  const afterCreation = notified;
  runner.emitLifecycle("initial_mount");
  assertEquals(notified, afterCreation);
  assertEquals(runner.stateDispatcher.get("n-modal", "open"), false);
});

Deno.test("rerender接続: loop guard 時は store更新も notification も起きない", () => {
  const store = createRuntimeLocalStateStore();
  const nodes: WiringNode[] = [
    ...runnerNodesFromEmission(),
    {
      nodeId: "n-loop",
      componentKey: "form_input/text",
      runtimeInteractions: [{
        trigger: "change",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-1",
        outputProp: "value",
        debounceMs: 500,
      }],
    },
  ];
  const runner = createUiEventEffectRunner({ nodes, store });
  let notified = 0;
  store.subscribe(() => notified++);
  const result = runner.emitLifecycle("initial_mount");
  assertEquals(result.ok, false);
  assertEquals(notified, 0);
  assertEquals(runner.stateDispatcher.get("n-modal", "open"), false);
});

Deno.test("rerender接続: SSE refresh相当の再renderでも store状態と fired-registry が維持される", () => {
  const store = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(
    runnerNodesFromEmission(),
    store,
  );
  const emission = modalEmission();
  const runner = createUiEventEffectRunner({
    nodes: runnerNodesFromEmission(),
    dispatcher,
  });
  runner.emitLifecycle("initial_mount");
  assertEquals(runner.stateDispatcher.get("n-modal", "open"), true);

  // SSE refresh: a NEW emission object is rendered with the SAME dispatcher/runner refs.
  const refreshed = renderEmission(modalEmission(), defaultComponentRegistry, {
    localStateStore: dispatcher,
  });
  assertEquals(modalOpenProp(refreshed), true);
  // Refresh does not re-create runner/store: lifecycle stays idempotent.
  assertEquals(runner.emitLifecycle("initial_mount").executed, []);
  void emission;
});

Deno.test("runner.updateNodes: SSE refresh reconciliation — existing node's initial_mount does not re-execute; a newly-appeared node's initial_mount executes exactly once", () => {
  const externalCalls: unknown[] = [];
  // Node A: already present at creation, initial_mount fires once on the first emitLifecycle.
  const nodeA: WiringNode = {
    nodeId: "n-a-existing",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-a",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "once_per_mount",
      sideEffectNone: true,
    }],
  };
  const runner = createUiEventEffectRunner({
    nodes: [nodeA],
    dispatchExternalPort: (spec) => externalCalls.push(spec),
  });
  const firstResult = runner.emitLifecycle("initial_mount");
  assertEquals(firstResult.ok, true);
  assertEquals(firstResult.executed, ["n-a-existing#0"]);
  assertEquals(externalCalls.length, 1);

  // SSE refresh: node A remains, node B (with its own initial_mount interaction) appears.
  const nodeB: WiringNode = {
    nodeId: "n-b-new",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-b",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "once_per_mount",
      sideEffectNone: true,
    }],
  };
  runner.updateNodes([nodeA, nodeB]);
  const secondResult = runner.emitLifecycle("initial_mount");
  assertEquals(secondResult.ok, true);
  // Node A's already-fired interaction (same nodeId + interaction index) does
  // not re-execute; node B's interaction executes exactly once.
  assertEquals(secondResult.executed, ["n-b-new#0"]);
  assertEquals(externalCalls.length, 2);

  // A further reconciliation with the same node list re-emits nothing new.
  runner.updateNodes([nodeA, nodeB]);
  const thirdResult = runner.emitLifecycle("initial_mount");
  assertEquals(thirdResult.executed, []);
  assertEquals(externalCalls.length, 2);
});

Deno.test("runner.updateNodes: recomputes the loop guard and predeclared slots against the refreshed node list", () => {
  const runner = createUiEventEffectRunner({ nodes: [] });
  assertEquals(runner.cycleErrors.length, 0);
  assertEquals(runner.declaredSlots, []);

  // A node introducing a direct self-loop only appears after reconciliation.
  const loopNode: WiringNode = {
    nodeId: "n-loop-refresh",
    componentKey: "form_input/text",
    stateJson: JSON.stringify({ value: "" }),
    runtimeInteractions: [{
      trigger: "change",
      actionType: "setState",
      targetNodeId: "n-loop-refresh",
      statePath: "value",
    }],
  };
  runner.updateNodes([loopNode]);
  assert(
    runner.cycleErrors.length > 0,
    "reconciliation must recompute the loop guard against the refreshed nodes",
  );
  assert(
    runner.declaredSlots.some((s) =>
      s.nodeId === "n-loop-refresh" && s.stateKey === "value"
    ),
    "reconciliation must predeclare UI監視割当 slots from the refreshed nodes",
  );
});

// ─── retry_safe_dispatch_idempotency: lifecycle-triggered dispatch ──────────
//
// Proves the audit's core distinction: the runner's fired-registry only guards
// duplicate dispatch WITHIN a single runner's lifetime (proof 3 — "frontend
// runner guard is not treated as full idempotency"). A page reload / SSE
// reconnect / runtime-runner recreation constructs a BRAND NEW runner instance
// with a brand new (empty) fired-registry — so cross-request retry-safety must
// come from a STABLE idempotencyKey the backend ledger can recognize, not from
// the fired-registry (which cannot survive the new instance).

function lifecycleDispatchNodes(): WiringNode[] {
  return [{
    nodeId: "n-fetch",
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "once_per_mount",
      sideEffectNone: true,
    }],
  }];
}

Deno.test("retry_safe_dispatch_idempotency: idempotencyKey is stable across two separately-created runner instances for the SAME authored interaction (simulates reload / runner recreation)", () => {
  const capturedA: Array<{ idempotencyKey?: string }> = [];
  const runnerA = createUiEventEffectRunner({
    nodes: lifecycleDispatchNodes(),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedA.push(spec),
  });
  runnerA.emitLifecycle("initial_mount");

  // Simulate reload: a BRAND NEW runner instance (fresh fired-registry, fresh
  // store) is created from the SAME authored node list and the SAME layoutId/
  // packageId identity — exactly what ProjectionShell does on remount.
  const capturedB: Array<{ idempotencyKey?: string }> = [];
  const runnerB = createUiEventEffectRunner({
    nodes: lifecycleDispatchNodes(),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedB.push(spec),
  });
  runnerB.emitLifecycle("initial_mount");

  assertEquals(capturedA.length, 1);
  assertEquals(capturedB.length, 1);
  assert(capturedA[0].idempotencyKey, "idempotencyKey must be present");
  assertEquals(
    capturedA[0].idempotencyKey,
    capturedB[0].idempotencyKey,
    "the SAME initial_mount interaction must produce the SAME idempotencyKey across runner recreation — this is what lets the backend ledger recognize a retry rather than a new logical dispatch",
  );
});

Deno.test("retry_safe_dispatch_idempotency: distinct authored interactions (different nodeId) produce distinct idempotencyKeys", () => {
  const captured: Array<{ idempotencyKey?: string }> = [];
  const nodes: WiringNode[] = [
    {
      nodeId: "n-fetch-a",
      componentKey: "layout/box",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-1",
        lifecycleDispatchConfirmed: true,
        idempotencyPolicy: "once_per_mount",
        sideEffectNone: true,
      }],
    },
    {
      nodeId: "n-fetch-b",
      componentKey: "layout/box",
      runtimeInteractions: [{
        trigger: "initial_mount",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:access_port:port-1",
        lifecycleDispatchConfirmed: true,
        idempotencyPolicy: "once_per_mount",
        sideEffectNone: true,
      }],
    },
  ];
  const runner = createUiEventEffectRunner({
    nodes,
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => captured.push(spec),
  });
  runner.emitLifecycle("initial_mount");

  assertEquals(captured.length, 2);
  assert(
    captured[0].idempotencyKey !== captured[1].idempotencyKey,
    "two distinct authored initial_mount interactions must not collide on the same idempotencyKey",
  );
});

// ─── projection_authority_runtime_interaction_identity: lifecycle path ──────
// PR577 follow-up implementation. SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
// lifecycle_policy.projection_authority_runtime_interaction_identity.

function lifecycleDispatchNodesWithRuntimeInteractionId(
  nodeId: string,
): WiringNode[] {
  return [{
    nodeId,
    componentKey: "layout/box",
    runtimeInteractions: [{
      trigger: "initial_mount",
      actionType: "dispatchExternalPort",
      portTargetRef: "external-port:access_port:port-1",
      lifecycleDispatchConfirmed: true,
      idempotencyPolicy: "once_per_mount",
      sideEffectNone: true,
      runtimeInteractionId: "aaaaaaaa-0000-0000-0000-000000000001",
    }],
  }];
}

Deno.test("retry_safe_dispatch_idempotency: lifecycle path forwards runtimeInteractionId — same id survives even when nodeId changes (authoring rename)", () => {
  const capturedA: Array<{ idempotencyKey?: string }> = [];
  const runnerA = createUiEventEffectRunner({
    nodes: lifecycleDispatchNodesWithRuntimeInteractionId("n-fetch"),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedA.push(spec),
  });
  runnerA.emitLifecycle("initial_mount");

  // Same authored interaction (same runtimeInteractionId) but the node was
  // renamed — simulates an authoring edit that changes structural position
  // without changing the assigned identity.
  const capturedB: Array<{ idempotencyKey?: string }> = [];
  const runnerB = createUiEventEffectRunner({
    nodes: lifecycleDispatchNodesWithRuntimeInteractionId("n-fetch-renamed"),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedB.push(spec),
  });
  runnerB.emitLifecycle("initial_mount");

  assertEquals(capturedA.length, 1);
  assertEquals(capturedB.length, 1);
  assertEquals(
    capturedA[0].idempotencyKey,
    capturedB[0].idempotencyKey,
    "runtimeInteractionId must survive a nodeId rename — the stable id, not the structural position, is the identity once assigned",
  );
});

Deno.test("retry_safe_dispatch_idempotency: lifecycle path without runtimeInteractionId keeps the pre-existing structural identity (backward compatible)", () => {
  const capturedWithId: Array<{ idempotencyKey?: string }> = [];
  createUiEventEffectRunner({
    nodes: lifecycleDispatchNodesWithRuntimeInteractionId("n-fetch"),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedWithId.push(spec),
  }).emitLifecycle("initial_mount");

  const capturedWithoutId: Array<{ idempotencyKey?: string }> = [];
  createUiEventEffectRunner({
    nodes: lifecycleDispatchNodes(),
    layoutId: "layout-1",
    packageId: "pkg-1",
    dispatchExternalPort: (spec) => capturedWithoutId.push(spec),
  }).emitLifecycle("initial_mount");

  assert(
    capturedWithId[0].idempotencyKey !== capturedWithoutId[0].idempotencyKey,
    "an assigned runtimeInteractionId must produce a different key than the id-less structural fallback for the same nodeId",
  );
});

// ─── liveNodeValueTracker (frontend/runtime/liveNodeValueTracker.ts): a
// sibling runtime-local value primitive, NOT createRuntimeLocalStateStore
// above — declare-before-set guarding and iterate/delete-free
// RuntimeLocalStateStore don't fit a dynamically-appearing/disappearing
// node's live value capture; see liveNodeValueTracker.ts's own doc comment
// for the full interface-shape comparison (PR #599 review round 5) ─────────

Deno.test("createLiveNodeValueTracker: set() registers a node's value", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "Status");
  assertEquals(tracker.snapshot(), { "node-1": "Status" });
});

Deno.test("createLiveNodeValueTracker: set() updates an already-registered node's value", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "Status");
  tracker.set("node-1", "Priority");
  assertEquals(tracker.snapshot(), { "node-1": "Priority" });
});

Deno.test("createLiveNodeValueTracker: snapshot() returns the SAME object reference across calls", () => {
  const tracker = createLiveNodeValueTracker();
  const first = tracker.snapshot();
  tracker.set("node-1", "x");
  const second = tracker.snapshot();
  assertEquals(first, second, "same reference must reflect later mutations");
  assertEquals(first === second, true);
});

Deno.test("createLiveNodeValueTracker: reconcile() deregisters nodes absent from the current node list", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "a");
  tracker.set("node-2", "b");
  tracker.reconcile(["node-1"]);
  assertEquals(tracker.snapshot(), { "node-1": "a" });
});

Deno.test("createLiveNodeValueTracker: reconcile() keeps values for nodes still present", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("node-1", "a");
  tracker.reconcile(["node-1", "node-2"]);
  assertEquals(tracker.snapshot(), { "node-1": "a" });
});

Deno.test("createLiveNodeValueTracker: set() with empty nodeId is a no-op (fail-close, no anonymous key)", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("", "x");
  assertEquals(tracker.snapshot(), {});
});

// ─── own-property identity (PR #599 review): a nodeId colliding with an
// inherited Object.prototype key must never resolve as an inherited value ──

Deno.test("createLiveNodeValueTracker: a nodeId shaped like an Object.prototype key is a genuinely separate, unset slot until set()", () => {
  const tracker = createLiveNodeValueTracker();
  // Before any set() call, "constructor"/"toString" must read as absent, not
  // as the inherited Object.prototype function a plain {} would expose.
  assertEquals(
    Object.prototype.hasOwnProperty.call(tracker.snapshot(), "constructor"),
    false,
  );
  tracker.set("constructor", "actual-value");
  assertEquals(tracker.snapshot()["constructor"], "actual-value");
  assertEquals(
    Object.prototype.hasOwnProperty.call(tracker.snapshot(), "constructor"),
    true,
  );
});

// ─── structural_subtree_conditional_visibility_contract: write_side_dynamic_value
// (payloadFrom.value = "event.<path>" for a UI状態更新 mutation — the mechanism
// credential_category_filter's onChange uses to write the SELECTED option, not
// a fixed literal) ──────────────────────────────────────────────────────────

Deno.test("resolveUiStateUpdateMutation: payloadFrom.value='event.value' on a setState actionType resolves to a deferred valueFrom, never a static value", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "setState",
    targetRef: "ui-local:credential_category_filter.selectedCategory",
    payloadFrom: { value: "event.value" },
  });
  assertEquals(resolved, {
    targetNodeId: "credential_category_filter",
    statePath: "selectedCategory",
    action: "set",
    valueFrom: "event.value",
  });
});

Deno.test("resolveUiStateUpdateMutation: a fixed-boolean actionType (openModal) ignores payloadFrom.value — dynamic value is only for generic setState/localStateMutation", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "openModal",
    targetNodeId: "n-banner",
    payloadFrom: { value: "event.value" },
  });
  assertEquals(resolved, { targetNodeId: "n-banner", statePath: "open", action: "set", value: true });
});

Deno.test("resolveUiStateUpdateMutationValue: resolves a valueFrom mutation against the real event payload", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "setState",
    targetRef: "ui-local:credential_category_filter.selectedCategory",
    payloadFrom: { value: "event.value" },
  })!;
  const result = resolveUiStateUpdateMutationValue(resolved, {
    eventPayload: { value: "external_instance_credential" },
    nodeValues: {},
  });
  assertEquals(result, { ok: true, value: "external_instance_credential" });
});

Deno.test("resolveUiStateUpdateMutationValue: a valueFrom mutation with NO eventContext (lifecycle trigger) fails close — never writes undefined", () => {
  const resolved = resolveUiStateUpdateMutation({
    actionType: "setState",
    targetRef: "ui-local:credential_category_filter.selectedCategory",
    payloadFrom: { value: "event.value" },
  })!;
  const result = resolveUiStateUpdateMutationValue(resolved);
  assert(!result.ok);
  assert(result.error.startsWith("RUNTIME_STATE_UPDATE_VALUE_FROM_REQUIRES_EVENT"));
});

Deno.test("resolveUiStateUpdateMutationValue: a static value (no valueFrom) resolves unchanged regardless of eventContext — 100% backward compatible", () => {
  const resolved = resolveUiStateUpdateMutation({ actionType: "openModal", targetNodeId: "n" })!;
  assertEquals(resolveUiStateUpdateMutationValue(resolved), { ok: true, value: true });
  assertEquals(
    resolveUiStateUpdateMutationValue(resolved, { eventPayload: {}, nodeValues: {} }),
    { ok: true, value: true },
  );
});

Deno.test("applyGuardedLocalStateMutation: a valueFrom mutation writes the event's actual value through the guarded dispatcher", () => {
  const store = createRuntimeLocalStateStore();
  const dispatcher = createRuntimeStateDispatcher(store);
  dispatcher.declare("credential_category_filter", "selectedCategory", "external_api_credential");
  const resolved = resolveUiStateUpdateMutation({
    actionType: "setState",
    targetRef: "ui-local:credential_category_filter.selectedCategory",
    payloadFrom: { value: "event.value" },
  })!;
  const result = applyGuardedLocalStateMutation(dispatcher, resolved, {
    eventPayload: { value: "users" },
    nodeValues: {},
  });
  assert(result.ok);
  assertEquals(dispatcher.get("credential_category_filter", "selectedCategory"), "users");
});

// ─── structural_subtree_conditional_visibility_contract: operation_reachability —
// emitLifecycle must skip a node whose resolveNodeVisibility resolves invisible,
// the SAME evaluator LayoutProjectionTree uses for DOM mount, so a hidden
// subtree's initial_mount/route_enter/initial_display interactions never fire ──

Deno.test("runner.emitLifecycle: a lifecycle interaction on a node nested under an invisible Category never executes — same visibility evaluator as DOM mount", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "selector",
      stateJson: JSON.stringify({ selectedCategory: "catA" }),
    },
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" },
    },
    {
      nodeId: "catB",
      parentNodeId: undefined,
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catB" },
    },
    {
      nodeId: "fieldInCatB",
      parentNodeId: "catB",
      runtimeInteractions: [
        { trigger: "initial_mount", actionType: "setState", targetNodeId: "sentinel", statePath: "fired", value: true },
      ],
    },
    { nodeId: "sentinel", stateJson: JSON.stringify({ fired: false }) },
  ];
  const runner = createUiEventEffectRunner({ nodes });
  const result = runner.emitLifecycle("initial_mount");
  assert(result.ok);
  // catA is the active category (selectedCategory === "catA") — catB (and its
  // descendant fieldInCatB) is NOT active, so fieldInCatB's initial_mount
  // interaction must not execute even though the runner's own flat node list
  // (independent of any DOM) still contains it.
  assertEquals(result.executed, []);
  assertEquals(runner.stateDispatcher.get("sentinel", "fired"), false);
});

Deno.test("runner.emitLifecycle: a lifecycle interaction on a node under the ACTIVE category executes normally — visibility gating is not a blanket suppression", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "selector",
      stateJson: JSON.stringify({ selectedCategory: "catA" }),
    },
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:selector.selectedCategory", matchValue: "catA" },
    },
    {
      nodeId: "fieldInCatA",
      parentNodeId: "catA",
      runtimeInteractions: [
        { trigger: "initial_mount", actionType: "setState", targetNodeId: "sentinel", statePath: "fired", value: true },
      ],
    },
    { nodeId: "sentinel", stateJson: JSON.stringify({ fired: false }) },
  ];
  const runner = createUiEventEffectRunner({ nodes });
  const result = runner.emitLifecycle("initial_mount");
  assert(result.ok);
  assertEquals(result.executed, ["fieldInCatA#0"]);
  assertEquals(runner.stateDispatcher.get("sentinel", "fired"), true);
});

Deno.test("runner.emitLifecycle: an undeclared visibilityBinding source is treated as invisible (skip, never an error thrown) at the lifecycle layer", () => {
  const nodes: WiringNode[] = [
    {
      nodeId: "catA",
      visibilityBinding: { source: "ui-local:never_declared.selectedCategory", matchValue: "catA" },
    },
    {
      nodeId: "fieldInCatA",
      parentNodeId: "catA",
      runtimeInteractions: [
        { trigger: "initial_mount", actionType: "setState", targetNodeId: "sentinel", statePath: "fired", value: true },
      ],
    },
    { nodeId: "sentinel", stateJson: JSON.stringify({ fired: false }) },
  ];
  const runner = createUiEventEffectRunner({ nodes });
  const result = runner.emitLifecycle("initial_mount");
  assert(result.ok);
  assertEquals(result.executed, []);
  assertEquals(runner.stateDispatcher.get("sentinel", "fired"), false);
});
