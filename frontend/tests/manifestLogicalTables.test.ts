import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  logicalTablesFromLegacyColumns,
  namedColumnsFromLogicalTables,
  normalizeLogicalTables,
  primaryLogicalTableRef,
  primaryTableColumns,
  normalizeRelationKeyColumn,
  qualifyScreenDesignColumnKeys,
  qualifiedColumnKey,
  qualifiedColumnsFromLogicalTables,
  relationKeyColumnOptionsForTableRef,
} from "../lib/manifestLogicalTables.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";

Deno.test("normalizeLogicalTables: legacy columns become single table", () => {
  const tables = normalizeLogicalTables(undefined, [
    { name: "id", dataType: "uuid", nullable: false },
  ]);
  assertEquals(tables.length, 1);
  assertEquals(tables[0].columns[0].name, "id");
});

Deno.test("namedColumnsFromLogicalTables: flattens all tables", () => {
  const cols = namedColumnsFromLogicalTables([
    { tableName: "a", columns: [{ name: "x", dataType: "text", nullable: true }] },
    { tableName: "b", columns: [{ name: "y", dataType: "text", nullable: true }] },
  ]);
  assertEquals(cols.map((c) => c.name), ["x", "y"]);
});

Deno.test("primaryTableColumns: returns first table only", () => {
  const cols = primaryTableColumns(logicalTablesFromLegacyColumns([
    { name: "only", dataType: "text", nullable: true },
  ]));
  assertEquals(cols[0].name, "only");
});

Deno.test("primaryLogicalTableRef: returns first named table", () => {
  assertEquals(
    primaryLogicalTableRef([
      { tableName: "employees", columns: [] },
      { tableName: "departments", columns: [] },
    ]),
    "employees",
  );
  assertEquals(primaryLogicalTableRef([{ tableName: "", columns: [] }]), "");
});

Deno.test("relationKeyColumnOptionsForTableRef: row id as id (record id) before jsonb columns", () => {
  const opts = relationKeyColumnOptionsForTableRef([
    {
      tableName: "user",
      columns: [
        { name: "name", dataType: "jsonb", nullable: true },
        { name: "password", dataType: "jsonb", nullable: true },
        { name: "role", dataType: "jsonb", nullable: true },
      ],
    },
  ], "user");
  assertEquals(opts[0], { value: "id", label: "id (record id)" });
  assertEquals(opts.map((o) => o.value), ["id", "name", "password", "role"]);
});

Deno.test("relationKeyColumnOptionsForTableRef: user-defined id column uses plain label", () => {
  const opts = relationKeyColumnOptionsForTableRef([
    {
      tableName: "user",
      columns: [
        { name: "id", dataType: "uuid", nullable: false },
        { name: "name", dataType: "text", nullable: true },
      ],
    },
  ], "user");
  assertEquals(opts, [
    { value: "id", label: "id" },
    { value: "name", label: "name" },
  ]);
});

Deno.test("normalizeRelationKeyColumn: recordId maps to id", () => {
  assertEquals(normalizeRelationKeyColumn("recordId"), "id");
  assertEquals(normalizeRelationKeyColumn("user_id"), "user_id");
});

Deno.test("qualifiedColumnsFromLogicalTables: distinct keys for same column name", () => {
  const keys = qualifiedColumnsFromLogicalTables([
    {
      tableName: "employees",
      columns: [{ name: "name", dataType: "text", nullable: true }],
    },
    {
      tableName: "user",
      columns: [{ name: "name", dataType: "jsonb", nullable: true }],
    },
  ]).map((q) => q.key);
  assertEquals(keys, ["employees.name", "user.name"]);
});

Deno.test("qualifyScreenDesignColumnKeys: splits shared bare name into per-table keys", () => {
  const design = emptyManifestScreenDesign();
  design.logicalTables = [
    {
      tableName: "employees",
      columns: [{ name: "name", dataType: "text", nullable: true }],
    },
    {
      tableName: "user",
      columns: [{ name: "name", dataType: "jsonb", nullable: true }],
    },
  ];
  design.displayColumns = ["name"];
  design.initialDataRows = [{ name: "shared" }];
  const next = qualifyScreenDesignColumnKeys(design);
  assertEquals(next.displayColumns, ["employees.name"]);
  assertEquals(next.initialDataRows[0]["employees.name"], "");
  assertEquals(next.initialDataRows[0]["user.name"], "");
  assertEquals(qualifiedColumnKey("employees", "name"), "employees.name");
});
