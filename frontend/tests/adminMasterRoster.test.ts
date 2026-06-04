import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { ADMIN_ROUTE_CARDS } from "../content/adminGuides.ts";
import { USER_STATUS_ENUM_GROUP_ID } from "../api/adminApi.ts";

Deno.test("ADMIN_ROUTE_CARDS includes master roster routes", () => {
  const hrefs = ADMIN_ROUTE_CARDS.map((c) => c.href);
  assertEquals(hrefs.includes("/admin/enums"), true);
  assertEquals(hrefs.includes("/admin/users"), true);
});

Deno.test("USER_STATUS_ENUM_GROUP_ID matches SSOT seed group", () => {
  assertEquals(USER_STATUS_ENUM_GROUP_ID, "33333333-3333-3333-3333-333333333301");
});

Deno.test("AdminEnumsRoster and AdminUsersRoster islands exist", async () => {
  const enums = await Deno.readTextFile(new URL("../islands/AdminEnumsRoster.tsx", import.meta.url));
  const users = await Deno.readTextFile(new URL("../islands/AdminUsersRoster.tsx", import.meta.url));
  assertEquals(enums.includes("Modal"), true);
  assertEquals(enums.includes("全件出力"), true);
  assertEquals(enums.includes("useConfirm"), true);
  assertEquals(users.includes("readonly"), true);
  assertEquals(users.includes("USER_STATUS_ENUM_GROUP_ID"), true);
});
