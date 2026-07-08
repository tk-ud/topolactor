/**
 * uiBuilderProjectionInspection — pure helpers for the /admin/ui-builder
 * read-only projection inspection panel.
 *
 * Role (SSOT: .agent/tasks/todo.md bundle ui-projection-surface-architecture-reinforcement,
 * docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml canvas workspace boundary):
 * confirm — never mutate — the applied projection surface:
 *   - applied topology confirmation (manifestId / layoutId / packageId / routeKey)
 *   - read query confirmation (dispatched axes + emission result kind)
 *   - rows / activeColumns / displayColumnMode confirmation from emission.data branches
 *   - propBindings confirmation resolved from emission.data branches (not first-row samples)
 *   - canvas preview vs applied projection comparison
 *
 * This module is projection-surface observation only. It is NOT persistence
 * authority, NOT promotion authority, NOT a canonical projection entry, and
 * NOT a seed fallback.
 */

import type { Emission, LayoutNode } from "../api/dispatch.ts";
import type { UserOperation } from "../runtime/resolveOperationVector.ts";
import { resolveRuntimeDataPath } from "../runtime/propBindingResolver.ts";

export type AppliedProjectionConfirmation = {
  /** Read query confirmation: the exact dispatched axes. */
  readQuery: {
    target: string;
    layer: string;
    action: string;
    targetRef?: string;
  };
  /** emission.data.kind (e.g. "screen_data_shape_query_result") or null. */
  resultKind: string | null;
  manifestId: string | null;
  layoutId: string | null;
  packageId: string | null;
  /** Collection outer shape branches from emission.data. */
  rowCount: number | null;
  activeColumns: string[] | null;
  displayColumnMode: string | null;
  aggregationResultCount: number | null;
};

/**
 * Builds the applied-topology / read-query / collection-shape confirmation from
 * a production dispatch emission. Every field reads emission.data branches
 * directly — no first-row collapse, no sample substitution.
 */
export function buildAppliedProjectionConfirmation(
  axes: UserOperation,
  emission: Emission,
): AppliedProjectionConfirmation {
  const data = emission.data ?? {};
  const targetRef = typeof axes.payload?.target_ref === "string"
    ? axes.payload.target_ref
    : undefined;
  const rows = Array.isArray(data.rows) ? data.rows : null;
  const aggregationResults = Array.isArray(data.aggregationResults)
    ? data.aggregationResults
    : null;
  const activeColumns = Array.isArray(data.activeColumns)
    ? data.activeColumns.filter((c): c is string => typeof c === "string")
    : null;
  return {
    readQuery: {
      target: axes.target,
      layer: axes.layer,
      action: axes.action,
      targetRef,
    },
    resultKind: typeof data.kind === "string" ? data.kind : null,
    manifestId: typeof data.manifestId === "string" ? data.manifestId : null,
    layoutId: emission.layoutId ?? null,
    packageId: emission.packageId ?? null,
    rowCount: rows ? rows.length : null,
    activeColumns,
    displayColumnMode: typeof data.displayColumnMode === "string"
      ? data.displayColumnMode
      : null,
    aggregationResultCount: aggregationResults
      ? aggregationResults.length
      : null,
  };
}

export type PropBindingConfirmationEntry = {
  nodeId: string;
  propName: string;
  source: string;
  transform?: string;
  /**
   * "resolved": source path resolved against emission.data branches.
   * "unresolved": path did not resolve (data branch absent).
   * "invalid_source": source is not an emission.data path.
   */
  status: "resolved" | "unresolved" | "invalid_source";
  /** For resolved array values: element count. */
  resolvedCount?: number;
  /** typeof summary of the resolved value ("array" | "object" | "string" | ...). */
  resolvedKind?: string;
};

/**
 * Confirms each authored propBinding by resolving its source path against the
 * actual emission.data branches (rows / activeColumns / displayColumnMode /
 * aggregationResults / ...). This is branch resolution confirmation — not a
 * first-row sample and not a smoke label.
 */
export function buildPropBindingsConfirmation(
  layoutNodes: readonly LayoutNode[] | undefined,
  emissionData: Record<string, unknown> | undefined,
): PropBindingConfirmationEntry[] {
  const entries: PropBindingConfirmationEntry[] = [];
  const data = emissionData ?? {};
  for (const node of layoutNodes ?? []) {
    if (!node.propBindings) continue;
    const nodeId = node.nodeId ?? "(unnamed)";
    for (const [propName, binding] of Object.entries(node.propBindings)) {
      const source = binding.source;
      if (source !== "emission.data" && !source.startsWith("emission.data.")) {
        entries.push({
          nodeId,
          propName,
          source,
          transform: binding.transform,
          status: "invalid_source",
        });
        continue;
      }
      const resolved = resolveRuntimeDataPath(data, source);
      if (resolved === undefined) {
        entries.push({
          nodeId,
          propName,
          source,
          transform: binding.transform,
          status: "unresolved",
        });
        continue;
      }
      entries.push({
        nodeId,
        propName,
        source,
        transform: binding.transform,
        status: "resolved",
        resolvedCount: Array.isArray(resolved) ? resolved.length : undefined,
        resolvedKind: Array.isArray(resolved) ? "array" : typeof resolved,
      });
    }
  }
  return entries;
}

export type CanvasAppliedComparison = {
  previewNodeCount: number;
  appliedNodeCount: number;
  /** nodeIds present in the canvas preview but absent from the applied projection. */
  onlyInPreview: string[];
  /** nodeIds present in the applied projection but absent from the canvas preview. */
  onlyInApplied: string[];
  /** nodeIds present on both sides. */
  common: string[];
  matches: boolean;
};

function nodeIdsOf(nodes: readonly { nodeId?: string }[] | undefined): string[] {
  return (nodes ?? [])
    .map((n) => n.nodeId)
    .filter((id): id is string => typeof id === "string" && id.length > 0);
}

/**
 * Canvas preview vs applied projection comparison by node identity.
 * Both sides are compared as-is; neither side is treated as authority — a
 * mismatch is surfaced for the author to act on through the canonical
 * authoring flow (this panel never repairs or applies anything).
 */
export function compareCanvasPreviewWithApplied(
  previewNodes: readonly { nodeId?: string }[] | undefined,
  appliedNodes: readonly { nodeId?: string }[] | undefined,
): CanvasAppliedComparison {
  const previewIds = nodeIdsOf(previewNodes);
  const appliedIds = nodeIdsOf(appliedNodes);
  const previewSet = new Set(previewIds);
  const appliedSet = new Set(appliedIds);
  const onlyInPreview = previewIds.filter((id) => !appliedSet.has(id));
  const onlyInApplied = appliedIds.filter((id) => !previewSet.has(id));
  const common = previewIds.filter((id) => appliedSet.has(id));
  return {
    previewNodeCount: previewIds.length,
    appliedNodeCount: appliedIds.length,
    onlyInPreview,
    onlyInApplied,
    common,
    matches: onlyInPreview.length === 0 && onlyInApplied.length === 0,
  };
}
