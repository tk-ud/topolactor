import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";

Deno.test("contentsAssign step 2.5: includes remoteManifestId for active remote targets", () => {
  const design = emptyManifestScreenDesign();
  design.relationIntents = [{
    localTableRef: "local_t",
    localKey: "id",
    joinTableRef: "remote_t",
    remoteKey: "fk",
    remoteManifestId: "00000000-0000-0000-0000-000000000099",
  }];
  design.logicalTables = [{
    tableName: "local_t",
    columns: [{ name: "id", dataType: "text", nullable: true }],
  }];

  const payload = buildAssignPayloadForStep(2.5, "draft-1", design, null);
  assertEquals(payload.relationIntents?.length, 1);
  assertEquals(
    payload.relationIntents![0].remoteManifestId,
    "00000000-0000-0000-0000-000000000099",
  );
});

Deno.test("contentsAssign step 2.5: draft-only remote omits remoteManifestId", () => {
  const design = emptyManifestScreenDesign();
  design.relationIntents = [{
    localTableRef: "a",
    localKey: "id",
    joinTableRef: "b",
    remoteKey: "fk",
  }];
  design.logicalTables = [
    { tableName: "a", columns: [{ name: "id", dataType: "text", nullable: true }] },
    { tableName: "b", columns: [{ name: "fk", dataType: "text", nullable: true }] },
  ];

  const payload = buildAssignPayloadForStep(2.5, "draft-1", design, null);
  assertEquals(payload.relationIntents?.[0].remoteManifestId, undefined);
});
