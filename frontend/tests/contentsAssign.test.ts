import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import {
  emptyManifestScreenDesign,
  patchAggregationBlocks,
} from "../lib/manifestScreenDesign.ts";
import type { ScreenDataShapeSummary } from "../lib/manifestTopologyExtensions.ts";

const manifestId = "00000000-0000-0000-0000-000000000099";

const existing: ScreenDataShapeSummary = {
  tableRef: "orders",
  importSchemaName: "public",
  searchTargets: ["id"],
  searchKeyColumns: ["id"],
  aggregationSpec: null,
  aggregationKey: null,
  aggregationColumns: [],
  aggregationFunction: null,
  aggregationMeasures: [],
  aggregationBlocks: [],
  displayColumns: ["id"],
  logicalTables: [],
  screenOperationKind: "list",
  screenOperationKinds: ["list"],
  userFacingTopologyLabel: "注文",
  columns: [{ name: "id", dataType: "uuid", nullable: false }],
  relationIntents: [{ localTableRef: "orders", joinTableRef: "customers", localKey: "customer_id", remoteKey: "id" }],
  operationEntityBindings: [],
  initialDataRows: [],
  searchConditions: [],
  havingConditions: [],
  displayColumnMode: null,
};

Deno.test("buildAssignPayloadForStep step 2: logicalTables from draft, preserves existing relations", () => {
  const design = emptyManifestScreenDesign();
  design.logicalTables = [{
    tableName: "lines",
    columns: [
      { name: "sku", dataType: "text", nullable: false },
      { name: "qty", dataType: "integer", nullable: false },
    ],
  }];
  design.relationIntents = [];
  design.tableRef = "ignored_table";

  const payload = buildAssignPayloadForStep(2, manifestId, design, existing);
  assertEquals(payload.logicalTables?.length, 1);
  assertEquals(payload.logicalTables?.[0].columns.map((c) => c.name), ["sku", "qty"]);
  assertEquals(payload.columns?.map((c) => c.name), ["sku", "qty"]);
  assertEquals(payload.relationIntents, existing.relationIntents);
  assertEquals(payload.tableRef, "orders");
  assertEquals(payload.searchTargets, existing.searchTargets);
});

Deno.test("buildAssignPayloadForStep step 2: strips enumGroupId when dataType is not enum", () => {
  const design = emptyManifestScreenDesign();
  const groupId = "22222222-2222-2222-2222-222222222201";
  design.logicalTables = [{
    tableName: "employees",
    columns: [
      { name: "status", dataType: "text", nullable: true, enumGroupId: groupId },
    ],
  }];

  const payload = buildAssignPayloadForStep(2, manifestId, design, existing);
  assertEquals(payload.logicalTables?.[0].columns[0].enumGroupId, undefined);
});

Deno.test("buildAssignPayloadForStep step 2: preserves enumGroupId on enum columns", () => {
  const design = emptyManifestScreenDesign();
  const groupId = "22222222-2222-2222-2222-222222222201";
  design.logicalTables = [{
    tableName: "employees",
    columns: [
      { name: "status", dataType: "enum", nullable: true, enumGroupId: groupId },
    ],
  }];

  const payload = buildAssignPayloadForStep(2, manifestId, design, existing);
  assertEquals(payload.logicalTables?.[0].columns[0].enumGroupId, groupId);
});

Deno.test("buildAssignPayloadForStep step 2.5: relationIntents only from draft", () => {
  const design = emptyManifestScreenDesign();
  design.relationIntents = [
    { localTableRef: "orders", joinTableRef: "lines", localKey: "order_id", remoteKey: "id" },
  ];
  design.columns = [{ name: "wip", dataType: "text", nullable: true }];

  const payload = buildAssignPayloadForStep(2.5, manifestId, design, existing);
  assertEquals(payload.relationIntents?.length, 1);
  assertEquals(payload.relationIntents?.[0].joinTableRef, "lines");
  assertEquals(payload.columns, existing.columns);
});

Deno.test("buildAssignPayloadForStep step 3: tableRef from primary logical table, columns from existing", () => {
  const design = emptyManifestScreenDesign();
  design.logicalTables = [{
    tableName: "order_lines",
    columns: [
      { name: "sku", dataType: "text", nullable: false },
      { name: "qty", dataType: "integer", nullable: false },
    ],
  }];
  design.tableRef = "ignored_manual";
  design.importSchemaName = "ignored_manual";
  design.operationKinds = ["create", "update"];
  design.searchKeyColumns = ["sku"];
  design.displayColumns = [];
  design.aggregationBlocks = [
    {
      sourceRef: "order_lines",
      aggregationKey: "",
      measures: [
        { column: "order_lines.qty", function: "sum" },
        { column: "order_lines.qty", function: "max" },
      ],
      searchConditions: [],
      havingConditions: [],
    },
  ];
  Object.assign(design, patchAggregationBlocks(design.aggregationBlocks, "order_lines"));
  design.operationEntityBindings = [{
    operationKind: "create",
    entityTargetColumns: ["sku", "qty"],
  }];
  design.columns = [{ name: "should_not_win", dataType: "text", nullable: true }];

  const payload = buildAssignPayloadForStep(3, manifestId, design, existing);
  assertEquals(payload.tableRef, "order_lines");
  assertEquals(payload.importSchemaName, "public");
  assertEquals(payload.screenOperationKinds, ["create", "update"]);
  assertEquals(payload.searchKeyColumns, ["sku"]);
  assertEquals(payload.displayColumns, []);
  assertEquals(payload.aggregationMeasures?.length, 2);
  assertEquals(payload.aggregationMeasures?.[0].function, "sum");
  assertEquals(payload.aggregationBlocks?.length, 1);
  assertEquals(payload.aggregationBlocks?.[0].sourceRef, "order_lines");
  assertEquals(
    payload.operationEntityBindings?.[0].entityTargetColumns,
    ["sku", "qty"],
  );
  assertEquals(payload.columns, existing.columns);
  assertEquals(payload.relationIntents, existing.relationIntents);
});
