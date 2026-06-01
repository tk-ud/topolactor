/**
 * Per-manifest screen operation → dispatcher axis mapping.
 * Target surface: admin/contents (manifest single-screen authoring). Not admin/manifests.
 */
import type { AdminManifestDraftInput } from "../api/adminApi.ts";

/** User-facing screen operation kinds (not dispatcher axis names). */
export type ScreenOperationKind =
  | "list"
  | "search"
  | "detail"
  | "create"
  | "update"
  | "aggregation_view";

export type DispatcherAxes = {
  role: string;
  target: string;
  layer: string;
  action: string;
  runtimeDestination: string;
};

export const SCREEN_OPERATION_OPTIONS: { kind: ScreenOperationKind; label: string }[] = [
  { kind: "list", label: "一覧" },
  { kind: "search", label: "検索" },
  { kind: "detail", label: "詳細" },
  { kind: "create", label: "登録" },
  { kind: "update", label: "更新" },
  { kind: "aggregation_view", label: "集計ビュー" },
];

const SCREEN_LABEL_STORAGE_KEY = "topolactor_screen_labels_v1";

/**
 * Maps screen operation kind → dispatcher axes.
 * Aligned with db/seed_empty.sql default entity routes (admin/default/entity/*).
 */
export function screenOperationToDispatcherAxes(kind: ScreenOperationKind): DispatcherAxes {
  switch (kind) {
    case "list":
      return {
        role: "admin",
        target: "default",
        layer: "entity",
        action: "Read",
        runtimeDestination: "topology_transform_runtime",
      };
    case "search":
      return {
        role: "admin",
        target: "default",
        layer: "entity",
        action: "Search",
        runtimeDestination: "topology_transform_runtime",
      };
    case "detail":
      return {
        role: "admin",
        target: "default",
        layer: "entity",
        action: "Read",
        runtimeDestination: "topology_transform_runtime",
      };
    case "create":
      return {
        role: "admin",
        target: "default",
        layer: "entity",
        action: "Create",
        runtimeDestination: "topology_transform_runtime",
      };
    case "update":
      return {
        role: "admin",
        target: "default",
        layer: "entity",
        action: "Update",
        runtimeDestination: "topology_transform_runtime",
      };
    case "aggregation_view":
      return {
        role: "admin",
        target: "default",
        layer: "aggregation",
        action: "Read",
        runtimeDestination: "topology_transform_runtime",
      };
  }
}

export function screenOperationLabel(kind: ScreenOperationKind): string {
  return SCREEN_OPERATION_OPTIONS.find((o) => o.kind === kind)?.label ?? kind;
}

/** Best-effort inverse mapping for list/detail display. */
export function dispatcherAxesToScreenOperationKind(axes: {
  layer?: string | null;
  action?: string | null;
}): ScreenOperationKind {
  const layer = (axes.layer ?? "").toLowerCase();
  const action = axes.action ?? "";

  if (layer === "aggregation") return "aggregation_view";
  if (action === "Search") return "search";
  if (action === "Create") return "create";
  if (action === "Update") return "update";
  if (action === "Read" && layer === "entity") return "list";
  return "list";
}

export function buildDraftInputFromScreenIntent(input: {
  operationKind: ScreenOperationKind;
  debugAxesOverride?: Partial<DispatcherAxes> | null;
}): AdminManifestDraftInput {
  const derived = screenOperationToDispatcherAxes(input.operationKind);
  const axes = input.debugAxesOverride
    ? { ...derived, ...input.debugAxesOverride }
    : derived;
  return {
    role: axes.role,
    target: axes.target,
    layer: axes.layer,
    action: axes.action,
    runtimeDestination: axes.runtimeDestination,
    projectionDefinition: null,
  };
}

function readLabelMap(): Record<string, string> {
  if (typeof globalThis.localStorage === "undefined") return {};
  try {
    const raw = localStorage.getItem(SCREEN_LABEL_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, string>;
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

export function getStoredScreenLabel(manifestId: string): string | null {
  const map = readLabelMap();
  return map[manifestId] ?? null;
}

export function setStoredScreenLabel(manifestId: string, label: string): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const map = readLabelMap();
  const trimmed = label.trim();
  if (!trimmed) {
    delete map[manifestId];
  } else {
    map[manifestId] = trimmed;
  }
  localStorage.setItem(SCREEN_LABEL_STORAGE_KEY, JSON.stringify(map));
}

export function displayScreenTitle(
  manifestId: string,
  operationKind: ScreenOperationKind,
): string {
  return getStoredScreenLabel(manifestId) ?? `${screenOperationLabel(operationKind)} (${manifestId.slice(0, 8)}…)`;
}
