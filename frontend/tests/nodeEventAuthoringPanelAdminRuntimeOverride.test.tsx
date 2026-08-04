// frontend/tests/nodeEventAuthoringPanelAdminRuntimeOverride.test.tsx
//
// Round 18 DOM interaction proof for NodeEventAuthoringPanel's admin_runtime dispatch-override
// authoring section (round 17: new authoring UI; round 18: wiring_kind gating). Mounts the
// PRODUCTION component into a live happy-dom container (not source-text assertions) and drives
// real input/select/click events, mocking only the fetch boundary
// (ui_topology:list_admin_runtime_target_ref_authoring_candidates).
//
// Covers: candidate load + failure, add/cancel/commit override, trigger select, target select,
// payloadFrom field/source add/edit/remove, invalid payloadFrom source display, duplicate-trigger
// overwrite (Record<string,string> keying), remove existing override, and the wiring_kind gate
// (hidden when no overrides + not admin_runtime; disabled+reason when overrides exist but the
// layout isn't admin_runtime; fully editable when admin_runtime).

import { assert, assertEquals, assertFalse, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import NodeEventAuthoringPanel from "../components/NodeEventAuthoringPanel.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const CANDIDATE_A = {
  manifestId: "00000000-0000-0000-0000-0000000ae210",
  manifestKey: "admin.enum.management.write.create_group",
  layer: "enum_dictionary",
  action: "create_group",
  targetRef: "manifest:00000000-0000-0000-0000-0000000ae210:enum_dictionary:create_group",
};
const CANDIDATE_B = {
  manifestId: "00000000-0000-0000-0000-0000000ae230",
  manifestKey: "admin.enum.management.write.delete_group",
  layer: "enum_dictionary",
  action: "delete_group",
  targetRef: "manifest:00000000-0000-0000-0000-0000000ae230:enum_dictionary:delete_group",
};

function makeCandidateFetch(
  candidates: unknown[] | null,
  errorMessage?: string,
): typeof globalThis.fetch {
  return (_url: string | URL | Request, init?: RequestInit) => {
    const body = JSON.parse((init?.body as string) ?? "{}");
    if (body.action === "list_admin_runtime_target_ref_authoring_candidates") {
      if (candidates === null) {
        return Promise.resolve(
          new Response(
            JSON.stringify({ success: false, errors: [{ code: "DB_UNAVAILABLE", message: errorMessage ?? "boom" }] }),
            { status: 500, headers: { "Content-Type": "application/json" } },
          ),
        );
      }
      return Promise.resolve(
        new Response(
          JSON.stringify({ success: true, emission: { data: { candidates } } }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    }
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: null }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };
}

function dispatchInputValue(element: Element, value: string) {
  (element as HTMLInputElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
}

function dispatchSelectValue(element: Element, value: string) {
  (element as HTMLSelectElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
}

// The payloadFrom field-name input commits its rename onBlur, not onInput/onChange.
function dispatchBlurValue(element: Element, value: string) {
  (element as HTMLInputElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("blur", { bubbles: true }));
}

function buttonByText(container: Element, text: string): HTMLButtonElement {
  const buttons = Array.from(container.querySelectorAll("button"));
  const button = (buttons.find((b) => b.textContent?.trim() === text) ??
    buttons.find((b) => b.textContent?.includes(text))) as HTMLButtonElement | undefined;
  assertExists(button, `button not found: ${text}`);
  return button;
}

// deno-lint-ignore no-explicit-any
function baseProps(overrides: Record<string, unknown> = {}): any {
  return {
    interactions: [],
    targetNodes: [],
    triggerOptions: ["click", "change", "submit"],
    onCommit: () => {},
    ...overrides,
  };
}

Deno.test("NodeEventAuthoringPanel admin_runtime override: hidden entirely on a non-admin_runtime layout with no existing overrides", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeCandidateFetch([]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "create",
        onCommitDispatchOverrides: () => {},
      })),
      container,
    );
    await flushUpdates();
    const html = container.innerHTML;
    assertFalse(html.includes("管理操作の上書き"), "override section must not render on a non-admin_runtime layout with nothing authored");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: existing override on non-admin_runtime layout renders read-only with reason", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "create",
        dispatchTargetRefByTrigger: { click: CANDIDATE_A.targetRef },
        onCommitDispatchOverrides: () => {},
      })),
      container,
    );
    await flushUpdates();
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("管理操作の上書き"), "section must still render because an override already exists");
    assert(html.includes("admin_runtimeではないため"), "disabled reason must be visible");
    const select = container.querySelector("select");
    assertExists(select);
    assert((select as HTMLSelectElement).disabled, "target select must be disabled when layout is not admin_runtime");
    assertFalse(html.includes("+ 管理操作の上書き"), "add button must not render when editing is disabled");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: candidate fetch failure shows the error, not a silent empty list", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeCandidateFetch(null, "DBに接続できません");
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        onCommitDispatchOverrides: () => {},
      })),
      container,
    );
    const addBtn = buttonByText(container, "+ 管理操作の上書き");
    addBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("DBに接続できません"), "candidate load error must be visible in the DOM");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: add, select trigger/target, edit payloadFrom, commit", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  type CommitShape = { targetRefByTrigger?: Record<string, string>; payloadFromByTrigger?: Record<string, Record<string, string>> };
  let committed: CommitShape | null = null;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A, CANDIDATE_B]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        onCommitDispatchOverrides: (next: CommitShape) => { committed = next; },
      })),
      container,
    );
    await flushUpdates();

    const addBtn = buttonByText(container, "+ 管理操作の上書き");
    addBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const selects = Array.from(container.querySelectorAll("select"));
    assertEquals(selects.length, 2, "staging form must render a trigger select and a target select");
    const [triggerSelect, targetSelect] = selects;
    dispatchSelectValue(triggerSelect, "change");
    dispatchSelectValue(targetSelect, CANDIDATE_B.targetRef);
    await flushUpdates();

    // payloadFrom: add a field/source pair.
    const addFieldBtn = buttonByText(container, "+ フィールドを追加");
    addFieldBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    const textInputs = Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
    assert(textInputs.length >= 2, "field name + source inputs must render after adding a payloadFrom entry");
    dispatchBlurValue(textInputs[0], "groupId");
    await flushUpdates();
    dispatchInputValue(textInputs[1], "node:group_id_field.value");
    await flushUpdates();

    const commitBtn = buttonByText(container, "管理操作の上書きを確定");
    commitBtn.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();

    if (!committed) throw new Error("onCommitDispatchOverrides must have been called");
    const result = committed as CommitShape;
    assertEquals(result.targetRefByTrigger?.["change"], CANDIDATE_B.targetRef);
    assertEquals(result.payloadFromByTrigger?.["change"]?.["groupId"], "node:group_id_field.value");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: cancel staging discards the draft without committing", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  let committed = false;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        onCommitDispatchOverrides: () => { committed = true; },
      })),
      container,
    );
    await flushUpdates();

    buttonByText(container, "+ 管理操作の上書き").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    buttonByText(container, "キャンセル").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();

    assertFalse(committed, "cancel must never call onCommitDispatchOverrides");
    assertFalse(container.innerHTML.includes("管理操作の上書きを確定"), "staging form must be gone after cancel");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: invalid payloadFrom source is surfaced, not silently accepted", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        dispatchTargetRefByTrigger: { click: CANDIDATE_A.targetRef },
        dispatchPayloadFromByTrigger: { click: { groupName: "not-a-recognized-source" } },
        onCommitDispatchOverrides: () => {},
      })),
      container,
    );
    await flushUpdates();
    await flushUpdates();
    const html = container.innerHTML;
    assert(html.includes("PAYLOAD_FROM_UNRESOLVED_REF"), "an unresolved payloadFrom source must be flagged in the DOM, not silently accepted");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: removing an existing override calls back with that trigger deleted", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  type RemoveCommitShape = { targetRefByTrigger?: Record<string, string> };
  let committed: RemoveCommitShape | null = null;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        dispatchTargetRefByTrigger: { click: CANDIDATE_A.targetRef },
        onCommitDispatchOverrides: (next: RemoveCommitShape) => { committed = next; },
      })),
      container,
    );
    await flushUpdates();
    await flushUpdates();

    buttonByText(container, "削除").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();

    if (!committed) throw new Error("onCommitDispatchOverrides must have been called on remove");
    const result = committed as RemoveCommitShape;
    assertEquals(result.targetRefByTrigger?.["click"], undefined);
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("NodeEventAuthoringPanel admin_runtime override: selecting an already-used trigger overwrites rather than duplicates (Record keying)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  type DupCommitShape = { targetRefByTrigger?: Record<string, string> };
  let committed: DupCommitShape | null = null;
  try {
    globalThis.fetch = makeCandidateFetch([CANDIDATE_A, CANDIDATE_B]);
    render(
      // deno-lint-ignore no-explicit-any
      h(NodeEventAuthoringPanel, baseProps({
        effectiveWiringKind: "admin_runtime",
        dispatchTargetRefByTrigger: { click: CANDIDATE_A.targetRef },
        onCommitDispatchOverrides: (next: DupCommitShape) => { committed = next; },
      })),
      container,
    );
    await flushUpdates();

    buttonByText(container, "+ 管理操作の上書き").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const selects = Array.from(container.querySelectorAll("select")) as HTMLSelectElement[];
    // First select in the staging form is the trigger picker; default is the first trigger option
    // ("click", already used) -- pick CANDIDATE_B as the target for this same trigger.
    const targetSelect = selects[selects.length - 1];
    dispatchSelectValue(targetSelect, CANDIDATE_B.targetRef);
    await flushUpdates();

    buttonByText(container, "管理操作の上書きを確定").dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();

    if (!committed) throw new Error("onCommitDispatchOverrides must have been called");
    const result = committed as DupCommitShape;
    // A single "click" key now maps to CANDIDATE_B -- overwritten, not duplicated.
    assertEquals(Object.keys(result.targetRefByTrigger ?? {}).length, 1);
    assertEquals(result.targetRefByTrigger?.["click"], CANDIDATE_B.targetRef);
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});
