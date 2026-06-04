import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { USER_LOGIN_MANIFEST_ID } from "../api/authApi.ts";

const MANIFEST_ID = USER_LOGIN_MANIFEST_ID;

Deno.test("user login manifest seed: manifest 090 exists in seed_empty.sql", async () => {
  const sql = await Deno.readTextFile(
    new URL("../../db/seed_empty.sql", import.meta.url),
  );
  assert(sql.includes(MANIFEST_ID), `seed_empty.sql must contain manifest ${MANIFEST_ID}`);
  assert(sql.includes("auth_action_binding"), "login manifest must have auth_action_binding");
  assert(sql.includes("auth_runtime"), "auth_action_binding must target auth_runtime");
  assert(sql.includes("ui_projection_mapping"), "login manifest must have ui_projection_mapping");
  assert(sql.includes('"realm":"user"'), "user login binding must use realm user");
  assert(!sql.includes("password_hash"), "login manifest seed must not contain password_hash");
});
