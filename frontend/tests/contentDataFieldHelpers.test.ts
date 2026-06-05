import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  generateUuidValue,
  relationUuidCandidatesForColumn,
} from "../lib/contentDataFieldHelpers.ts";

Deno.test("generateUuidValue returns RFC-shaped UUID", () => {
  const id = generateUuidValue();
  assertEquals(/^[0-9a-f-]{36}$/i.test(id), true);
});

Deno.test("relationUuidCandidatesForColumn resolves FK from related remote key column", () => {
  const candidates = relationUuidCandidatesForColumn(
    "employees.user_id",
    [{
      localTableRef: "employees",
      localKey: "user_id",
      joinTableRef: "auth.user",
      remoteKey: "id",
    }],
    [
      { values: { "auth.user.id": "00000000-0000-0000-0000-0000000000a1" }, lineage: { source: "manual" } },
      { values: { "auth.user.id": "00000000-0000-0000-0000-0000000000a2" }, lineage: { source: "manual" } },
    ],
  );
  assertEquals(candidates.includes("00000000-0000-0000-0000-0000000000a1"), true);
  assertEquals(candidates.includes("00000000-0000-0000-0000-0000000000a2"), true);
});
