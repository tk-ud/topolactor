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
  createRuntimeLocalStateStore,
  createRuntimeStateDispatcher,
  createUiEventEffectRunner,
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
