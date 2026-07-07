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
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
  createRuntimeStateDispatcher,
  createUiEventEffectRunner,
  type NotifyingRuntimeLocalStateStore,
} from "../runtime/uiEventEffectRunner.ts";
import { enqueueInstanceOperationDispatchCommand } from "../runtime/frontendScheduler.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";

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
