import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  adminSubmitConfirmMessage,
  adminSubmitStatusClass,
  initialAdminSubmitStatus,
} from "../lib/adminSubmitUx.ts";

Deno.test("adminSubmitStatusClass: success uses alert-success", () => {
  assertEquals(adminSubmitStatusClass("success"), "alert-success");
});

Deno.test("initialAdminSubmitStatus: idle with no message", () => {
  const s = initialAdminSubmitStatus();
  assertEquals(s.outcome, "idle");
  assertEquals(s.message, null);
});

Deno.test("adminSubmitConfirmMessage: built-in kind messages", () => {
  assertEquals(adminSubmitConfirmMessage("save"), "保存します。よろしいですか？");
  assertEquals(
    adminSubmitConfirmMessage("create", "カスタム確認"),
    "カスタム確認",
  );
});
