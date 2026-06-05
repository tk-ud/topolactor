/**
 * layout_patch preview audit utilities — SSOT:
 * - docs/registrar-admin-ui-specification.md §5.6 (resolver → className)
 * - docs/design/ui-ux-primitive-catalog-ssot.yaml PatchPreviewPanel (before/after diff)
 */
import type { PatchField } from "../components/PatchPreviewPanel.tsx";
import { resolveCanvasRootPreviewClassName } from "./layoutClassPreviewUtils.ts";
import {
  lookupTopologyLayoutClassKey,
  resolveTopologyLayoutClassRefs,
} from "./topologyLayoutClassResolver.ts";

export type LayoutClassPreviewResolution = {
  classRef: string;
  className: string;
  ok: boolean;
  message?: string;
};

export type LayoutPatchPreviewAudit = {
  fields: PatchField[];
  classResolutions: LayoutClassPreviewResolution[];
  canvasRootClassName: string;
  backendMessage: string;
  beforeJson: string;
  afterJson: string;
  nodeCountBefore: number;
  nodeCountAfter: number;
};

function formatPatchValue(value: unknown): string {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "string") return value;
  return JSON.stringify(value);
}

function readPatchRoot(json: string): Record<string, unknown> | null {
  const trimmed = json.trim();
  if (!trimmed) return {};
  try {
    const parsed = JSON.parse(trimmed);
    if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    return null;
  }
  return null;
}

function readPatchNodes(root: Record<string, unknown> | null): Record<string, unknown>[] {
  if (!root || !Array.isArray(root.nodes)) return [];
  return root.nodes.filter(
    (node): node is Record<string, unknown> =>
      typeof node === "object" && node !== null && !Array.isArray(node),
  );
}

function readLayoutClassRefs(root: Record<string, unknown> | null): string[] {
  if (!root || !Array.isArray(root.layoutClassRefs)) return [];
  return root.layoutClassRefs.filter(
    (value): value is string => typeof value === "string" && value.trim().length > 0,
  );
}

export function buildLayoutClassPreviewResolutions(
  layoutClassRefs: readonly string[],
): LayoutClassPreviewResolution[] {
  return layoutClassRefs.map((classRef) => {
    const trimmed = classRef.trim();
    const entry = lookupTopologyLayoutClassKey(trimmed);
    if (!entry) {
      return {
        classRef: trimmed,
        className: "",
        ok: false,
        message: "UNKNOWN_CLASS_REF",
      };
    }
    const resolved = resolveTopologyLayoutClassRefs([trimmed]);
    return {
      classRef: trimmed,
      className: resolved.ok ? resolved.className : "",
      ok: resolved.ok,
      message: resolved.ok ? undefined : resolved.error,
    };
  });
}

/** Build visual audit payload for layout_patch preview modal (before/after + className resolution). */
export function buildLayoutPatchPreviewAudit(
  beforeJson: string,
  afterJson: string,
  backendMessage = "",
): LayoutPatchPreviewAudit {
  const beforeRoot = readPatchRoot(beforeJson);
  const afterRoot = readPatchRoot(afterJson);
  const fields: PatchField[] = [];

  const beforeRefs = readLayoutClassRefs(beforeRoot);
  const afterRefs = readLayoutClassRefs(afterRoot);
  if (JSON.stringify(beforeRefs) !== JSON.stringify(afterRefs)) {
    fields.push({
      fieldLabel: "layoutClassRefs",
      before: formatPatchValue(beforeRefs),
      after: formatPatchValue(afterRefs),
    });
  }

  const beforeNodes = readPatchNodes(beforeRoot);
  const afterNodes = readPatchNodes(afterRoot);
  if (beforeNodes.length !== afterNodes.length) {
    fields.push({
      fieldLabel: "nodes.length",
      before: String(beforeNodes.length),
      after: String(afterNodes.length),
    });
  }

  const afterById = new Map(
    afterNodes.map((node) => [
      typeof node.nodeId === "string" ? node.nodeId : "",
      node,
    ]),
  );
  for (const beforeNode of beforeNodes) {
    const nodeId = typeof beforeNode.nodeId === "string" ? beforeNode.nodeId : "";
    const afterNode = afterById.get(nodeId);
    if (!afterNode) continue;
    for (const key of ["x", "y", "width", "height", "orderIndex", "slotKey", "parentNodeId"] as const) {
      const beforeValue = beforeNode[key];
      const afterValue = afterNode[key];
      if (JSON.stringify(beforeValue) !== JSON.stringify(afterValue)) {
        fields.push({
          fieldLabel: `node:${nodeId}:${key}`,
          before: formatPatchValue(beforeValue),
          after: formatPatchValue(afterValue),
        });
      }
    }
  }

  if (fields.length === 0 && beforeJson.trim() !== afterJson.trim()) {
    fields.push({
      fieldLabel: "tensorPatchJson",
      before: `${beforeJson.trim().length} chars`,
      after: `${afterJson.trim().length} chars`,
    });
  }

  const classResolutions = buildLayoutClassPreviewResolutions(afterRefs);
  const canvasRootClassName = resolveCanvasRootPreviewClassName(afterRefs);

  return {
    fields,
    classResolutions,
    canvasRootClassName,
    backendMessage,
    beforeJson,
    afterJson,
    nodeCountBefore: beforeNodes.length,
    nodeCountAfter: afterNodes.length,
  };
}
