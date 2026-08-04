/**
 * liveNodeValueTracker.test.ts
 *
 * Tests for:
 *   - createLiveNodeValueTracker (set/snapshot/reconcile)
 *   - seedTrackerFromPropBindingsValue (form_input/search_input propBindings.value pre-fill,
 *     admin-write-surface-selection-context-and-mode-composition-gap Bundle)
 */
import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  createLiveNodeValueTracker,
  seedTrackerFromPropBindingsValue,
} from "../runtime/liveNodeValueTracker.ts";
import { resolveRuntimeDataPath } from "../runtime/propBindingResolver.ts";

Deno.test("createLiveNodeValueTracker: set/snapshot round trips a value", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("field_a", "hello");
  assertEquals(tracker.snapshot().field_a, "hello");
});

Deno.test("createLiveNodeValueTracker: set with empty nodeId is a no-op", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("", "hello");
  assertEquals(Object.prototype.hasOwnProperty.call(tracker.snapshot(), ""), false);
});

Deno.test("createLiveNodeValueTracker: reconcile drops nodeIds absent from the current node list", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("field_a", "hello");
  tracker.set("field_b", "world");
  tracker.reconcile(["field_a"]);
  const snap = tracker.snapshot();
  assertEquals(snap.field_a, "hello");
  assertEquals(Object.prototype.hasOwnProperty.call(snap, "field_b"), false);
});

Deno.test("createLiveNodeValueTracker: snapshot returns the same backing object reference across calls", () => {
  const tracker = createLiveNodeValueTracker();
  const first = tracker.snapshot();
  tracker.set("field_a", "hello");
  const second = tracker.snapshot();
  assertEquals(first, second);
});

Deno.test("seedTrackerFromPropBindingsValue: seeds an untracked search_input node from a resolved string", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "demo_status" } },
    resolveRuntimeDataPath,
  );
  assertEquals(tracker.snapshot().group_name_field, "demo_status");
});

Deno.test("seedTrackerFromPropBindingsValue: never overwrites an already-tracked value (user edit or earlier pre-fill)", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("group_name_field", "user_typed_value");
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "demo_status" } },
    resolveRuntimeDataPath,
  );
  assertEquals(tracker.snapshot().group_name_field, "user_typed_value");
});

Deno.test("seedTrackerFromPropBindingsValue: skips nodes that are not form_input/search_input", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    {
      nodeId: "some_button",
      componentKind: "action/button",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "demo_status" } },
    resolveRuntimeDataPath,
  );
  assertEquals(Object.prototype.hasOwnProperty.call(tracker.snapshot(), "some_button"), false);
});

Deno.test("seedTrackerFromPropBindingsValue: leaves the tracker untouched when the source path is absent", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: {} },
    resolveRuntimeDataPath,
  );
  assertEquals(Object.prototype.hasOwnProperty.call(tracker.snapshot(), "group_name_field"), false);
});

Deno.test("seedTrackerFromPropBindingsValue: skips a node with no propBindings.value binding", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    { nodeId: "group_name_field", componentKind: "form_input/search_input" },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "demo_status" } },
    resolveRuntimeDataPath,
  );
  assertEquals(Object.prototype.hasOwnProperty.call(tracker.snapshot(), "group_name_field"), false);
});

// ─── seedTrackerFromPropBindingsValue: options.forceOverwrite (PR #600 review
// round 12 — record-switch display/dispatch divergence fix) ────────────────

Deno.test("seedTrackerFromPropBindingsValue: WITHOUT forceOverwrite, a stale tracked value from a PREVIOUS record's Load survives a new Load's re-seed (the round-12-identified divergence bug, kept reproducible)", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  // Load(A): seeds "record_a".
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "record_a" } },
    resolveRuntimeDataPath,
  );
  assertEquals(tracker.snapshot().group_name_field, "record_a");
  // Load(B) without forceOverwrite: "record_b" must NOT displace the already-tracked
  // "record_a" — this is the exact divergence the owner flagged (display would show
  // B's value via propBindings, but the tracker — and therefore Preview/Confirm's
  // node:<id>.value payload — would still carry A's stale value).
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "record_b" } },
    resolveRuntimeDataPath,
  );
  assertEquals(tracker.snapshot().group_name_field, "record_a");
});

Deno.test("seedTrackerFromPropBindingsValue: WITH forceOverwrite:true, Load(B) after Load(A) overwrites the tracker so display and dispatch payload both carry B's value (fixes the round-12 divergence)", () => {
  const tracker = createLiveNodeValueTracker();
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "record_a" } },
    resolveRuntimeDataPath,
    { forceOverwrite: true },
  );
  assertEquals(tracker.snapshot().group_name_field, "record_a");
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: { groupName: "record_b" } },
    resolveRuntimeDataPath,
    { forceOverwrite: true },
  );
  assertEquals(tracker.snapshot().group_name_field, "record_b");
});

Deno.test("seedTrackerFromPropBindingsValue: forceOverwrite:true still does not fabricate a value when the source path resolves to nothing", () => {
  const tracker = createLiveNodeValueTracker();
  tracker.set("group_name_field", "record_a");
  const layoutNodes = [
    {
      nodeId: "group_name_field",
      componentKind: "form_input/search_input",
      propBindings: { value: { source: "emission.data.preview.groupName" } },
    },
  ];
  seedTrackerFromPropBindingsValue(
    tracker,
    layoutNodes,
    { preview: {} },
    resolveRuntimeDataPath,
    { forceOverwrite: true },
  );
  // No resolved value for the new record: the previous value is left as-is (there is
  // nothing better to overwrite it WITH) rather than being cleared to undefined.
  assertEquals(tracker.snapshot().group_name_field, "record_a");
});
