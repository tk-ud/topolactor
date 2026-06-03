// test-dom-setup.ts — happy-dom environment setup/teardown for Preact DOM event simulation tests.
//
// Usage:
//   const { container, cleanup } = setupDom();
//   try {
//     render(h(Component, props), container);
//     await flushUpdates();
//     // ... assertions ...
//   } finally {
//     cleanup();
//   }
//
// Lifecycle:
//   setupDom() — installs happy-dom Window as globalThis.document/window/localStorage/sessionStorage/location/Event
//   cleanup()  — restores all patched globals and closes the Window (frees DOM resources)
//
// Preact scheduling:
//   Call `options.requestAnimationFrame = (cb) => { setTimeout(cb, 0); return 0; }` in the test file
//   ONCE at module level (before any render call). This makes Preact flush effects after the next
//   macrotask instead of waiting for the browser's real rAF (which doesn't exist in Deno).
//
// flushUpdates():
//   Awaits one macrotask. Preact schedules re-renders as Promise microtasks; our rAF shim schedules
//   effect flushes as setTimeout(0). All pending microtask chains (fetch → setState → re-render)
//   and effect flushes complete before the awaited setTimeout fires.
// deno-lint-ignore-file no-explicit-any

import { Window } from "happy-dom";

export interface DomEnv {
  container: Element;
  cleanup: () => void;
}

const PATCHED_GLOBALS = [
  "document",
  "window",
  "localStorage",
  "sessionStorage",
  "location",
  "Event",
  "CustomEvent",
] as const;

export function setupDom(): DomEnv {
  const win = new Window({ url: "http://localhost/" }) as any;
  const doc = win.document;

  const saved: Record<string, unknown> = {};
  for (const k of PATCHED_GLOBALS) {
    saved[k] = (globalThis as any)[k];
  }

  (globalThis as any).document = doc;
  (globalThis as any).window = win;
  (globalThis as any).localStorage = win.localStorage;
  (globalThis as any).sessionStorage = win.sessionStorage;
  (globalThis as any).location = win.location;
  (globalThis as any).Event = win.Event;
  (globalThis as any).CustomEvent = win.CustomEvent;

  const container: Element = doc.createElement("div");
  doc.body.appendChild(container);

  function cleanup(): void {
    // Unmount any Preact tree in container by clearing its children.
    // (Calling render(null, container) requires Preact's render import; callers do it if needed.)
    try {
      container.remove();
    } catch { /* ignore if already removed */ }
    for (const k of PATCHED_GLOBALS) {
      (globalThis as any)[k] = saved[k];
    }
    try {
      win.close();
    } catch { /* ignore */ }
  }

  return { container, cleanup };
}

// Flush all pending microtasks and one macrotask round.
// Preact schedules re-renders as Promise microtasks. Our options.requestAnimationFrame shim
// schedules effect flushes as setTimeout(0). One macrotask is sufficient to drain both.
export async function flushUpdates(): Promise<void> {
  await new Promise<void>((r) => setTimeout(r, 0));
}
