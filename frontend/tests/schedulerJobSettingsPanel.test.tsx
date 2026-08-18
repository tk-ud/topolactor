// frontend/tests/schedulerJobSettingsPanel.test.tsx
//
// scheduler-settings subBundle (admin-surface-topology-seed-conversion), 2026-07-22
// Owner-confirmed design (.agent/tasks/todo.md "scheduler-settings 3分割設計の確定"): the existing
// hardcoded /admin/scheduler route and frontend/islands/SchedulerJobSettingsPanel.tsx are retired
// BY SCOPE REDUCTION, not replacement — the panel keeps mounting, and its responsibility narrows to
// exactly list / search(job_key) / filter(trigger_kind, schedule_policy_kind, active) / explicit
// enable / explicit disable. This file proves BOTH halves against the real production component
// (mounted into a live happy-dom container, not source-text assertions):
//   (a) the panel still exists and mounts real, interactive UI (not a ProjectionShell stub);
//   (b) create/edit/step-chain authoring UI is GONE, while list/search/filter/enable/disable UI is
//       present and dispatches the real mutation_confirmation_contract (dryRun preview -> inline
//       row confirm/cancel -> confirmed write).

import { assert, assertEquals, assertFalse, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import SchedulerJobSettingsPanel from "../islands/SchedulerJobSettingsPanel.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const FILTER_OPTIONS = {
  triggerKindOptions: [
    { value: "cron", label: "cron" },
    { value: "hook", label: "hook" },
    { value: "client", label: "client" },
  ],
  schedulePolicyKindOptions: [
    { value: "cron", label: "cron" },
    { value: "interval_seconds", label: "interval_seconds" },
    { value: "manual_only", label: "manual_only" },
  ],
  activeOptions: [
    { value: "true", label: "active" },
    { value: "false", label: "inactive" },
  ],
};

const JOB_ACTIVE = {
  schedulerJobId: "00000000-0000-0000-0000-00000000sj01",
  jobKey: "demo_schedule",
  triggerKind: "cron",
  schedulePolicyKind: "manual_only",
  cronExpression: null,
  scheduleIntervalSeconds: null,
  timezone: null,
  manualRunAllowed: true,
  active: true,
  updatedAt: "2026-08-17T00:00:00Z",
};

const JOB_INACTIVE = { ...JOB_ACTIVE, schedulerJobId: "00000000-0000-0000-0000-00000000sj02", jobKey: "other_job", active: false };

type Handler = (body: Record<string, unknown>) => { status: number; body: unknown };

function makeDispatchFetch(handler: Handler): typeof globalThis.fetch {
  return (_url: string | URL | Request, init?: RequestInit) => {
    const body = JSON.parse((init?.body as string) ?? "{}");
    const { status, body: respBody } = handler(body);
    return Promise.resolve(
      new Response(JSON.stringify(respBody), { status, headers: { "Content-Type": "application/json" } }),
    );
  };
}

function listOk(jobs: unknown[]) {
  return { success: true, emission: { data: { ok: true, schedulerJobs: jobs, ...FILTER_OPTIONS } } };
}

function buttonByText(container: Element, text: string): HTMLButtonElement {
  const buttons = Array.from(container.querySelectorAll("button"));
  const button = buttons.find((b) => b.textContent?.trim() === text) as HTMLButtonElement | undefined;
  assertExists(button, `button not found: ${text}`);
  return button;
}

async function clickAndFlush(el: Element) {
  el.dispatchEvent(new Event("click", { bubbles: true }));
  await flushUpdates();
  await flushUpdates();
}

Deno.test("SchedulerJobSettingsPanel: mounts real interactive UI (not a stub/ProjectionShell placeholder)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeDispatchFetch((body) => {
      assertEquals(body.layer, "scheduler_jobs");
      assertEquals(body.action, "list_settings");
      return { status: 200, body: listOk([JOB_ACTIVE]) };
    });
    render(h(SchedulerJobSettingsPanel, {}), container);
    await flushUpdates();
    await flushUpdates();

    assert(container.querySelector("h2")?.textContent?.includes("Scheduler Job Settings"));
    assert(container.querySelector("table"), "job table must render");
    assert(container.innerHTML.includes("demo_schedule"), "real job data must render");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("SchedulerJobSettingsPanel: create/edit/step-chain authoring UI is absent", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    globalThis.fetch = makeDispatchFetch(() => ({ status: 200, body: listOk([JOB_ACTIVE]) }));
    render(h(SchedulerJobSettingsPanel, {}), container);
    await flushUpdates();
    await flushUpdates();

    // No create/edit form: no jobKey/authorityScope/cronExpression/steps text-entry inputs, no
    // Create/Save/Add Step controls -- that authoring surface moved to /admin/contents.
    const textInputs = Array.from(container.querySelectorAll('input[type="text"], textarea'));
    assertEquals(textInputs.length, 0, "no free-text authoring inputs (jobKey/authorityScope/etc.) may render");

    const buttonLabels = Array.from(container.querySelectorAll("button")).map((b) => b.textContent?.trim());
    for (const forbidden of ["Create", "Save", "Add Step", "New Job", "Edit"]) {
      assertFalse(
        buttonLabels.some((label) => label?.includes(forbidden)),
        `authoring control must not render: ${forbidden}`,
      );
    }
    assertFalse(container.innerHTML.includes("credentialRequirementRef"));
    assertFalse(container.innerHTML.includes("externalPortRef"));
    assertFalse(container.innerHTML.includes("authorityScope"));
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("SchedulerJobSettingsPanel: search input and trigger/schedule/active filter selects render and dispatch scoped filters", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  const seenPayloads: Record<string, unknown>[] = [];
  try {
    globalThis.fetch = makeDispatchFetch((body) => {
      seenPayloads.push((body.payload as Record<string, unknown>) ?? {});
      return { status: 200, body: listOk([JOB_ACTIVE]) };
    });
    render(h(SchedulerJobSettingsPanel, {}), container);
    await flushUpdates();
    await flushUpdates();

    const searchInput = container.querySelector('input[type="search"]');
    assertExists(searchInput, "job_key search input must render");
    const selects = container.querySelectorAll("select");
    assertEquals(selects.length, 3, "exactly trigger_kind / schedule_policy_kind / active filter selects");

    (searchInput as HTMLInputElement).value = "demo";
    searchInput!.dispatchEvent(new Event("input", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const last = seenPayloads[seenPayloads.length - 1];
    assertEquals(last.search, "demo");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("SchedulerJobSettingsPanel: Enable/Disable button dispatches dryRun preview, then Confirm dispatches confirmed write", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  const dispatched: Record<string, unknown>[] = [];
  try {
    globalThis.fetch = makeDispatchFetch((body) => {
      if (body.action === "list_settings") return { status: 200, body: listOk([JOB_ACTIVE]) };
      if (body.action === "disable") {
        dispatched.push(body);
        const payload = body.payload as Record<string, unknown>;
        if (payload.dryRun) {
          return {
            status: 200,
            body: {
              success: true,
              emission: {
                data: {
                  ok: true, dryRun: true, valid: true, schedulerJobId: JOB_ACTIVE.schedulerJobId,
                  preview: { operation: "disable", jobKey: JOB_ACTIVE.jobKey, activeBefore: true, activeAfter: false },
                },
              },
            },
          };
        }
        if (payload.confirmed) {
          return {
            status: 200,
            body: { success: true, emission: { data: { ok: true, schedulerJobId: JOB_ACTIVE.schedulerJobId, jobKey: JOB_ACTIVE.jobKey, active: false } } },
          };
        }
      }
      return { status: 200, body: listOk([JOB_ACTIVE]) };
    });
    render(h(SchedulerJobSettingsPanel, {}), container);
    await flushUpdates();
    await flushUpdates();

    // JOB_ACTIVE.active === true, so its row's toggle button reads "Disable".
    const disableButton = buttonByText(container, "Disable");
    await clickAndFlush(disableButton);

    assert(container.innerHTML.includes("Confirm disable"), "dryRun preview must reveal the row's inline confirm state");
    const confirmButton = buttonByText(container, "Confirm");
    const cancelButton = buttonByText(container, "Cancel");
    assertExists(confirmButton);
    assertExists(cancelButton);

    await clickAndFlush(confirmButton);

    assertEquals(dispatched.length, 2, "exactly one dryRun call then one confirmed call");
    assertEquals((dispatched[0].payload as Record<string, unknown>).dryRun, true);
    assertFalse("confirmed" in (dispatched[0].payload as Record<string, unknown>));
    assertEquals((dispatched[1].payload as Record<string, unknown>).confirmed, true);
    assertFalse("dryRun" in (dispatched[1].payload as Record<string, unknown>));
    assert(container.innerHTML.includes("demo_schedule disabled."), "success notice must render after confirmed write");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});

Deno.test("SchedulerJobSettingsPanel: Cancel dismisses the pending toggle without dispatching a write", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  const dispatchedActions: string[] = [];
  try {
    globalThis.fetch = makeDispatchFetch((body) => {
      if (body.action === "list_settings") return { status: 200, body: listOk([JOB_INACTIVE]) };
      dispatchedActions.push(String(body.action));
      const payload = body.payload as Record<string, unknown>;
      if (payload.dryRun) {
        return {
          status: 200,
          body: {
            success: true,
            emission: {
              data: {
                ok: true, dryRun: true, valid: true, schedulerJobId: JOB_INACTIVE.schedulerJobId,
                preview: { operation: "enable", jobKey: JOB_INACTIVE.jobKey, activeBefore: false, activeAfter: true },
              },
            },
          },
        };
      }
      throw new Error("Cancel must never dispatch a confirmed write");
    });
    render(h(SchedulerJobSettingsPanel, {}), container);
    await flushUpdates();
    await flushUpdates();

    // JOB_INACTIVE.active === false, so its row's toggle button reads "Enable".
    const enableButton = buttonByText(container, "Enable");
    await clickAndFlush(enableButton);
    assert(container.innerHTML.includes("Confirm enable"));

    const cancelButton = buttonByText(container, "Cancel");
    await clickAndFlush(cancelButton);

    assertEquals(dispatchedActions, ["enable"], "only the dryRun preview call, never a second/confirmed call");
    assertFalse(container.innerHTML.includes("Confirm enable"), "pending confirm state must be cleared after Cancel");
    assertExists(buttonByText(container, "Enable"), "row reverts to its plain toggle button");
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});
