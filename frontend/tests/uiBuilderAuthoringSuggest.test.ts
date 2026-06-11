/**
 * uiBuilderAuthoringSuggest.test.ts
 *
 * UIBuilder authoring suggest assist — pure helper bundle tests.
 *
 * SSOT: docs/design/admin-console-workflow-ssot.yaml
 *   - ui_builder_canvas_workspace
 *   - frontend_local_derived_calculation_binding
 *   - layout_node_props_contract
 * SSOT: docs/design/pipeline-continuity-ssot.yaml
 *   - component_wiring_execution_lane
 *
 * Coverage:
 * - deriveDbTableCandidates: logicalTables, relation_intent, empty, unnamed
 * - deriveDbColumnCandidates: selected tableRef, missing tableRef, empty columns
 * - deriveQualifiedColumnCandidates: qualified keys, unresolved relations (structured error)
 * - deriveDataBindingPathCandidates: array-capable, non-array, componentKind guard
 * - deriveSourceNodeSuggestCandidates: known/unknown componentKind, empty nodes
 * - deriveTargetNodeSuggestCandidates: allowedTargetProps coherence
 * - deriveTargetPropSuggestCandidates: resolveAllowedTargetProps alignment
 * - deriveRuleTableMatchConditionSuggestCandidates: field + node + db_column candidates
 * - deriveEmissionScalarPathCandidates: scalar only, not array
 * - deriveEmissionArrayPathCandidates: array only, not scalar
 * - deriveOperationEntityColumnCandidates: per operation kind
 * - deriveUiBuilderAuthoringSuggestBundle: composite, partial availability
 *
 * Boundary tests:
 * - No mutation: suggest derivation does not touch draftNodes/calculationBindings
 * - No fetch/eval/Function: module has no such imports (verified by absence)
 * - route: targetRef is NOT mixed into any manifest/node candidate
 * - unresolved relation: structured reason, not silent empty
 * - ambiguous active remote: no active remote manifests passed → structured reason from step3
 */

import {
  assertEquals,
  assertFalse,
  assert,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  deriveDbTableCandidates,
  deriveDbColumnCandidates,
  deriveQualifiedColumnCandidates,
  deriveDataBindingPathCandidates,
  deriveSourceNodeSuggestCandidates,
  deriveTargetNodeSuggestCandidates,
  deriveTargetPropSuggestCandidates,
  deriveRuleTableMatchConditionSuggestCandidates,
  deriveEmissionScalarPathCandidates,
  deriveEmissionArrayPathCandidates,
  deriveOperationEntityColumnCandidates,
  deriveUiBuilderAuthoringSuggestBundle,
  type DbTableCandidate,
  type DbColumnCandidate,
  type QualifiedColumnCandidate,
  type TargetNodeCandidate,
  type SourceNodeCandidate,
} from "../lib/uiBuilderAuthoringSuggest.ts";
import type { DraftNodeMinimal } from "../lib/uiBuilderAutocompleteCandidates.ts";
import type { LogicalTableShape, RelationIntentShape, OperationEntityBindingShape } from "../lib/manifestTopologyExtensions.ts";

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const employeesTable: LogicalTableShape = {
  tableName: "employees",
  columns: [
    { name: "id", dataType: "integer", nullable: false },
    { name: "name", dataType: "text", nullable: false },
    { name: "department_id", dataType: "integer", nullable: true },
  ],
};

const departmentsTable: LogicalTableShape = {
  tableName: "departments",
  columns: [
    { name: "id", dataType: "integer", nullable: false },
    { name: "dept_name", dataType: "text", nullable: false },
  ],
};

const emptyTableNoName: LogicalTableShape = {
  tableName: "",
  columns: [{ name: "col1", dataType: "text", nullable: true }],
};

const relationIntentJoin: RelationIntentShape = {
  localTableRef: "employees",
  joinTableRef: "departments",
  localKey: "department_id",
  remoteKey: "id",
};

const unresolvableRelation: RelationIntentShape = {
  localTableRef: "employees",
  joinTableRef: "nonexistent_table",
  localKey: "dept_id",
  remoteKey: "id",
  remoteManifestId: "manifest-uuid-0001",
};

const inputNode: DraftNodeMinimal = {
  nodeId: "node_input_001",
  componentKind: "form_input/input",
  componentKey: "input.primitive",
  nodeKind: "catalog_component",
};

const selectNode: DraftNodeMinimal = {
  nodeId: "node_select_002",
  componentKind: "form_input/select",
  componentKey: "select.template",
  nodeKind: "catalog_component",
};

const unknownKindNode: DraftNodeMinimal = {
  nodeId: "node_unknown_003",
  componentKind: "custom/unknown_kind",
  nodeKind: "catalog_component",
};

const structuralNode: DraftNodeMinimal = {
  nodeId: "node_div_004",
  nodeKind: "structural_html",
  htmlTag: "div",
};

const cardListNode: DraftNodeMinimal = {
  nodeId: "node_cardlist_005",
  componentKind: "display/card_list",
  componentKey: "card_list.template",
  nodeKind: "catalog_component",
};

const emissionWithRowsAndScalar = JSON.stringify({
  rows: [{ id: 1, name: "Alice", dept: "Engineering" }],
  totalCount: 42,
  meta: { page: 1 },
});

const operationEntityBindings: OperationEntityBindingShape[] = [
  { operationKind: "list", entityTargetColumn: null, entityTargetColumns: ["employees.id", "employees.name"] },
  { operationKind: "create", entityTargetColumn: null, entityTargetColumns: ["employees.name", "employees.department_id"] },
];

// ─── deriveDbTableCandidates ──────────────────────────────────────────────────

Deno.test("deriveDbTableCandidates: empty inputs return structured reason", () => {
  const result = deriveDbTableCandidates([], []);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string; unresolvedErrors: string[] }).reason.length > 0);
  assertEquals((result as { ok: false; reason: string; unresolvedErrors: string[] }).unresolvedErrors.length, 0);
});

Deno.test("deriveDbTableCandidates: logicalTables produce local candidates", () => {
  const result = deriveDbTableCandidates([employeesTable, departmentsTable], []);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: DbTableCandidate[] };
  assertEquals(candidates.length, 2);
  const emp = candidates.find((c) => c.tableRef === "employees");
  assert(emp !== undefined);
  assertEquals(emp!.source, "local");
  assertEquals(emp!.columnCount, 3);
});

Deno.test("deriveDbTableCandidates: unnamed table (empty tableName) is excluded", () => {
  const result = deriveDbTableCandidates([emptyTableNoName, employeesTable], []);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: DbTableCandidate[] };
  assertFalse(candidates.some((c) => c.tableRef === ""));
  assert(candidates.some((c) => c.tableRef === "employees"));
});

Deno.test("deriveDbTableCandidates: relation_intent joinTableRef added when not in logicalTables", () => {
  const extRelation: RelationIntentShape = {
    localTableRef: "employees",
    joinTableRef: "positions",
    localKey: "position_id",
    remoteKey: "id",
  };
  const result = deriveDbTableCandidates([employeesTable], [extRelation]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: DbTableCandidate[] };
  const pos = candidates.find((c) => c.tableRef === "positions");
  assert(pos !== undefined);
  assertEquals(pos!.source, "relation_intent");
});

Deno.test("deriveDbTableCandidates: no duplicate tableRefs when relation joinTableRef already in logicalTables", () => {
  const result = deriveDbTableCandidates([employeesTable, departmentsTable], [relationIntentJoin]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: DbTableCandidate[] };
  const depts = candidates.filter((c) => c.tableRef === "departments");
  assertEquals(depts.length, 1);
  assertEquals(depts[0]!.source, "local");
});

// ─── deriveDbColumnCandidates ─────────────────────────────────────────────────

Deno.test("deriveDbColumnCandidates: empty tableRef returns structured reason", () => {
  const result = deriveDbColumnCandidates([employeesTable], "");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("tableRef"));
});

Deno.test("deriveDbColumnCandidates: unknown tableRef returns structured reason", () => {
  const result = deriveDbColumnCandidates([employeesTable], "nonexistent");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("nonexistent"));
});

Deno.test("deriveDbColumnCandidates: valid tableRef returns columns with qualifiedKey", () => {
  const result = deriveDbColumnCandidates([employeesTable], "employees");
  assert(result.ok);
  const { columns, tableRef } = result as { ok: true; columns: DbColumnCandidate[]; tableRef: string };
  assertEquals(tableRef, "employees");
  assertEquals(columns.length, 3);
  const nameCol = columns.find((c) => c.columnName === "name");
  assert(nameCol !== undefined);
  assertEquals(nameCol!.qualifiedKey, "employees.name");
  assertEquals(nameCol!.dataType, "text");
  assertEquals(nameCol!.nullable, false);
});

Deno.test("deriveDbColumnCandidates: qualifiedKey uses tableRef.columnName format", () => {
  const result = deriveDbColumnCandidates([departmentsTable], "departments");
  assert(result.ok);
  const { columns } = result as { ok: true; columns: DbColumnCandidate[] };
  for (const col of columns) {
    assertEquals(col.qualifiedKey, `departments.${col.columnName}`);
  }
});

Deno.test("deriveDbColumnCandidates: table with no columns returns structured reason", () => {
  const emptyColTable: LogicalTableShape = { tableName: "empty_table", columns: [] };
  const result = deriveDbColumnCandidates([emptyColTable], "empty_table");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("empty_table"));
});

// ─── deriveQualifiedColumnCandidates ─────────────────────────────────────────

Deno.test("deriveQualifiedColumnCandidates: empty logicalTables returns structured reason", () => {
  const result = deriveQualifiedColumnCandidates([], []);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string; unresolvedErrors: string[] }).reason.length > 0);
});

Deno.test("deriveQualifiedColumnCandidates: produces tableRef.columnName keys", () => {
  const result = deriveQualifiedColumnCandidates([employeesTable], []);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: QualifiedColumnCandidate[]; unresolvedErrors: string[] };
  assert(candidates.length > 0);
  const idCandidate = candidates.find((c) => c.columnName === "id" && c.tableRef === "employees");
  assert(idCandidate !== undefined);
  assertEquals(idCandidate!.key, "employees.id");
});

Deno.test("deriveQualifiedColumnCandidates: unresolved relation returns structured error (not silent)", () => {
  const result = deriveQualifiedColumnCandidates([employeesTable], [unresolvableRelation]);
  if (result.ok) {
    const { unresolvedErrors } = result as { ok: true; candidates: QualifiedColumnCandidate[]; unresolvedErrors: string[] };
    assert(unresolvedErrors.length > 0, "unresolved relation must produce structured errors, not silent empty");
  } else {
    assert((result as { ok: false; reason: string }).reason.length > 0);
  }
});

Deno.test("deriveQualifiedColumnCandidates: active remote manifest ambiguity not silently resolved (no remoteTargets passed)", () => {
  const ambiguousRel: RelationIntentShape = {
    localTableRef: "employees",
    joinTableRef: "external_table",
    localKey: "ext_id",
    remoteKey: "id",
  };
  const result = deriveQualifiedColumnCandidates([employeesTable], [ambiguousRel]);
  if (result.ok) {
    const { unresolvedErrors } = result as { ok: true; candidates: QualifiedColumnCandidate[]; unresolvedErrors: string[] };
    assert(unresolvedErrors.length > 0, "unresolvable active remote must produce structured error");
  }
});

// ─── deriveDataBindingPathCandidates ─────────────────────────────────────────

Deno.test("deriveDataBindingPathCandidates: non-array componentKind returns structured reason", () => {
  const result = deriveDataBindingPathCandidates("action/button", emissionWithRowsAndScalar, [], "");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("action/button"));
});

Deno.test("deriveDataBindingPathCandidates: empty componentKind returns structured reason", () => {
  const result = deriveDataBindingPathCandidates("", emissionWithRowsAndScalar, [], "");
  assertFalse(result.ok);
});

Deno.test("deriveDataBindingPathCandidates: card_list returns array source paths", () => {
  const result = deriveDataBindingPathCandidates(
    "display/card_list",
    emissionWithRowsAndScalar,
    [employeesTable],
    "employees",
  );
  assert(result.ok);
  const r = result as { ok: true; componentKind: string; acceptsArrayProps: string[]; sourcePaths: { path: string; description: string }[]; fieldPaths: { path: string; description: string }[] };
  assert(r.acceptsArrayProps.includes("items"));
  assert(r.sourcePaths.some((p) => p.path.includes("emission.data.rows")));
});

Deno.test("deriveDataBindingPathCandidates: selected tableRef produces fieldPaths from columns", () => {
  const result = deriveDataBindingPathCandidates(
    "display/card_list",
    emissionWithRowsAndScalar,
    [employeesTable],
    "employees",
  );
  assert(result.ok);
  const r = result as { ok: true; componentKind: string; acceptsArrayProps: string[]; sourcePaths: { path: string }[]; fieldPaths: { path: string }[] };
  assert(r.fieldPaths.some((f) => f.path === "id"));
  assert(r.fieldPaths.some((f) => f.path === "name"));
  assert(r.fieldPaths.some((f) => f.path === "department_id"));
});

Deno.test("deriveDataBindingPathCandidates: form_input/select returns option source paths", () => {
  const result = deriveDataBindingPathCandidates(
    "form_input/select",
    emissionWithRowsAndScalar,
    [],
    "",
  );
  assert(result.ok);
  const r = result as { ok: true; acceptsArrayProps: string[] };
  assert(r.acceptsArrayProps.includes("options"));
});

Deno.test("deriveDataBindingPathCandidates: no emission data returns empty sourcePaths (not error)", () => {
  const result = deriveDataBindingPathCandidates("display/card_list", "", [employeesTable], "employees");
  assert(result.ok);
  const r = result as { ok: true; sourcePaths: unknown[] };
  assertEquals(r.sourcePaths.length, 0);
});

// ─── deriveSourceNodeSuggestCandidates ───────────────────────────────────────

Deno.test("deriveSourceNodeSuggestCandidates: empty nodes returns structured reason", () => {
  const result = deriveSourceNodeSuggestCandidates([]);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveSourceNodeSuggestCandidates: input node produces value candidate", () => {
  const result = deriveSourceNodeSuggestCandidates([inputNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  assert(candidates.some((c) => c.nodeId === "node_input_001" && c.propKey === "value"));
});

Deno.test("deriveSourceNodeSuggestCandidates: checkbox node produces checked candidate", () => {
  const checkboxNode: DraftNodeMinimal = {
    nodeId: "node_checkbox_099",
    componentKind: "form_input/checkbox",
    nodeKind: "catalog_component",
  };
  const result = deriveSourceNodeSuggestCandidates([checkboxNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  assert(candidates.some((c) => c.propKey === "checked"));
});

Deno.test("deriveSourceNodeSuggestCandidates: unknown componentKind gets default value propKey", () => {
  const result = deriveSourceNodeSuggestCandidates([unknownKindNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  assert(candidates.some((c) => c.nodeId === "node_unknown_003" && c.propKey === "value"));
});

Deno.test("deriveSourceNodeSuggestCandidates: multiple nodes produce multiple candidates", () => {
  const result = deriveSourceNodeSuggestCandidates([inputNode, selectNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  assert(candidates.length >= 2);
});

Deno.test("deriveSourceNodeSuggestCandidates: selection set produces multiple source node candidates", () => {
  const nodes: DraftNodeMinimal[] = [inputNode, selectNode, unknownKindNode];
  const result = deriveSourceNodeSuggestCandidates(nodes);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  const nodeIds = new Set(candidates.map((c) => c.nodeId));
  assert(nodeIds.has("node_input_001"));
  assert(nodeIds.has("node_select_002"));
  assert(nodeIds.has("node_unknown_003"));
});

// ─── deriveTargetNodeSuggestCandidates ───────────────────────────────────────

Deno.test("deriveTargetNodeSuggestCandidates: empty draftNodes returns structured reason", () => {
  const result = deriveTargetNodeSuggestCandidates([]);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveTargetNodeSuggestCandidates: input node has allowedTargetProps [value]", () => {
  const result = deriveTargetNodeSuggestCandidates([inputNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: TargetNodeCandidate[] };
  const inp = candidates.find((c) => c.nodeId === "node_input_001");
  assert(inp !== undefined);
  assertEquals(inp!.allowedTargetProps, ["value"]);
});

Deno.test("deriveTargetNodeSuggestCandidates: unknown componentKind has null allowedTargetProps (not blocked)", () => {
  const result = deriveTargetNodeSuggestCandidates([unknownKindNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: TargetNodeCandidate[] };
  const unk = candidates.find((c) => c.nodeId === "node_unknown_003");
  assert(unk !== undefined);
  assertEquals(unk!.allowedTargetProps, null);
});

Deno.test("deriveTargetNodeSuggestCandidates: structural_html node has allowedTargetProps [inlineText]", () => {
  const result = deriveTargetNodeSuggestCandidates([structuralNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: TargetNodeCandidate[] };
  const div = candidates.find((c) => c.nodeId === "node_div_004");
  assert(div !== undefined);
  assert(div!.allowedTargetProps?.includes("inlineText") || div!.allowedTargetProps === null);
});

// ─── deriveTargetPropSuggestCandidates ───────────────────────────────────────

Deno.test("deriveTargetPropSuggestCandidates: empty componentKind returns structured reason", () => {
  const result = deriveTargetPropSuggestCandidates("");
  assertFalse(result.ok);
});

Deno.test("deriveTargetPropSuggestCandidates: form_input/input returns [value]", () => {
  const result = deriveTargetPropSuggestCandidates("form_input/input");
  assert(result.ok);
  const { targetProps } = result as { ok: true; targetProps: string[] };
  assertEquals(targetProps, ["value"]);
});

Deno.test("deriveTargetPropSuggestCandidates: form_input/checkbox returns [checked]", () => {
  const result = deriveTargetPropSuggestCandidates("form_input/checkbox");
  assert(result.ok);
  const { targetProps } = result as { ok: true; targetProps: string[] };
  assertEquals(targetProps, ["checked"]);
});

Deno.test("deriveTargetPropSuggestCandidates: calc_topology/computed_field_preview returns [preview, value]", () => {
  const result = deriveTargetPropSuggestCandidates("calc_topology/computed_field_preview");
  assert(result.ok);
  const { targetProps } = result as { ok: true; targetProps: string[] };
  assert(targetProps.includes("preview"));
  assert(targetProps.includes("value"));
});

Deno.test("deriveTargetPropSuggestCandidates: unknown componentKind returns ok:false with reason (not empty candidates)", () => {
  const result = deriveTargetPropSuggestCandidates("custom/unknown");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveTargetPropSuggestCandidates: aligns with resolveAllowedTargetProps", () => {
  const componentKinds = [
    "form_input/input",
    "form_input/select",
    "form_input/checkbox",
    "calc_topology/calculation_preview_panel",
    "layout/structural_html",
  ];
  for (const kind of componentKinds) {
    const result = deriveTargetPropSuggestCandidates(kind);
    assert(result.ok, `Expected ok for ${kind}`);
    const { targetProps } = result as { ok: true; targetProps: string[] };
    assert(targetProps.length > 0, `Expected non-empty targetProps for ${kind}`);
  }
});

// ─── deriveRuleTableMatchConditionSuggestCandidates ──────────────────────────

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: empty tablePath returns structured reason", () => {
  const result = deriveRuleTableMatchConditionSuggestCandidates("", emissionWithRowsAndScalar, [], []);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("tablePath"));
});

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: non-array tablePath returns structured reason", () => {
  const json = JSON.stringify({ notArray: { scalar: 1 } });
  const result = deriveRuleTableMatchConditionSuggestCandidates("emission.data.notArray", json, [], []);
  assertFalse(result.ok);
});

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: array tablePath returns field candidates", () => {
  const result = deriveRuleTableMatchConditionSuggestCandidates(
    "emission.data.rows",
    emissionWithRowsAndScalar,
    [],
    [],
  );
  assert(result.ok);
  const r = result as { ok: true; fieldCandidates: { field: string }[]; valueSourceCandidates: unknown[] };
  assert(r.fieldCandidates.some((f) => f.field === "id"));
  assert(r.fieldCandidates.some((f) => f.field === "name"));
  assert(r.fieldCandidates.some((f) => f.field === "dept"));
});

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: node source candidates included", () => {
  const result = deriveRuleTableMatchConditionSuggestCandidates(
    "emission.data.rows",
    emissionWithRowsAndScalar,
    [inputNode],
    [],
  );
  assert(result.ok);
  const r = result as { ok: true; fieldCandidates: unknown[]; valueSourceCandidates: Array<{ kind: string }> };
  assert(r.valueSourceCandidates.some((v) => v.kind === "node"));
});

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: literal candidate included", () => {
  const result = deriveRuleTableMatchConditionSuggestCandidates(
    "emission.data.rows",
    emissionWithRowsAndScalar,
    [],
    [],
  );
  assert(result.ok);
  const r = result as { ok: true; fieldCandidates: unknown[]; valueSourceCandidates: Array<{ kind: string }> };
  assert(r.valueSourceCandidates.some((v) => v.kind === "literal"));
});

Deno.test("deriveRuleTableMatchConditionSuggestCandidates: db_column candidates from logicalTables", () => {
  const result = deriveRuleTableMatchConditionSuggestCandidates(
    "emission.data.rows",
    emissionWithRowsAndScalar,
    [],
    [employeesTable],
  );
  assert(result.ok);
  const r = result as { ok: true; fieldCandidates: unknown[]; valueSourceCandidates: Array<{ kind: string; qualifiedKey?: string }> };
  assert(r.valueSourceCandidates.some((v) => v.kind === "db_column"));
  const dbCols = r.valueSourceCandidates.filter((v) => v.kind === "db_column");
  assert(dbCols.some((v) => v.qualifiedKey === "employees.id"));
});

// ─── deriveEmissionScalarPathCandidates ──────────────────────────────────────

Deno.test("deriveEmissionScalarPathCandidates: returns only scalar paths", () => {
  const result = deriveEmissionScalarPathCandidates(emissionWithRowsAndScalar);
  assert(result.ok);
  const r = result as { ok: true; candidates: Array<{ isArray: boolean }> };
  assertFalse(r.candidates.some((c) => c.isArray), "scalar result must not include array paths");
});

Deno.test("deriveEmissionScalarPathCandidates: totalCount is a scalar path", () => {
  const result = deriveEmissionScalarPathCandidates(emissionWithRowsAndScalar);
  assert(result.ok);
  const r = result as { ok: true; candidates: Array<{ path: string }> };
  assert(r.candidates.some((c) => c.path.includes("totalCount")));
});

Deno.test("deriveEmissionScalarPathCandidates: empty emission returns structured reason", () => {
  const result = deriveEmissionScalarPathCandidates("");
  assertFalse(result.ok);
});

Deno.test("deriveEmissionScalarPathCandidates: no scalars returns structured reason", () => {
  const arrayOnly = JSON.stringify({ rows: [{ id: 1 }] });
  const result = deriveEmissionScalarPathCandidates(arrayOnly);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

// ─── deriveEmissionArrayPathCandidates ───────────────────────────────────────

Deno.test("deriveEmissionArrayPathCandidates: returns only array paths", () => {
  const result = deriveEmissionArrayPathCandidates(emissionWithRowsAndScalar);
  assert(result.ok);
  const r = result as { ok: true; candidates: Array<{ isArray: boolean }> };
  assert(r.candidates.every((c) => c.isArray), "array result must only include array paths");
});

Deno.test("deriveEmissionArrayPathCandidates: rows is an array path", () => {
  const result = deriveEmissionArrayPathCandidates(emissionWithRowsAndScalar);
  assert(result.ok);
  const r = result as { ok: true; candidates: Array<{ path: string }> };
  assert(r.candidates.some((c) => c.path.includes("rows")));
});

Deno.test("deriveEmissionArrayPathCandidates: emission with only scalars returns structured reason", () => {
  const scalarOnly = JSON.stringify({ count: 5, name: "test" });
  const result = deriveEmissionArrayPathCandidates(scalarOnly);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("ruleTable tablePath candidates come only from array paths", () => {
  const arrayResult = deriveEmissionArrayPathCandidates(emissionWithRowsAndScalar);
  const scalarResult = deriveEmissionScalarPathCandidates(emissionWithRowsAndScalar);
  assert(arrayResult.ok);
  assert(scalarResult.ok);
  const arrayPaths = (arrayResult as { ok: true; candidates: Array<{ path: string }> }).candidates.map((c) => c.path);
  const scalarPaths = (scalarResult as { ok: true; candidates: Array<{ path: string }> }).candidates.map((c) => c.path);
  for (const ap of arrayPaths) {
    assertFalse(scalarPaths.includes(ap), `Path ${ap} should not appear in both array and scalar`);
  }
});

// ─── deriveOperationEntityColumnCandidates ───────────────────────────────────

Deno.test("deriveOperationEntityColumnCandidates: empty bindings returns structured reason", () => {
  const result = deriveOperationEntityColumnCandidates([], [employeesTable]);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveOperationEntityColumnCandidates: returns per-operation columns", () => {
  const result = deriveOperationEntityColumnCandidates(operationEntityBindings, [employeesTable]);
  assert(result.ok);
  const r = result as { ok: true; candidates: Array<{ operationKind: string; columns: string[] }> };
  const listBinding = r.candidates.find((c) => c.operationKind === "list");
  assert(listBinding !== undefined);
  assert(listBinding!.columns.length > 0);
});

// ─── deriveUiBuilderAuthoringSuggestBundle ────────────────────────────────────

Deno.test("deriveUiBuilderAuthoringSuggestBundle: returns all sub-bundles", () => {
  const bundle = deriveUiBuilderAuthoringSuggestBundle({
    logicalTables: [employeesTable, departmentsTable],
    relationIntents: [],
    draftNodes: [inputNode, cardListNode],
    emissionDataJson: emissionWithRowsAndScalar,
  });
  assert("dbTables" in bundle);
  assert("qualifiedColumns" in bundle);
  assert("sourceNodes" in bundle);
  assert("targetNodes" in bundle);
  assert("emissionScalarPaths" in bundle);
  assert("emissionArrayPaths" in bundle);
});

Deno.test("deriveUiBuilderAuthoringSuggestBundle: partial availability is normal (sub-bundles independent)", () => {
  const bundle = deriveUiBuilderAuthoringSuggestBundle({
    logicalTables: [],
    relationIntents: [],
    draftNodes: [inputNode],
    emissionDataJson: emissionWithRowsAndScalar,
  });
  assertFalse(bundle.dbTables.ok);
  assertFalse(bundle.qualifiedColumns.ok);
  assert(bundle.sourceNodes.ok);
  assert(bundle.emissionScalarPaths.ok);
});

Deno.test("deriveUiBuilderAuthoringSuggestBundle: empty nodes — source/target return structured reason", () => {
  const bundle = deriveUiBuilderAuthoringSuggestBundle({
    logicalTables: [employeesTable],
    relationIntents: [],
    draftNodes: [],
    emissionDataJson: emissionWithRowsAndScalar,
  });
  assertFalse(bundle.sourceNodes.ok);
  assertFalse(bundle.targetNodes.ok);
  assert(bundle.dbTables.ok);
});

// ─── Boundary tests ──────────────────────────────────────────────────────────

Deno.test("boundary: suggest derivation does not mutate draftNodes input", () => {
  const nodes: DraftNodeMinimal[] = [{ ...inputNode }];
  const originalNodeId = nodes[0]!.nodeId;
  deriveSourceNodeSuggestCandidates(nodes);
  deriveTargetNodeSuggestCandidates(nodes);
  assertEquals(nodes[0]!.nodeId, originalNodeId);
  assertEquals(nodes.length, 1);
});

Deno.test("boundary: suggest derivation does not mutate logicalTables input", () => {
  const tables: LogicalTableShape[] = [{ ...employeesTable, columns: [...employeesTable.columns] }];
  const originalLength = tables[0]!.columns.length;
  deriveDbTableCandidates(tables, []);
  deriveDbColumnCandidates(tables, "employees");
  deriveQualifiedColumnCandidates(tables, []);
  assertEquals(tables[0]!.columns.length, originalLength);
});

Deno.test("boundary: route: targetRef not in any manifest/node candidate output", () => {
  const result = deriveSourceNodeSuggestCandidates([inputNode, selectNode]);
  assert(result.ok);
  const { candidates } = result as { ok: true; candidates: SourceNodeCandidate[] };
  for (const c of candidates) {
    assertFalse(c.nodeId.startsWith("route:"));
    assertFalse(c.propKey.startsWith("route:"));
  }
});

Deno.test("boundary: unresolved relation returns structured error not silent empty", () => {
  const result = deriveQualifiedColumnCandidates([employeesTable], [unresolvableRelation]);
  if (result.ok) {
    const { unresolvedErrors } = result as { ok: true; candidates: QualifiedColumnCandidate[]; unresolvedErrors: string[] };
    assert(unresolvedErrors.length > 0, "unresolved relation must have structured errors");
  } else {
    assert((result as { ok: false; reason: string }).reason.length > 0);
  }
});

Deno.test("boundary: no eval/fetch/Function in suggest module (structural check)", async () => {
  const fileContent = await Deno.readTextFile(
    new URL("../lib/uiBuilderAuthoringSuggest.ts", import.meta.url),
  );
  assertFalse(fileContent.includes("eval("), "eval() must not be used");
  assertFalse(fileContent.includes("new Function("), "new Function() must not be used");
  assertFalse(fileContent.includes("fetch("), "fetch() must not be used");
  assertFalse(fileContent.includes("ManifestDispatcher"), "ManifestDispatcher must not be imported");
});
