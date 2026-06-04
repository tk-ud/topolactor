import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { AUTH_RELATIONSHIP_BOUNDARY_MANIFEST_ID } from "../api/authApi.ts";

Deno.test("auth relationship boundary seed: manifest 091 with auth.user logical table", async () => {
  const sql = await Deno.readTextFile(
    new URL("../../db/seed_empty.sql", import.meta.url),
  );
  assert(
    sql.includes(AUTH_RELATIONSHIP_BOUNDARY_MANIFEST_ID),
    `seed_empty.sql must contain manifest ${AUTH_RELATIONSHIP_BOUNDARY_MANIFEST_ID}`,
  );
  assert(sql.includes('"tableName":"auth.user"'), "boundary manifest must define auth.user");
  assert(sql.includes('"name":"id"'), "auth.user must expose id column for remote joins");
  assert(sql.includes("auth.user.boundary"), "boundary manifest must have hub_grouping manifestKey");
});
