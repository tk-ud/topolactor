import { assertEquals, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { Window } from "happy-dom";
import { h } from "preact";
import { render } from "preact";
import { ConfirmDialog } from "../components/ConfirmDialog.tsx";

Deno.test("ConfirmDialog: closed renders nothing", () => {
  const window = new Window();
  const document = window.document;
  const container = document.createElement("div");
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
});

Deno.test("ConfirmDialog: open renders message and alertdialog role", () => {
  const window = new Window();
  const document = window.document;
  const container = document.createElement("div");
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
});
