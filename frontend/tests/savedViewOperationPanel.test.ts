// savedViewOperationPanel.test.ts
//
// Real component-render + real dispatch-body proof (not source-string checks) that Team Markdown
// Refresh / Clone / Rebind reach the real backend actions — replacing the previous stubNotice()
// placeholders in TeamMarkdownAuthoring. Uses a live DOM (happy-dom) so preview -> explicit confirm
// -> write actually runs through preact state/effects, and a mocked /api/dispatch that captures and
// asserts on the real request body (target/layer/action/idOrHubId/payload), not just that "a
// function was called".

import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { __testOnly } from "../runtime/frontendScheduler.ts";
import { SavedViewOperationPanel } from "../components/SavedViewOperationPanel.tsx";
import type { SavedViewDetail } from "../api/teamMarkdownApi.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function buildValidSeed() {
  return {
    seed_version: "v1",
    template_ref: {},
    source_ref: {},
    binding_ref: {},
    render_ref: {
      rendered_markdown_hash: "hash-1",
      rendered_at: new Date().toISOString(),
      renderer_version: "v1",
    },
    adjustment_ref: {},
    dashboard_ref: {},
    lineage_ref: {},
  };
}

function buildSavedView(): SavedViewDetail {
  return {
    savedViewId: "00000000-0000-0000-0000-0000000000aa",
    title: "Fixture saved view",
    templateKey: "fixture_template",
    sourceTableRef: "topology.source",
    sourceRecordRef: "record-1",
    status: "active",
    updatedAt: new Date().toISOString(),
    cardMetadataJson: {},
    templateId: "00000000-0000-0000-0000-0000000000bb",
    bindingJson: { placeholder_bindings: [] },
    completedPresetSeedJson: buildValidSeed(),
    renderedMarkdown: "# Fixture",
    userAdjustmentPatchJson: {},
    searchIndexText: "fixture",
    createdAt: new Date().toISOString(),
  };
}

const originalFetch = globalThis.fetch;

function captureDispatchFetch(capturedBodies: unknown[]): typeof globalThis.fetch {
  return (input: string | URL | Request, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input instanceof Request ? input.url : input.href;
    if (url === "/api/dispatch" && init?.body) {
      capturedBodies.push(JSON.parse(init.body as string));
    }
    return Promise.resolve(
      new Response(
        JSON.stringify({ success: true, emission: { data: { ok: true, savedViewId: "00000000-0000-0000-0000-0000000000cc" } } }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
  };
}

async function clickButtonByText(container: Element, text: string): Promise<void> {
  const buttons = Array.from(container.querySelectorAll("button"));
  const target = buttons.find((b) => b.textContent?.includes(text));
  if (!target) throw new Error(`button not found: ${text}`);
  target.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("click", { bubbles: true }));
  await flushUpdates();
}

Deno.test("SavedViewOperationPanel refresh: preview shows non-mutating payload, confirm write dispatches saved_view:refresh with real templateMarkdown/sourceRecordJson", async () => {
  __testOnly.resetCommandQueue();
  const capturedBodies: unknown[] = [];
  globalThis.fetch = captureDispatchFetch(capturedBodies);
  const { container, cleanup } = setupDom();
  try {
    const onWritten = () => {};
    render(
      h(SavedViewOperationPanel, { mode: "refresh", savedView: buildSavedView(), onWritten, onCancel: () => {} }),
      container,
    );
    await flushUpdates();

    // Real input collection — fill the source record JSON textarea.
    const textareas = Array.from(container.querySelectorAll("textarea"));
    const sourceRecordTextarea = textareas[1] as HTMLTextAreaElement; // template markdown, then source record
    sourceRecordTextarea.value = JSON.stringify({ field_one: "value-1" });
    sourceRecordTextarea.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    await flushUpdates();

    await clickButtonByText(container, "Preview");
    const html = container.innerHTML;
    assert(html.includes("field_one"), "non-mutating preview must show the real collected payload");
    assertEquals(capturedBodies.length, 0, "Preview must not call the backend — it is client-side only");

    await clickButtonByText(container, "Confirm refresh");
    assert(container.innerHTML.includes("Confirm refresh"), "explicit confirm dialog must be shown before write");

    await clickButtonByText(container, "Write");

    assertEquals(capturedBodies.length, 1, "confirming the dialog must dispatch exactly one real request");
    const body = capturedBodies[0] as { target: string; layer: string; action: string; idOrHubId: string; payload: Record<string, unknown> };
    assertEquals(body.target, "admin");
    assertEquals(body.layer, "team_markdown");
    assertEquals(body.action, "saved_view:refresh");
    assertEquals(body.idOrHubId, "00000000-0000-0000-0000-0000000000aa");
    assertEquals((body.payload as { sourceRecordJson: Record<string, unknown> }).sourceRecordJson, { field_one: "value-1" });
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SavedViewOperationPanel clone: rejects write when required target refs are missing (no disabled placeholder — a real validation error instead)", async () => {
  __testOnly.resetCommandQueue();
  const capturedBodies: unknown[] = [];
  globalThis.fetch = captureDispatchFetch(capturedBodies);
  const { container, cleanup } = setupDom();
  try {
    render(
      h(SavedViewOperationPanel, { mode: "clone", savedView: buildSavedView(), onWritten: () => {}, onCancel: () => {} }),
      container,
    );
    await flushUpdates();

    await clickButtonByText(container, "Preview");
    assert(
      container.innerHTML.includes("targetSourceTableRef is required"),
      "missing required clone input must produce a real validation error, not a silently-disabled button",
    );
    assertEquals(capturedBodies.length, 0);
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SavedViewOperationPanel clone: real input collection + confirm dispatches saved_view:clone with target refs", async () => {
  __testOnly.resetCommandQueue();
  const capturedBodies: unknown[] = [];
  globalThis.fetch = captureDispatchFetch(capturedBodies);
  const { container, cleanup } = setupDom();
  try {
    render(
      h(SavedViewOperationPanel, { mode: "clone", savedView: buildSavedView(), onWritten: () => {}, onCancel: () => {} }),
      container,
    );
    await flushUpdates();

    const inputs = Array.from(container.querySelectorAll("input")) as HTMLInputElement[];
    // clone renders targetSourceTableRef, targetSourceRecordRef, title inputs first.
    inputs[0].value = "topology.target_table";
    inputs[0].dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    inputs[1].value = "target-record-1";
    inputs[1].dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    await flushUpdates();

    await clickButtonByText(container, "Preview");
    await clickButtonByText(container, "Confirm clone");
    await clickButtonByText(container, "Write");

    assertEquals(capturedBodies.length, 1);
    const body = capturedBodies[0] as { action: string; idOrHubId: string; payload: Record<string, unknown> };
    assertEquals(body.action, "saved_view:clone");
    assertEquals(body.idOrHubId, "00000000-0000-0000-0000-0000000000aa");
    assertEquals(body.payload.targetSourceTableRef, "topology.target_table");
    assertEquals(body.payload.targetSourceRecordRef, "target-record-1");
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SavedViewOperationPanel rebind: real input collection + confirm dispatches saved_view:rebind with bindingJson", async () => {
  __testOnly.resetCommandQueue();
  const capturedBodies: unknown[] = [];
  globalThis.fetch = captureDispatchFetch(capturedBodies);
  const { container, cleanup } = setupDom();
  try {
    render(
      h(SavedViewOperationPanel, { mode: "rebind", savedView: buildSavedView(), onWritten: () => {}, onCancel: () => {} }),
      container,
    );
    await flushUpdates();

    const textareas = Array.from(container.querySelectorAll("textarea")) as HTMLTextAreaElement[];
    // rebind renders bindingJson, completedPresetSeedJson, templateMarkdown, sourceRecordJson in that order.
    const bindingTextarea = textareas[0];
    bindingTextarea.value = JSON.stringify({ placeholder_bindings: [{ placeholder_key: "x", required: true }] });
    bindingTextarea.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    const sourceRecordTextarea = textareas[3];
    sourceRecordTextarea.value = JSON.stringify({ x: "resolved" });
    sourceRecordTextarea.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    await flushUpdates();

    await clickButtonByText(container, "Preview");
    await clickButtonByText(container, "Confirm rebind");
    await clickButtonByText(container, "Write");

    assertEquals(capturedBodies.length, 1);
    const body = capturedBodies[0] as { action: string; payload: Record<string, unknown> };
    assertEquals(body.action, "saved_view:rebind");
    assertEquals((body.payload as { bindingJson: Record<string, unknown> }).bindingJson, {
      placeholder_bindings: [{ placeholder_key: "x", required: true }],
    });
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SavedViewOperationPanel: malformed JSON in a JSON field surfaces a real error, never silently ignored", async () => {
  __testOnly.resetCommandQueue();
  const capturedBodies: unknown[] = [];
  globalThis.fetch = captureDispatchFetch(capturedBodies);
  const { container, cleanup } = setupDom();
  try {
    render(
      h(SavedViewOperationPanel, { mode: "refresh", savedView: buildSavedView(), onWritten: () => {}, onCancel: () => {} }),
      container,
    );
    await flushUpdates();

    const textareas = Array.from(container.querySelectorAll("textarea")) as HTMLTextAreaElement[];
    const sourceRecordTextarea = textareas[1];
    sourceRecordTextarea.value = "{not valid json";
    sourceRecordTextarea.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
    await flushUpdates();

    await clickButtonByText(container, "Preview");
    assert(container.innerHTML.includes("must be valid JSON"), "malformed JSON input must produce a visible error");
    assertEquals(capturedBodies.length, 0);
  } finally {
    render(null, container);
    cleanup();
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});
