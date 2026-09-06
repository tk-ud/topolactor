// frontend/tests/authNormalErrorLabelBoundary.test.tsx
//
// Production-composition DOM proof (real fetch-mocked mount, not a synthetic helper call or a
// static string grep) that /auth (LoginManifestPanel) and /super_auth (SuperAuthPanel) no longer
// show raw backend/client diagnostic error text as always-visible primary content. Both surfaces
// previously rendered `e.message ?? e.Message` (LoginManifestPanel) or `authErrorText(e)`
// (SuperAuthPanel, `[code] message`) directly as the primary error list -- the SAME raw-diagnostic-
// as-primary defect on two canonical normal auth surfaces sharing the same AuthError shape.
//
// A static banned-term grep cannot catch this: the backend error text ("Invalid username or
// password.", "AUTH_USER_NOT_APPROVED") is a real *dynamic* runtime value returned from a mocked
// fetch, not a literal string in the component source. Only mounting the real component and
// inspecting the rendered DOM after a real (mocked) login attempt proves the friendly/technical
// separation holds for that dynamic value.

import { assert, assertFalse } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import LoginManifestPanel from "../islands/LoginManifestPanel.tsx";
import SuperAuthPanel from "../islands/SuperAuthPanel.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

function visibleText(container: Element): string {
  const clone = container.cloneNode(true) as Element;
  for (const details of Array.from(clone.querySelectorAll("details"))) {
    details.remove();
  }
  return clone.textContent ?? "";
}

function technicalDisclosureText(container: Element): string {
  return Array.from(container.querySelectorAll("details")).map((d) => d.textContent ?? "").join("\n");
}

async function waitFor(predicate: () => boolean, maxIterations = 40): Promise<void> {
  for (let i = 0; i < maxIterations && !predicate(); i++) {
    await flushUpdates();
  }
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
}

async function submitForm(container: Element, formSelector: string): Promise<void> {
  const form = container.querySelector(formSelector) as HTMLFormElement;
  const usernameInput = form.querySelector('input[type="text"]') as HTMLInputElement;
  const passwordInput = form.querySelector('input[type="password"]') as HTMLInputElement;
  usernameInput.value = "alice";
  usernameInput.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
  passwordInput.value = "wrong-password";
  passwordInput.dispatchEvent(new (globalThis as unknown as { Event: typeof Event }).Event("input", { bubbles: true }));
  form.dispatchEvent(
    new (globalThis as unknown as { Event: typeof Event }).Event("submit", { bubbles: true, cancelable: true }),
  );
}

Deno.test(
  "LoginManifestPanel (real mount): AUTH_INVALID_CREDENTIALS renders a friendly Japanese primary message, raw backend text only in 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = ((url: string) => {
      const path = url.toString();
      if (path === "/api/auth/login-manifest") {
        return Promise.resolve(jsonResponse({
          success: true,
          authActionBinding: { runtime_destination: "auth_runtime", action: "login" },
          uiProjection: { labels: {} },
        }));
      }
      if (path === "/api/auth/projection-login") {
        return Promise.resolve(jsonResponse({
          success: false,
          errors: [{ Code: "AUTH_INVALID_CREDENTIALS", Message: "Invalid username or password." }],
        }));
      }
      return Promise.resolve(jsonResponse({ success: false }));
    }) as typeof fetch;

    try {
      render(h(LoginManifestPanel, {}), container);
      await waitFor(() => container.querySelector("form") !== null);

      await submitForm(container, "form");
      await waitFor(() => (container.textContent ?? "").includes("ログインに失敗しました"));
      await flushUpdates();

      const primary = visibleText(container);
      assert(
        primary.includes("ユーザー名またはパスワードが正しくありません"),
        "a friendly Japanese message must be the always-visible primary error text",
      );
      assertFalse(
        primary.includes("Invalid username or password."),
        "the raw backend diagnostic message must not appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(
        technical.includes("Invalid username or password."),
        "the raw backend diagnostic message must still be reachable inside a 技術情報 disclosure",
      );
      assert(technical.includes("AUTH_INVALID_CREDENTIALS"), "the raw error code must still be reachable in disclosure");
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "LoginManifestPanel (real mount): AUTH_USER_NOT_APPROVED renders its own friendly primary message, not the raw backend sentence",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = ((url: string) => {
      const path = url.toString();
      if (path === "/api/auth/login-manifest") {
        return Promise.resolve(jsonResponse({
          success: true,
          authActionBinding: { runtime_destination: "auth_runtime", action: "login" },
          uiProjection: { labels: {} },
        }));
      }
      if (path === "/api/auth/projection-login") {
        return Promise.resolve(jsonResponse({
          success: false,
          errors: [{ code: "AUTH_USER_NOT_APPROVED", message: "User account is not approved." }],
        }));
      }
      return Promise.resolve(jsonResponse({ success: false }));
    }) as typeof fetch;

    try {
      render(h(LoginManifestPanel, {}), container);
      await waitFor(() => container.querySelector("form") !== null);

      await submitForm(container, "form");
      await waitFor(() => (container.textContent ?? "").includes("ログインに失敗しました"));
      await flushUpdates();

      const primary = visibleText(container);
      assert(
        primary.includes("アカウントは承認待ちです"),
        "AUTH_USER_NOT_APPROVED must render its own friendly primary sentence",
      );
      assertFalse(
        primary.includes("User account is not approved."),
        "the raw backend diagnostic message must not appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(technical.includes("User account is not approved."));
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "LoginManifestPanel (real mount): a raw client-side transport/network error falls back to one generic friendly sentence, never the raw exception text",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = ((url: string) => {
      const path = url.toString();
      if (path === "/api/auth/login-manifest") {
        return Promise.resolve(jsonResponse({
          success: true,
          authActionBinding: { runtime_destination: "auth_runtime", action: "login" },
          uiProjection: { labels: {} },
        }));
      }
      if (path === "/api/auth/projection-login") {
        return Promise.reject(new TypeError("NetworkError: connection reset mid-transfer"));
      }
      return Promise.resolve(jsonResponse({ success: false }));
    }) as typeof fetch;

    try {
      render(h(LoginManifestPanel, {}), container);
      await waitFor(() => container.querySelector("form") !== null);

      await submitForm(container, "form");
      await waitFor(() => (container.textContent ?? "").includes("ログインに失敗しました"));
      await flushUpdates();

      const primary = visibleText(container);
      assert(
        primary.includes("ログインできませんでした"),
        "an unmapped/codeless error must fall back to the single generic friendly sentence",
      );
      assertFalse(
        primary.includes("NetworkError: connection reset mid-transfer"),
        "the raw JS exception message must never appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(technical.includes("NetworkError: connection reset mid-transfer"));
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);

Deno.test(
  "SuperAuthPanel (real mount): AUTH_INVALID_CREDENTIALS renders a friendly Japanese primary message, raw `[code] message` text only in 技術情報 disclosure",
  async () => {
    const { container, cleanup } = setupDom();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = ((url: string) => {
      const path = url.toString();
      if (path === "/api/super_auth/login") {
        return Promise.resolve(jsonResponse({
          success: false,
          errors: [{ code: "AUTH_INVALID_CREDENTIALS", message: "Invalid username or password." }],
        }));
      }
      return Promise.resolve(jsonResponse({ success: false }));
    }) as typeof fetch;

    try {
      render(h(SuperAuthPanel, {}), container);
      await waitFor(() => container.querySelector("form") !== null);

      await submitForm(container, "form");
      await waitFor(() => (container.textContent ?? "").includes("ログインに失敗しました"));
      await flushUpdates();

      const primary = visibleText(container);
      assert(
        primary.includes("ユーザー名またはパスワードが正しくありません"),
        "a friendly Japanese message must be the always-visible primary error text",
      );
      assertFalse(
        primary.includes("Invalid username or password."),
        "the raw backend diagnostic message must not appear in always-visible primary text",
      );
      assertFalse(
        primary.includes("AUTH_INVALID_CREDENTIALS"),
        "the raw error code must not appear in always-visible primary text",
      );

      const technical = technicalDisclosureText(container);
      assert(technical.includes("AUTH_INVALID_CREDENTIALS"));
      assert(technical.includes("Invalid username or password."));
    } finally {
      globalThis.fetch = originalFetch;
      render(null, container);
      cleanup();
    }
  },
);
