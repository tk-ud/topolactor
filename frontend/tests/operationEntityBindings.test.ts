import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  columnsForOperationKind,
  isColumnFullySelected,
  isRowFullySelected,
  setColumnAcrossOperationKinds,
  setColumnsForOperationKind,
} from "../lib/operationEntityBindings.ts";
import { operationEntityColumnRole } from "../runtime/screenAuthoringIntent.ts";
import { uxOperationEntityRoleLabel } from "../content/adminUxTerms.ts";

Deno.test("setColumnsForOperationKind: replaces columns for one operation", () => {
  const next = setColumnsForOperationKind([], "list", ["a", "b"]);
  assertEquals(next, [{ operationKind: "list", entityTargetColumns: ["a", "b"] }]);
});

Deno.test("setColumnAcrossOperationKinds: toggles one column across kinds", () => {
  const bindings = [{ operationKind: "list" as const, entityTargetColumns: ["a"] }];
  const added = setColumnAcrossOperationKinds(bindings, "b", ["list", "create"], true);
  assertEquals(columnsForOperationKind(added, "list"), ["a", "b"]);
  assertEquals(columnsForOperationKind(added, "create"), ["b"]);
  const removed = setColumnAcrossOperationKinds(added, "b", ["list", "create"], false);
  assertEquals(columnsForOperationKind(removed, "list"), ["a"]);
  assertEquals(columnsForOperationKind(removed, "create"), []);
});

Deno.test("bulk selection helpers: row and column fully selected", () => {
  const bindings = [
    { operationKind: "list" as const, entityTargetColumns: ["a", "b"] },
    { operationKind: "create" as const, entityTargetColumns: ["a"] },
  ];
  assertEquals(isColumnFullySelected(bindings, "list", ["a", "b"]), true);
  assertEquals(isColumnFullySelected(bindings, "create", ["a", "b"]), false);
  assertEquals(isRowFullySelected(bindings, "a", ["list", "create"]), true);
  assertEquals(isRowFullySelected(bindings, "b", ["list", "create"]), false);
});

Deno.test("operationEntityColumnRole: read vs write vs delete kinds", () => {
  assertEquals(operationEntityColumnRole("list"), "response");
  assertEquals(operationEntityColumnRole("create"), "request");
  assertEquals(operationEntityColumnRole("delete"), "operation_target");
  assertEquals(uxOperationEntityRoleLabel("response"), "表示（Res）");
  assertEquals(uxOperationEntityRoleLabel("request"), "入力（Req）");
});
