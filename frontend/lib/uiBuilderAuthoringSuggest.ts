/**
 * UIBuilder authoring suggest assist — pure helper bundle.
 *
 * SSOT: docs/design/admin-console-workflow-ssot.yaml
 *   - ui_builder_canvas_workspace (canvas workspace contract)
 *   - frontend_local_derived_calculation_binding (calc source, ruleTable, emission, node sources)
 *   - layout_node_props_contract (propBindings.source, emission.data.* paths)
 * SSOT: docs/design/pipeline-continuity-ssot.yaml
 *   - component_wiring_execution_lane (targetRef, routeKey, wiringKind)
 *
 * Boundary:
 * - No Preact imports.
 * - No fetch, no dynamic code execution, no dispatch layer imports.
 * - No route: targetRef mixed into manifest/runtimeDispatch candidates.
 * - No DB creation authority — candidates reference existing field sources only.
 * - No silent fallback: unavailable candidates return structured reason.
 * - Mutation authority: none — all helpers are pure derivation only.
 *
 * Reuse: manifestLogicalTables.ts SSOT functions are used via structural cast.
 * LogicalTableShape ≅ LogicalTableDraft (same fields), RelationIntentShape ≅ RelationIntentDraft.
 */

import type {
  LogicalTableShape,
  RelationIntentShape,
  OperationEntityBindingShape,
} from "./manifestTopologyExtensions.ts";
import type { LogicalTableDraft, RelationIntentDraft } from "./manifestScreenDesign.ts";
import {
  qualifiedColumnKey,
  step3FieldSourceFromDesign,
  qualifiedColumnsFromLogicalTables,
  namedLogicalTableRefs,
  columnNamesForTableRef,
} from "./manifestLogicalTables.ts";
import {
  resolveAllowedTargetProps,
} from "../runtime/frontendLocalCalculationResolver.ts";
import {
  deriveEmissionPathCandidates,
  deriveRuleTableFieldCandidates,
  deriveNodeCandidates,
  type DraftNodeMinimal,
  type EmissionPathCandidate,
} from "./uiBuilderAutocompleteCandidates.ts";
import { COMPONENT_ARRAY_PROP_CAPABILITIES } from "../runtime/propBindingResolver.ts";

// ─── Type casts (LogicalTableShape ≅ LogicalTableDraft structurally) ──────────

function asLogicalTableDrafts(tables: LogicalTableShape[]): LogicalTableDraft[] {
  return tables as unknown as LogicalTableDraft[];
}

function asRelationIntentDrafts(rels: RelationIntentShape[]): RelationIntentDraft[] {
  return rels as unknown as RelationIntentDraft[];
}

// ─── DB table candidate types ─────────────────────────────────────────────────

export type DbTableCandidate = {
  tableRef: string;
  /** "local" = defined in Step 2 logicalTables; "relation_intent" = joinTableRef from Step 2.5 */
  source: "local" | "relation_intent";
  columnCount: number;
};

export type DbTableCandidateResult =
  | { ok: true; candidates: DbTableCandidate[] }
  | { ok: false; reason: string; unresolvedErrors: string[] };

/**
 * Derive DB table candidates from logicalTables + relationIntents.
 * Local tables take precedence; relation_intent joinTableRefs are appended when not already covered.
 */
export function deriveDbTableCandidates(
  logicalTables: LogicalTableShape[],
  relationIntents: RelationIntentShape[],
): DbTableCandidateResult {
  if (logicalTables.length === 0 && relationIntents.length === 0) {
    return {
      ok: false,
      reason: "manifest screen design が読み込まれていません。ルートに紐づくマニフェストを選択してください。",
      unresolvedErrors: [],
    };
  }

  const candidates: DbTableCandidate[] = [];
  const seen = new Set<string>();

  for (const t of logicalTables) {
    const ref = t.tableName.trim();
    if (!ref || seen.has(ref)) continue;
    seen.add(ref);
    candidates.push({ tableRef: ref, source: "local", columnCount: t.columns.length });
  }

  for (const rel of relationIntents) {
    const ref = rel.joinTableRef.trim();
    if (!ref || seen.has(ref)) continue;
    seen.add(ref);
    candidates.push({ tableRef: ref, source: "relation_intent", columnCount: 0 });
  }

  if (candidates.length === 0) {
    return {
      ok: false,
      reason: "有効なテーブル候補がありません。Step 2 でテーブルを定義してください。",
      unresolvedErrors: [],
    };
  }

  return { ok: true, candidates };
}

// ─── DB column candidate types ────────────────────────────────────────────────

export type DbColumnCandidate = {
  columnName: string;
  qualifiedKey: string;
  dataType: string;
  nullable: boolean;
};

export type DbColumnCandidateResult =
  | { ok: true; columns: DbColumnCandidate[]; tableRef: string }
  | { ok: false; reason: string };

/**
 * Derive DB column candidates for a selected tableRef from logicalTables.
 * Returns structured reason when tableRef is not found or has no columns.
 */
export function deriveDbColumnCandidates(
  logicalTables: LogicalTableShape[],
  tableRef: string,
): DbColumnCandidateResult {
  const ref = tableRef.trim();
  if (!ref) {
    return { ok: false, reason: "tableRef が未選択です。テーブルを選択してください。" };
  }

  const table = logicalTables.find((t) => t.tableName.trim() === ref);
  if (!table) {
    return {
      ok: false,
      reason: `テーブル「${ref}」は logicalTables に存在しません。Step 2 でテーブル名を確認してください。`,
    };
  }

  const columns: DbColumnCandidate[] = table.columns
    .filter((c) => c.name.trim())
    .map((c) => ({
      columnName: c.name.trim(),
      qualifiedKey: qualifiedColumnKey(ref, c.name.trim()),
      dataType: c.dataType,
      nullable: c.nullable,
    }));

  if (columns.length === 0) {
    return {
      ok: false,
      reason: `テーブル「${ref}」にカラムが定義されていません。Step 2 でカラムを追加してください。`,
    };
  }

  return { ok: true, columns, tableRef: ref };
}

// ─── Qualified column candidate types ────────────────────────────────────────

export type QualifiedColumnCandidate = {
  key: string;
  tableRef: string;
  columnName: string;
  dataType: string;
};

export type QualifiedColumnCandidateResult =
  | { ok: true; candidates: QualifiedColumnCandidate[]; unresolvedErrors: string[] }
  | { ok: false; reason: string; unresolvedErrors: string[] };

/**
 * Derive qualified column candidates (tableRef.columnName) from logicalTables + relationIntents.
 * Uses step3FieldSourceFromDesign for relation resolution.
 *
 * Scope boundary — active remote manifest テーブル/フィールド:
 *   SSOT Step 3 includes active-remote targets via manifest:list_relationship_remote_targets,
 *   but that requires a backend dispatch call which is prohibited in UIBuilder authoring.
 *   remoteTargets is therefore always [] here. Active remote manifest column suggest is
 *   out of scope for this bundle and tracked as a separate future bundle.
 *
 * Returns unresolved relation errors as structured reasons — no silent fallback.
 */
export function deriveQualifiedColumnCandidates(
  logicalTables: LogicalTableShape[],
  relationIntents: RelationIntentShape[],
): QualifiedColumnCandidateResult {
  if (logicalTables.length === 0) {
    return {
      ok: false,
      reason: "logicalTables が空です。Step 2 でテーブル・カラムを定義してください。",
      unresolvedErrors: [],
    };
  }

  const step3Result = step3FieldSourceFromDesign(
    asLogicalTableDrafts(logicalTables),
    asRelationIntentDrafts(relationIntents),
    [],
  );

  const candidates: QualifiedColumnCandidate[] = step3Result.qualifiedColumns.map((q) => ({
    key: q.key,
    tableRef: q.tableRef,
    columnName: q.columnName,
    dataType: q.column.dataType,
  }));

  if (candidates.length === 0) {
    return {
      ok: false,
      reason: "qualified column 候補がありません。Step 2 でカラムを定義してください。",
      unresolvedErrors: step3Result.unresolvedErrors,
    };
  }

  return { ok: true, candidates, unresolvedErrors: step3Result.unresolvedErrors };
}

// ─── Data binding path candidate types ────────────────────────────────────────

export type DataBindingPathCandidate = {
  path: string;
  description: string;
};

export type DataBindingCandidateResult =
  | {
    ok: true;
    componentKind: string;
    acceptsArrayProps: string[];
    sourcePaths: DataBindingPathCandidate[];
    fieldPaths: DataBindingPathCandidate[];
  }
  | { ok: false; reason: string };

/**
 * Derive data binding path candidates for array-capable components.
 *
 * sourcePaths = array emission.data.* paths (for the `source` field in propBindings).
 * fieldPaths = column names from logicalTables for the selected tableRef
 *   (for keyPath / labelPath / valuePath / childrenPath).
 *
 * Returns structured reason for unsupported componentKind or missing data.
 */
export function deriveDataBindingPathCandidates(
  componentKind: string,
  emissionDataJson: string,
  logicalTables: LogicalTableShape[],
  selectedTableRef: string,
): DataBindingCandidateResult {
  const kind = componentKind.trim();
  if (!kind) {
    return { ok: false, reason: "componentKind が未設定です。" };
  }

  const acceptsArrayProps = COMPONENT_ARRAY_PROP_CAPABILITIES[kind] ?? [];
  if (acceptsArrayProps.length === 0) {
    return {
      ok: false,
      reason: `componentKind「${kind}」は配列 prop バインド非対応です。` +
        `CardList（display/card_list）、List（data_display/list）、Table（data_display/table）、` +
        `Select（form_input/select）などを使用してください。`,
    };
  }

  const emissionResult = deriveEmissionPathCandidates(emissionDataJson);
  const sourcePaths: DataBindingPathCandidate[] = emissionResult.ok
    ? emissionResult.candidates
      .filter((c) => c.isArray)
      .map((c) => ({ path: c.path, description: `emission.data 配列パス` }))
    : [];

  const columnNames = selectedTableRef.trim()
    ? columnNamesForTableRef(asLogicalTableDrafts(logicalTables), selectedTableRef.trim())
    : [];

  const fieldPaths: DataBindingPathCandidate[] = columnNames.map((name) => ({
    path: name,
    description: `${selectedTableRef}.${name}`,
  }));

  return {
    ok: true,
    componentKind: kind,
    acceptsArrayProps,
    sourcePaths,
    fieldPaths,
  };
}

// ─── Source node suggest candidate types ─────────────────────────────────────

export type SourceNodeCandidate = {
  nodeId: string;
  propKey: string;
  label: string;
};

export type SourceNodeCandidateResult =
  | { ok: true; candidates: SourceNodeCandidate[] }
  | { ok: false; reason: string };

const SOURCE_PROP_KEYS_BY_COMPONENT_KIND: Record<string, string[]> = {
  "form_input/input": ["value"],
  "form_input/textarea": ["value"],
  "form_input/search_input": ["value"],
  "form_input/select": ["value"],
  "form_input/checkbox": ["checked"],
  "form_input/radio_group": ["value"],
  "calc_topology/calculation_preview_panel": ["result"],
  "calc_topology/computed_field_preview": ["preview", "value"],
  "calc_topology/cross_entity_calculation_panel": ["preview", "value"],
  "calc_topology/formula_builder": ["value"],
  "layout/structural_html": ["inlineText"],
  "structural_html": ["inlineText"],
};

/**
 * Derive source node + propKey candidates from draftNodes.
 * Each node produces one candidate per known propKey for its componentKind.
 * Unknown componentKinds produce a single "value" candidate (not blocked).
 */
export function deriveSourceNodeSuggestCandidates(
  draftNodes: DraftNodeMinimal[],
): SourceNodeCandidateResult {
  if (draftNodes.length === 0) {
    return { ok: false, reason: "canvas にノードがありません。ノードを追加してください。" };
  }

  const nodeCandidates = deriveNodeCandidates(draftNodes);
  const candidates: SourceNodeCandidate[] = [];

  for (const nc of nodeCandidates) {
    const node = draftNodes.find((n) => n.nodeId === nc.nodeId);
    const kind = node?.componentKind ?? "";
    const propKeys = SOURCE_PROP_KEYS_BY_COMPONENT_KIND[kind] ?? ["value"];
    for (const propKey of propKeys) {
      candidates.push({ nodeId: nc.nodeId, propKey, label: `${nc.label} → ${propKey}` });
    }
  }

  if (candidates.length === 0) {
    return { ok: false, reason: "source node 候補がありません。" };
  }

  return { ok: true, candidates };
}

// ─── Target node suggest candidate types ─────────────────────────────────────

export type TargetNodeCandidate = {
  nodeId: string;
  componentKind: string | undefined;
  label: string;
  allowedTargetProps: string[] | null;
};

export type TargetNodeCandidateResult =
  | { ok: true; candidates: TargetNodeCandidate[] }
  | { ok: false; reason: string };

/**
 * Derive target node candidates from draftNodes.
 * Each node includes allowedTargetProps for the UI to restrict prop selection.
 * null allowedTargetProps = unknown componentKind (not constrained per validateCalcTargetProp).
 */
export function deriveTargetNodeSuggestCandidates(
  draftNodes: DraftNodeMinimal[],
): TargetNodeCandidateResult {
  if (draftNodes.length === 0) {
    return { ok: false, reason: "canvas にノードがありません。ノードを追加してください。" };
  }

  const nodeCandidates = deriveNodeCandidates(draftNodes);
  const candidates: TargetNodeCandidate[] = nodeCandidates.map((nc) => {
    const node = draftNodes.find((n) => n.nodeId === nc.nodeId);
    const kind = node?.componentKind ?? "";
    const allowedTargetProps = kind ? resolveAllowedTargetProps(kind) : null;
    return {
      nodeId: nc.nodeId,
      componentKind: node?.componentKind,
      label: nc.label,
      allowedTargetProps,
    };
  });

  return { ok: true, candidates };
}

// ─── Target prop suggest candidate types ─────────────────────────────────────

export type TargetPropCandidateResult =
  | { ok: true; targetProps: string[]; componentKind: string }
  | { ok: false; reason: string };

/**
 * Derive targetProp candidates for a given componentKind.
 * Always consistent with resolveAllowedTargetProps / validateCalcTargetProp.
 * Unknown componentKind is NOT blocked (per validateCalcTargetProp semantics):
 *   returns ok:false with reason rather than empty candidates.
 */
export function deriveTargetPropSuggestCandidates(
  componentKind: string,
): TargetPropCandidateResult {
  const kind = componentKind.trim();
  if (!kind) {
    return { ok: false, reason: "componentKind が未設定です。ターゲットノードを選択してください。" };
  }

  const allowed = resolveAllowedTargetProps(kind);
  if (allowed === null) {
    return {
      ok: false,
      reason: `componentKind「${kind}」は targetProp 制約マップに登録されていません。` +
        `任意の targetProp を使用できますが、実行時エラーになる可能性があります。`,
    };
  }

  if (allowed.length === 0) {
    return {
      ok: false,
      reason: `componentKind「${kind}」に有効な targetProp がありません。`,
    };
  }

  return { ok: true, targetProps: allowed, componentKind: kind };
}

// ─── ruleTable matchCondition suggest candidate types ─────────────────────────

export type MatchConditionFieldCandidate = {
  field: string;
  description: string;
};

export type MatchConditionValueSourceCandidate =
  | { kind: "node"; nodeId: string; propKey: string; label: string }
  | { kind: "literal"; description: string }
  | { kind: "db_column"; qualifiedKey: string; tableRef: string; columnName: string };

export type MatchConditionCandidateResult =
  | {
    ok: true;
    fieldCandidates: MatchConditionFieldCandidate[];
    valueSourceCandidates: MatchConditionValueSourceCandidate[];
  }
  | { ok: false; reason: string };

/**
 * Derive ruleTable matchCondition candidates.
 * fieldCandidates = table row fields from tablePath (requires emissionDataJson).
 * valueSourceCandidates = node sources + literal placeholder + DB column derived sources.
 *
 * Candidate display only — does not save or apply conditions automatically.
 */
export function deriveRuleTableMatchConditionSuggestCandidates(
  tablePath: string,
  emissionDataJson: string,
  draftNodes: DraftNodeMinimal[],
  logicalTables: LogicalTableShape[],
): MatchConditionCandidateResult {
  const path = tablePath.trim();
  if (!path) {
    return { ok: false, reason: "tablePath が未設定です。配列パスを指定してください。" };
  }

  const fieldsResult = deriveRuleTableFieldCandidates(path, emissionDataJson);
  if (!fieldsResult.ok) {
    return { ok: false, reason: fieldsResult.reason };
  }

  const fieldCandidates: MatchConditionFieldCandidate[] = fieldsResult.fields.map((f) => ({
    field: f,
    description: `${path}[].${f}`,
  }));

  const valueSourceCandidates: MatchConditionValueSourceCandidate[] = [];

  // Node sources
  const nodeResult = deriveSourceNodeSuggestCandidates(draftNodes);
  if (nodeResult.ok) {
    for (const nc of nodeResult.candidates) {
      valueSourceCandidates.push({
        kind: "node",
        nodeId: nc.nodeId,
        propKey: nc.propKey,
        label: nc.label,
      });
    }
  }

  // Literal placeholder (not auto-applied)
  valueSourceCandidates.push({
    kind: "literal",
    description: "固定値（literal）— 業務基準値の本番 literal 保存は避け、ruleTable/emission source を優先してください",
  });

  // DB column derived sources
  const qualifiedCols = qualifiedColumnsFromLogicalTables(asLogicalTableDrafts(logicalTables));
  for (const q of qualifiedCols) {
    valueSourceCandidates.push({
      kind: "db_column",
      qualifiedKey: q.key,
      tableRef: q.tableRef,
      columnName: q.columnName,
    });
  }

  return { ok: true, fieldCandidates, valueSourceCandidates };
}

// ─── Emission path suggest (scalar / array split) ─────────────────────────────

export type EmissionScalarPathCandidateResult =
  | { ok: true; candidates: EmissionPathCandidate[] }
  | { ok: false; reason: string };

/**
 * Derive emission.data.* scalar path candidates for calc source variables.
 * Scalar paths only (isArray === false). Array paths should use ruleTable source.
 */
export function deriveEmissionScalarPathCandidates(
  emissionDataJson: string,
): EmissionScalarPathCandidateResult {
  const result = deriveEmissionPathCandidates(emissionDataJson);
  if (!result.ok) return result;

  const scalars = result.candidates.filter((c) => !c.isArray);
  if (scalars.length === 0) {
    return {
      ok: false,
      reason: "scalar emission path 候補がありません。emission.data.* に scalar フィールドがありません。",
    };
  }

  return { ok: true, candidates: scalars };
}

/**
 * Derive emission.data.* array path candidates for ruleTable tablePath.
 * Array paths only (isArray === true).
 */
export function deriveEmissionArrayPathCandidates(
  emissionDataJson: string,
): EmissionScalarPathCandidateResult {
  const result = deriveEmissionPathCandidates(emissionDataJson);
  if (!result.ok) return result;

  const arrays = result.candidates.filter((c) => c.isArray);
  if (arrays.length === 0) {
    return {
      ok: false,
      reason: "ruleTable tablePath 候補がありません。emission.data.* に配列フィールドがありません。",
    };
  }

  return { ok: true, candidates: arrays };
}

// ─── Operation entity binding column candidates ───────────────────────────────

export type OperationEntityColumnCandidateResult =
  | { ok: true; candidates: { operationKind: string; columns: string[] }[] }
  | { ok: false; reason: string };

/**
 * Derive entityTargetColumns candidates per operation kind.
 * Filtered to columns that exist in qualifiedColumns from logicalTables.
 */
export function deriveOperationEntityColumnCandidates(
  operationEntityBindings: OperationEntityBindingShape[],
  logicalTables: LogicalTableShape[],
): OperationEntityColumnCandidateResult {
  if (operationEntityBindings.length === 0) {
    return { ok: false, reason: "operation_entity_bindings が設定されていません。Step 3 で操作エンティティを定義してください。" };
  }

  const qualifiedCols = qualifiedColumnsFromLogicalTables(asLogicalTableDrafts(logicalTables));
  const knownKeys = new Set(qualifiedCols.map((q) => q.key));

  const candidates = operationEntityBindings
    .filter((b) => b.entityTargetColumns.length > 0)
    .map((b) => ({
      operationKind: b.operationKind,
      columns: b.entityTargetColumns.filter((c) => knownKeys.has(c) || !c.includes(".")),
    }));

  if (candidates.length === 0) {
    return { ok: false, reason: "entityTargetColumns が設定されている operation binding がありません。" };
  }

  return { ok: true, candidates };
}

// ─── Full bundle derivation ────────────────────────────────────────────────────

export type UiBuilderAuthoringSuggestBundle = {
  dbTables: DbTableCandidateResult;
  qualifiedColumns: QualifiedColumnCandidateResult;
  sourceNodes: SourceNodeCandidateResult;
  targetNodes: TargetNodeCandidateResult;
  emissionScalarPaths: EmissionScalarPathCandidateResult;
  emissionArrayPaths: EmissionScalarPathCandidateResult;
};

/**
 * Derive all authoring suggest candidates at once.
 * Each sub-bundle is independently ok/fail — partial availability is normal.
 */
export function deriveUiBuilderAuthoringSuggestBundle(
  params: {
    logicalTables: LogicalTableShape[];
    relationIntents: RelationIntentShape[];
    draftNodes: DraftNodeMinimal[];
    emissionDataJson: string;
  },
): UiBuilderAuthoringSuggestBundle {
  return {
    dbTables: deriveDbTableCandidates(params.logicalTables, params.relationIntents),
    qualifiedColumns: deriveQualifiedColumnCandidates(params.logicalTables, params.relationIntents),
    sourceNodes: deriveSourceNodeSuggestCandidates(params.draftNodes),
    targetNodes: deriveTargetNodeSuggestCandidates(params.draftNodes),
    emissionScalarPaths: deriveEmissionScalarPathCandidates(params.emissionDataJson),
    emissionArrayPaths: deriveEmissionArrayPathCandidates(params.emissionDataJson),
  };
}
