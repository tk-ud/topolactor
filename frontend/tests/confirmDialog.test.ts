import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h } from "preact";
import { render } from "preact";
import { ConfirmDialog } from "../components/ConfirmDialog.tsx";
import { setupDom } from "./test-dom-setup.ts";

Deno.test("ConfirmDialog: closed renders nothing", () => {
  const { container, cleanup } = setupDom();
  try {
    render(
      h(ConfirmDialog, {
        open: false,
        message: "テスト",
        onConfirm: () => {},
        onCancel: () => {},
      }),
      container,
    );
    assertEquals(container.innerHTML, "");
  } finally {
    cleanup();
  }
});

Deno.test("ConfirmDialog: open renders message and alertdialog role", () => {
  const { container, cleanup } = setupDom();
  try {
    render(
      h(ConfirmDialog, {
        open: true,
        message: "保存します。よろしいですか？",
        onConfirm: () => {},
        onCancel: () => {},
      }),
      container,
    );
    const html = container.innerHTML;
    assertFalse(html.length === 0);
    assertEquals(html.includes("保存します。よろしいですか？"), true);
    assertEquals(html.includes('role="alertdialog"'), true);
  } finally {
    cleanup();
  }
});
