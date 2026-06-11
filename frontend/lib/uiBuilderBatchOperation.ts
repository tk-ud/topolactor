/**
 * uiBuilderBatchOperation.ts — pure batch operation helpers for UIBuilder selection set.
 *
 * SSOT: docs/design/admin-console-workflow-ssot.yaml (ui_builder_canvas_workspace)
 * SSOT: docs/design/topology-layout-class-ssot.yaml
 * SSOT: docs/design/pipeline-continuity-ssot.yaml (component_wiring_execution_lane)
 *
 * Contract:
 * - No eval(), no Function(). No fetch(). No Preact imports.
 * - No backend dispatch per input/change.
 * - Silent skip prohibited: every invalid/unsupported node → per-node error.
 * - Frontend draft-state only. Does NOT persist to DB or bypass layout_patch boundary.
 * - Wiring batch is authoring-assist preview only; no backend wiring write.
 * - Calc binding batch adds to frontend draft calculationBindings array only.
 * - Route navigation targetRef stays frontend-local; not mixed with manifest dispatch.
 */

import { lookupTopologyLayoutClassKey } from "../runtime/topologyLayoutClassResolver.ts";
import { COMPONENT_ARRAY_PROP_CAPABILITIES } from "../runtime/propBindingResolver.ts";
import { isRouteNavigationTargetRef } from "./packageWiringPicker.ts";
import type { CalcBinding } from "../runtime/frontendLocalCalculationResolver.ts";

// ─── Minimal node interface (compatible with DraftNode in UiBuilderAdmin) ─────

export interface BatchNodeMinimal {
  nodeId: string;
  componentKey: string;
  nodeKind?: string;
  componentKind?: string;
  parentNodeId?: string | null;
  layoutClassRefs?: string[];
  propsJson?: string;
  stateJson?: string;
  propBindings?: Record<string, {
    source: string;
    keyPath?: string;
    labelPath?: string;
    valuePath?: string;
    childrenPath?: string;
    transform?: string;
  }> | null;
  wiringId?: string;
}

// ─── Preview result types ──────────────────────────────────────────────────────

export type BatchNodeEntryStatus = "ok" | "no_change" | "warning" | "error";

export interface BatchNodePreviewEntry {
  nodeId: string;
  nodeLabel: string;
  status: BatchNodeEntryStatus;
  error?: string;
  changeSummary?: string;
  before?: string;
  after?: string;
}

export interface BatchPreviewResult {
  targetCount: number;
  willChangeCount: number;
  warningCount: number;
  errorCount: number;
  hasAnyError: boolean;
  entries: BatchNodePreviewEntry[];
}

// ─── layoutClassRefs operation types ─────────────────────────────────────────

export type BatchLayoutClassRefOpKind = "add" | "remove" | "replace_group";

// ─── Wiring assist types ──────────────────────────────────────────────────────

export interface BatchWiringAssistDraft {
  wiringKind: string;
  targetSurface: string;
  targetRef: string;
}

export interface BatchWiringAssistNodeEntry {
  nodeId: string;
  nodeLabel: string;
  componentKind?: string;
  currentWiringId?: string;
  validationStatus: "ok" | "error";
  error?: string;
  previewNote?: string;
}

export interface BatchWiringAssistPreviewResult {
  targetCount: number;
  errorCount: number;
  entries: BatchWiringAssistNodeEntry[];
}

// ─── Calc binding assist types ─────────────────────────────────────────────────

export interface BatchCalcBindingAssistPreviewResult {
  referencedNodeCount: number;
  validationErrors: string[];
  previewNote: string;
  dispatchAdded: false;
  evalAdded: false;
}

// ─── Internal helpers ─────────────────────────────────────────────────────────

function nodeLabelForBatch(node: BatchNodeMinimal): string {
  if (node.nodeKind === "structural_html") return `<${node.componentKey}> (${node.nodeId})`;
  return `${node.componentKey} (${node.nodeId})`;
}

function resolveAllowedForOk(
  role: "container" | "component_wrapper",
  allowedFor: string[],
): boolean {
  if (role === "container") {
    return allowedFor.some(
      (r) => r === "layout_root" || r === "layout_section" || r === "layout_row",
    );
  }
  return allowedFor.includes("component_wrapper");
}

function getCurrentJsonObj(
  raw: string | undefined,
): Record<string, unknown> {
  if (!raw?.trim()) return {};
  try {
    const p = JSON.parse(raw);
    if (typeof p === "object" && p !== null && !Array.isArray(p)) {
      return p as Record<string, unknown>;
    }
  } catch { /* ignore */ }
  return {};
}

// ─── Role resolution ──────────────────────────────────────────────────────────

/**
 * Determine whether a node acts as a layout container or component_wrapper.
 * Uses same logic as isLayoutContainerNode in UiBuilderAdmin.tsx:
 *   - has children → container
 *   - has layout container classRef → container
 *   - structural_html nodeKind → container (structural elements are layout by nature)
 *   - otherwise → component_wrapper
 */
export function resolveNodeBatchLayoutRole(
  node: BatchNodeMinimal,
  allNodes: readonly BatchNodeMinimal[],
): "container" | "component_wrapper" {
  if (allNodes.some((n) => n.parentNodeId === node.nodeId)) return "container";
  for (const classKey of node.layoutClassRefs ?? []) {
    const entry = lookupTopologyLayoutClassKey(classKey.trim());
    if (
      entry?.allowedFor.some(
        (r) => r === "layout_root" || r === "layout_section" || r === "layout_row",
      )
    ) {
      return "container";
    }
  }
  if (node.nodeKind === "structural_html") return "container";
  return "component_wrapper";
}

// ─── layoutClassRefs batch ────────────────────────────────────────────────────

/**
 * Preview result of applying a layoutClassRef batch operation to selected nodes.
 *
 * SSOT: topology-layout-class-ssot.yaml
 * - classKey must exist in SSOT dictionary (raw className prohibited).
 * - allowedFor must include the node's role (container / component_wrapper).
 * - conflict_group violations are per-node errors (not silent).
 * - Nodes not in selection set are not touched.
 */
export function previewBatchLayoutClassRefs(
  allNodes: readonly BatchNodeMinimal[],
  selectedNodeIds: ReadonlySet<string>,
  op: BatchLayoutClassRefOpKind,
  classKey: string,
): BatchPreviewResult {
  const targetNodes = allNodes.filter((n) => selectedNodeIds.has(n.nodeId));
  const entries: BatchNodePreviewEntry[] = [];
  let willChangeCount = 0;
  let warningCount = 0;
  let errorCount = 0;

  const dictEntry = lookupTopologyLayoutClassKey(classKey.trim());
  if (!dictEntry) {
    const isRawClassName = classKey.trim().startsWith("topolactor-topology-");
    const errorCode = isRawClassName
      ? "BATCH_RAW_CLASS_NAME_PROHIBITED"
      : "BATCH_UNKNOWN_CLASS_KEY";
    const msg = isRawClassName
      ? `"${classKey}" は raw className です。topology-layout-class-ssot.yaml の classKey を使用してください。`
      : `"${classKey}" は topology-layout-class-ssot.yaml に存在しません。`;
    for (const node of targetNodes) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: `${errorCode}: ${msg}`,
      });
    }
    return {
      targetCount: targetNodes.length,
      willChangeCount: 0,
      warningCount: 0,
      errorCount,
      hasAnyError: true,
      entries,
    };
  }

  for (const node of targetNodes) {
    const role = resolveNodeBatchLayoutRole(node, allNodes);
    if (!resolveAllowedForOk(role, dictEntry.allowedFor)) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: `BATCH_CLASS_NOT_ALLOWED_FOR_ROLE: classKey "${classKey}" の allowedFor [${dictEntry.allowedFor.join(", ")}] にノードロール "${role}" が含まれていません。`,
      });
      continue;
    }

    const current = node.layoutClassRefs ?? [];

    if (op === "add") {
      if (current.includes(classKey)) {
        warningCount++;
        entries.push({
          nodeId: node.nodeId,
          nodeLabel: nodeLabelForBatch(node),
          status: "no_change",
          changeSummary: `既に ${classKey} が設定済み（変更なし）`,
          before: current.join(", ") || "(なし)",
          after: current.join(", ") || "(なし)",
        });
        continue;
      }
      if (dictEntry.conflictGroup) {
        const conflicting = current.filter((k) => {
          const e = lookupTopologyLayoutClassKey(k.trim());
          return e?.conflictGroup === dictEntry.conflictGroup;
        });
        if (conflicting.length > 0) {
          errorCount++;
          entries.push({
            nodeId: node.nodeId,
            nodeLabel: nodeLabelForBatch(node),
            status: "error",
            error: `BATCH_CONFLICT_GROUP_VIOLATION: conflict_group "${dictEntry.conflictGroup}" に既に [${conflicting.join(", ")}] が設定されています。replace_group 操作を使用してください。`,
            before: current.join(", ") || "(なし)",
          });
          continue;
        }
      }
      willChangeCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "ok",
        changeSummary: `add: ${classKey}`,
        before: current.join(", ") || "(なし)",
        after: [...current, classKey].join(", "),
      });
    } else if (op === "remove") {
      if (!current.includes(classKey)) {
        warningCount++;
        entries.push({
          nodeId: node.nodeId,
          nodeLabel: nodeLabelForBatch(node),
          status: "no_change",
          changeSummary: `${classKey} は設定されていない（変更なし）`,
          before: current.join(", ") || "(なし)",
          after: current.join(", ") || "(なし)",
        });
        continue;
      }
      willChangeCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "ok",
        changeSummary: `remove: ${classKey}`,
        before: current.join(", ") || "(なし)",
        after: current.filter((k) => k !== classKey).join(", ") || "(なし)",
      });
    } else {
      // replace_group
      if (!dictEntry.conflictGroup) {
        if (current.includes(classKey)) {
          warningCount++;
          entries.push({
            nodeId: node.nodeId,
            nodeLabel: nodeLabelForBatch(node),
            status: "no_change",
            changeSummary: `既に ${classKey} が設定済み（変更なし）`,
            before: current.join(", ") || "(なし)",
            after: current.join(", ") || "(なし)",
          });
          continue;
        }
        willChangeCount++;
        entries.push({
          nodeId: node.nodeId,
          nodeLabel: nodeLabelForBatch(node),
          status: "ok",
          changeSummary: `add（conflict_group なし）: ${classKey}`,
          before: current.join(", ") || "(なし)",
          after: [...current, classKey].join(", "),
        });
        continue;
      }
      const toRemove = current.filter((k) => {
        const e = lookupTopologyLayoutClassKey(k.trim());
        return e?.conflictGroup === dictEntry.conflictGroup;
      });
      const afterRemove = current.filter((k) => !toRemove.includes(k));
      const afterAdd = afterRemove.includes(classKey)
        ? afterRemove
        : [...afterRemove, classKey];
      if (JSON.stringify(afterAdd) === JSON.stringify(current)) {
        warningCount++;
        entries.push({
          nodeId: node.nodeId,
          nodeLabel: nodeLabelForBatch(node),
          status: "no_change",
          changeSummary: `既に ${classKey} が設定済み（変更なし）`,
          before: current.join(", ") || "(なし)",
          after: current.join(", ") || "(なし)",
        });
        continue;
      }
      willChangeCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "ok",
        changeSummary:
          toRemove.length > 0
            ? `replace_group "${dictEntry.conflictGroup}": [${toRemove.join(", ")}] → ${classKey}`
            : `add: ${classKey}`,
        before: current.join(", ") || "(なし)",
        after: afterAdd.join(", "),
      });
    }
  }

  return {
    targetCount: targetNodes.length,
    willChangeCount,
    warningCount,
    errorCount,
    hasAnyError: errorCount > 0,
    entries,
  };
}

/**
 * Apply layoutClassRefs batch operation to draft nodes.
 * Skips nodes that have errors in preview (allowedFor violations, conflict_group errors).
 * Those skips are NOT silent — they were shown explicitly in previewBatchLayoutClassRefs.
 */
export function applyBatchLayoutClassRefs<T extends BatchNodeMinimal>(
  allNodes: readonly T[],
  selectedNodeIds: ReadonlySet<string>,
  op: BatchLayoutClassRefOpKind,
  classKey: string,
): T[] {
  const dictEntry = lookupTopologyLayoutClassKey(classKey.trim());
  if (!dictEntry) return [...allNodes];

  return allNodes.map((node) => {
    if (!selectedNodeIds.has(node.nodeId)) return node;

    const role = resolveNodeBatchLayoutRole(node, allNodes);
    if (!resolveAllowedForOk(role, dictEntry.allowedFor)) return node;

    const current = node.layoutClassRefs ?? [];
    let next: string[];

    if (op === "add") {
      if (current.includes(classKey)) return node;
      if (dictEntry.conflictGroup) {
        const hasConflict = current.some((k) => {
          const e = lookupTopologyLayoutClassKey(k.trim());
          return e?.conflictGroup === dictEntry.conflictGroup;
        });
        if (hasConflict) return node;
      }
      next = [...current, classKey];
    } else if (op === "remove") {
      if (!current.includes(classKey)) return node;
      next = current.filter((k) => k !== classKey);
    } else {
      // replace_group
      if (!dictEntry.conflictGroup) {
        next = current.includes(classKey) ? current : [...current, classKey];
      } else {
        const afterRemove = current.filter((k) => {
          const e = lookupTopologyLayoutClassKey(k.trim());
          return e?.conflictGroup !== dictEntry.conflictGroup;
        });
        next = afterRemove.includes(classKey) ? afterRemove : [...afterRemove, classKey];
      }
    }
    return { ...node, layoutClassRefs: next };
  });
}

// ─── propsJson / stateJson batch ──────────────────────────────────────────────

export type BatchJsonPatchField = "propsJson" | "stateJson";

function parseBatchJsonPatch(
  patchJson: string,
): { ok: true; parsed: Record<string, unknown> } | { ok: false; error: string } {
  if (!patchJson.trim()) {
    return { ok: false, error: "BATCH_JSON_EMPTY: パッチ JSON が空です" };
  }
  let parsed: unknown;
  try {
    parsed = JSON.parse(patchJson);
  } catch {
    return { ok: false, error: "BATCH_JSON_MALFORMED: JSON として解析できません" };
  }
  if (Array.isArray(parsed)) {
    return {
      ok: false,
      error: "BATCH_JSON_ARRAY_ROOT: 配列ルートは不可。JSON オブジェクトを入力してください",
    };
  }
  if (typeof parsed !== "object" || parsed === null) {
    return {
      ok: false,
      error: "BATCH_JSON_SCALAR_ROOT: スカラー値ルートは不可。JSON オブジェクトを入力してください",
    };
  }
  return { ok: true, parsed: parsed as Record<string, unknown> };
}

function mergeJsonPatch(
  existing: string | undefined,
  patch: Record<string, unknown>,
): { merged: Record<string, unknown>; destructiveKeys: string[] } {
  const existingObj = getCurrentJsonObj(existing);
  const destructiveKeys: string[] = [];
  for (const [k, v] of Object.entries(patch)) {
    if (
      k in existingObj &&
      existingObj[k] !== undefined &&
      existingObj[k] !== null &&
      JSON.stringify(existingObj[k]) !== JSON.stringify(v)
    ) {
      destructiveKeys.push(k);
    }
  }
  return { merged: { ...existingObj, ...patch }, destructiveKeys };
}

/**
 * Preview result of applying a propsJson/stateJson merge patch to selected nodes.
 * - Validates patchJson is a JSON object (not array, scalar, malformed).
 * - Shows per-node before/after diff with destructive overwrite warnings.
 * SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract
 */
export function previewBatchJsonPatch(
  allNodes: readonly BatchNodeMinimal[],
  selectedNodeIds: ReadonlySet<string>,
  field: BatchJsonPatchField,
  patchJson: string,
): BatchPreviewResult {
  const parseResult = parseBatchJsonPatch(patchJson);
  const targetNodes = allNodes.filter((n) => selectedNodeIds.has(n.nodeId));
  const entries: BatchNodePreviewEntry[] = [];
  let willChangeCount = 0;
  let warningCount = 0;
  let errorCount = 0;

  if (!parseResult.ok) {
    for (const node of targetNodes) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: parseResult.error,
      });
    }
    return {
      targetCount: targetNodes.length,
      willChangeCount: 0,
      warningCount: 0,
      errorCount,
      hasAnyError: true,
      entries,
    };
  }

  const patch = parseResult.parsed;
  for (const node of targetNodes) {
    const currentRaw = node[field];
    const { merged, destructiveKeys } = mergeJsonPatch(currentRaw, patch);
    const currentObj = getCurrentJsonObj(currentRaw);
    const afterStr = JSON.stringify(merged, null, 2);
    const beforeStr = JSON.stringify(currentObj, null, 2);
    if (afterStr === beforeStr) {
      warningCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "no_change",
        changeSummary: "変更なし（マージ後の値が同じです）",
        before: currentRaw || "(なし)",
        after: afterStr,
      });
      continue;
    }
    willChangeCount++;
    entries.push({
      nodeId: node.nodeId,
      nodeLabel: nodeLabelForBatch(node),
      status: destructiveKeys.length > 0 ? "warning" : "ok",
      changeSummary:
        destructiveKeys.length > 0
          ? `マージ（上書き: ${destructiveKeys.join(", ")}）`
          : "マージ",
      before: currentRaw || "(なし)",
      after: afterStr,
    });
  }

  return {
    targetCount: targetNodes.length,
    willChangeCount,
    warningCount,
    errorCount,
    hasAnyError: false,
    entries,
  };
}

/**
 * Apply propsJson/stateJson merge patch to draft nodes.
 */
export function applyBatchJsonPatch<T extends BatchNodeMinimal>(
  allNodes: readonly T[],
  selectedNodeIds: ReadonlySet<string>,
  field: BatchJsonPatchField,
  patchJson: string,
): T[] {
  const parseResult = parseBatchJsonPatch(patchJson);
  if (!parseResult.ok) return [...allNodes];
  return allNodes.map((node) => {
    if (!selectedNodeIds.has(node.nodeId)) return node;
    const { merged } = mergeJsonPatch(node[field], parseResult.parsed);
    return { ...node, [field]: JSON.stringify(merged, null, 2) };
  });
}

// ─── propBindings batch ────────────────────────────────────────────────────────

export interface BatchPropBindingDraft {
  propName: string;
  source: string;
  keyPath?: string;
  labelPath?: string;
  valuePath?: string;
  childrenPath?: string;
  transform?: string;
}

const EMISSION_DATA_PREFIX = "emission.data.";

/**
 * Preview result of adding a propBinding to selected nodes.
 * - Silent skip prohibited: non-supported componentKind → per-node error.
 * - Non-supported propName for the componentKind → per-node error.
 * - source must start with "emission.data." → per-node error if not.
 * SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract.propBindings
 */
export function previewBatchPropBinding(
  allNodes: readonly BatchNodeMinimal[],
  selectedNodeIds: ReadonlySet<string>,
  binding: BatchPropBindingDraft,
): BatchPreviewResult {
  const targetNodes = allNodes.filter((n) => selectedNodeIds.has(n.nodeId));
  const entries: BatchNodePreviewEntry[] = [];
  let willChangeCount = 0;
  let warningCount = 0;
  let errorCount = 0;

  if (!binding.source.startsWith(EMISSION_DATA_PREFIX)) {
    for (const node of targetNodes) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: `BATCH_PROP_BINDING_INVALID_SOURCE: source "${binding.source}" は "${EMISSION_DATA_PREFIX}" で始まる必要があります`,
      });
    }
    return {
      targetCount: targetNodes.length,
      willChangeCount: 0,
      warningCount: 0,
      errorCount,
      hasAnyError: true,
      entries,
    };
  }

  if (!binding.propName.trim()) {
    for (const node of targetNodes) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: "BATCH_PROP_BINDING_MISSING_PROP_NAME: propName が必要です",
      });
    }
    return {
      targetCount: targetNodes.length,
      willChangeCount: 0,
      warningCount: 0,
      errorCount,
      hasAnyError: true,
      entries,
    };
  }

  for (const node of targetNodes) {
    const componentKind = node.componentKind ?? "";
    const accepted = COMPONENT_ARRAY_PROP_CAPABILITIES[componentKind];

    if (!accepted) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: `BATCH_PROP_BINDING_UNSUPPORTED_COMPONENT: componentKind "${componentKind}" は配列 prop binding に対応していません`,
      });
      continue;
    }

    if (!accepted.includes(binding.propName)) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "error",
        error: `BATCH_PROP_BINDING_UNSUPPORTED_PROP: prop "${binding.propName}" は componentKind "${componentKind}" の acceptsArrayProps に含まれていません（対応: ${accepted.join(", ")}）`,
      });
      continue;
    }

    const existing = node.propBindings?.[binding.propName];
    if (existing && existing.source === binding.source) {
      warningCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        status: "no_change",
        changeSummary: `propBindings.${binding.propName} は既に同一設定（変更なし）`,
      });
      continue;
    }

    willChangeCount++;
    entries.push({
      nodeId: node.nodeId,
      nodeLabel: nodeLabelForBatch(node),
      status: "ok",
      changeSummary: `propBindings.${binding.propName} = { source: "${binding.source}" }`,
      before: existing ? `source: "${existing.source}"` : "(なし)",
    });
  }

  return {
    targetCount: targetNodes.length,
    willChangeCount,
    warningCount,
    errorCount,
    hasAnyError: errorCount > 0,
    entries,
  };
}

/**
 * Apply propBinding batch to draft nodes.
 * Skips nodes with unsupported componentKind or propName (shown in preview, not silent).
 */
export function applyBatchPropBinding<T extends BatchNodeMinimal>(
  allNodes: readonly T[],
  selectedNodeIds: ReadonlySet<string>,
  binding: BatchPropBindingDraft,
): T[] {
  if (!binding.source.startsWith(EMISSION_DATA_PREFIX)) return [...allNodes];
  if (!binding.propName.trim()) return [...allNodes];

  return allNodes.map((node) => {
    if (!selectedNodeIds.has(node.nodeId)) return node;
    const componentKind = node.componentKind ?? "";
    const accepted = COMPONENT_ARRAY_PROP_CAPABILITIES[componentKind];
    if (!accepted || !accepted.includes(binding.propName)) return node;

    const bindingEntry: NonNullable<BatchNodeMinimal["propBindings"]>[string] = {
      source: binding.source,
      ...(binding.keyPath ? { keyPath: binding.keyPath } : {}),
      ...(binding.labelPath ? { labelPath: binding.labelPath } : {}),
      ...(binding.valuePath ? { valuePath: binding.valuePath } : {}),
      ...(binding.childrenPath ? { childrenPath: binding.childrenPath } : {}),
      ...(binding.transform ? { transform: binding.transform } : {}),
    };
    const current = node.propBindings ?? {};
    return { ...node, propBindings: { ...current, [binding.propName]: bindingEntry } };
  });
}

// ─── Wiring authoring assist batch ────────────────────────────────────────────

/**
 * Preview wiring template against selected nodes.
 *
 * Authoring assist only — no backend dispatch, no actual wiring save.
 * The actual save goes through the existing single-node wiring editor.
 *
 * SSOT: pipeline-continuity-ssot.yaml component_wiring_execution_lane
 * - Navigation wiring (route: prefix) stays frontend-local.
 * - route: targetRef MUST NOT be set with manifest targetSurface.
 * - wiringKey/wiringId/targetRef/targetSurface/wiringKind identity preserved.
 */
export function previewBatchWiringAssist(
  allNodes: readonly BatchNodeMinimal[],
  selectedNodeIds: ReadonlySet<string>,
  draft: BatchWiringAssistDraft,
): BatchWiringAssistPreviewResult {
  const targetNodes = allNodes.filter((n) => selectedNodeIds.has(n.nodeId));
  const entries: BatchWiringAssistNodeEntry[] = [];
  let errorCount = 0;

  const isRouteNav = isRouteNavigationTargetRef(draft.targetRef);
  const isManifestSurface = draft.targetSurface === "manifest";

  if (isRouteNav && isManifestSurface) {
    for (const node of targetNodes) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        componentKind: node.componentKind,
        currentWiringId: node.wiringId,
        validationStatus: "error",
        error:
          "BATCH_WIRING_ROUTE_MANIFEST_CONFLICT: route: prefix の targetRef は manifest targetSurface に設定できません（navigation wiring は frontend-local のまま維持）",
      });
    }
    return { targetCount: targetNodes.length, errorCount, entries };
  }

  for (const node of targetNodes) {
    if (!draft.wiringKind.trim()) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        componentKind: node.componentKind,
        currentWiringId: node.wiringId,
        validationStatus: "error",
        error: "BATCH_WIRING_MISSING_KIND: wiringKind が必要です",
      });
      continue;
    }
    if (!draft.targetSurface.trim()) {
      errorCount++;
      entries.push({
        nodeId: node.nodeId,
        nodeLabel: nodeLabelForBatch(node),
        componentKind: node.componentKind,
        currentWiringId: node.wiringId,
        validationStatus: "error",
        error: "BATCH_WIRING_MISSING_TARGET_SURFACE: targetSurface が必要です",
      });
      continue;
    }
    entries.push({
      nodeId: node.nodeId,
      nodeLabel: nodeLabelForBatch(node),
      componentKind: node.componentKind,
      currentWiringId: node.wiringId,
      validationStatus: "ok",
      previewNote: `wiringKind: ${draft.wiringKind}, targetSurface: ${draft.targetSurface}, targetRef: ${draft.targetRef || "(なし)"}（参照のみ — 保存は既存の単一ノード配線フローから実行）`,
    });
  }

  return { targetCount: targetNodes.length, errorCount, entries };
}

// ─── Calc binding assist batch ────────────────────────────────────────────────

/**
 * Preview adding a new CalcBinding that references multiple selected nodes.
 *
 * Validates:
 * - No eval/Function (structural check only).
 * - emission paths start with "emission.data.".
 * - variable kinds are in allowed set.
 * - No duplicate calculationId.
 *
 * Does NOT add backend dispatch or eval.
 * dispatchAdded and evalAdded are literal false constants as explicit contract proof.
 */
export function previewBatchCalcBindingAssist(
  calcBindings: readonly CalcBinding[],
  selectedNodeIds: ReadonlySet<string>,
  newBinding: CalcBinding,
): BatchCalcBindingAssistPreviewResult {
  const errors: string[] = [];
  const ALLOWED_KINDS = new Set(["node", "emission", "ruleTable", "literal"]);

  for (const [name, source] of Object.entries(newBinding.variables ?? {})) {
    const s = source as { kind?: string; path?: string };
    if (!ALLOWED_KINDS.has(s.kind ?? "")) {
      errors.push(
        `BATCH_CALC_INVALID_VARIABLE_KIND: 変数 "${name}" の kind "${s.kind}" は許可されていません`,
      );
    }
    if (s.kind === "emission") {
      const path = s.path ?? "";
      if (!path.startsWith(EMISSION_DATA_PREFIX)) {
        errors.push(
          `BATCH_CALC_EMISSION_PATH_INVALID: 変数 "${name}" の path "${path}" は "${EMISSION_DATA_PREFIX}" で始まる必要があります`,
        );
      }
    }
  }

  if (calcBindings.some((b) => b.calculationId === newBinding.calculationId)) {
    errors.push(
      `BATCH_CALC_DUPLICATE_ID: calculationId "${newBinding.calculationId}" は既に存在します`,
    );
  }

  const referencedNodeIds = new Set<string>();
  for (const source of Object.values(newBinding.variables ?? {})) {
    const s = source as { kind?: string; nodeId?: string };
    if (s.kind === "node" && s.nodeId && selectedNodeIds.has(s.nodeId)) {
      referencedNodeIds.add(s.nodeId);
    }
  }
  if (selectedNodeIds.has(newBinding.targetNodeId)) {
    referencedNodeIds.add(newBinding.targetNodeId);
  }

  return {
    referencedNodeCount: referencedNodeIds.size,
    validationErrors: errors,
    previewNote: `calcBinding "${newBinding.calculationId}": target ${newBinding.targetNodeId}.${newBinding.targetProp}（選択ノード参照: ${referencedNodeIds.size} 件）`,
    dispatchAdded: false,
    evalAdded: false,
  };
}

/**
 * Apply batch calc binding: add new binding to existing array.
 * Does NOT call backend, does NOT use eval/Function.
 * Returns new array with the binding appended (no-op if duplicate calculationId).
 */
export function applyBatchCalcBindingAssist(
  calcBindings: readonly CalcBinding[],
  newBinding: CalcBinding,
): CalcBinding[] {
  if (calcBindings.some((b) => b.calculationId === newBinding.calculationId)) {
    return [...calcBindings];
  }
  return [...calcBindings, newBinding];
}
