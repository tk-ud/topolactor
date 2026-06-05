import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  displayShapePatchFromOperationBindings,
  hydrateOperationBindingsFromLegacyDisplayColumns,
  projectionColumnsForOperationKind,
  projectionColumnsFromOperationBindings,
} from "../lib/screenSampleProjection.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";

Deno.test("projectionColumnsForOperationKind: projects single operation binding only", () => {
  const cols = projectionColumnsForOperationKind(
    "create",
    [
      { operationKind: "list", entityTargetColumns: ["employees.name", "employees.salary"] },
      { operationKind: "create", entityTargetColumns: ["employees.name"] },
    ],
    ["employees.name", "employees.salary", "employees.status"],
  );
  assertEquals(cols, ["employees.name"]);
});

Deno.test("projectionColumnsFromOperationBindings: unions columns across selected operations", () => {
  const cols = projectionColumnsFromOperationBindings(
    ["list", "create"],
    [
      { operationKind: "list", entityTargetColumns: ["employees.name", "employees.salary"] },
      { operationKind: "create", entityTargetColumns: ["employees.name"] },
    ],
    ["employees.name", "employees.salary", "employees.status"],
  );
  assertEquals(cols, ["employees.name", "employees.salary"]);
});

Deno.test("displayShapePatchFromOperationBindings: syncs displayColumns for save", () => {
  const design = emptyManifestScreenDesign();
  design.operationKinds = ["list"];
  design.operationEntityBindings = [{
    operationKind: "list",
    entityTargetColumns: ["a.x"],
  }];
  const patch = displayShapePatchFromOperationBindings(design, ["a.x", "a.y"]);
  assertEquals(patch.displayColumns, ["a.x"]);
  assertEquals(patch.displayColumnMode, "selected");
});

Deno.test("hydrateOperationBindingsFromLegacyDisplayColumns: maps legacy displayColumns to list", () => {
  const design = emptyManifestScreenDesign();
  design.operationKinds = ["list"];
  design.displayColumns = ["employees.name"];
  const next = hydrateOperationBindingsFromLegacyDisplayColumns(design);
  assertEquals(next.operationEntityBindings[0].entityTargetColumns, ["employees.name"]);
});
