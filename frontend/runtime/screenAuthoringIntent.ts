/**
 * Per-manifest screen operation → dispatcher axis mapping.
 * Target surface: admin/contents (manifest single-screen authoring).
 * Axes are manifest-scoped (target + layer) — aligned with ManifestScreenOperationDeriver.
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

/**
 * Context for dispatcher axis resolution only.
 * manifestKey: identifier for dispatcher target (hub_grouping key), NOT topology.name / admin route / physical table.
 * manifestId: persistent manifest UUID.
 * topology.name (topologySystemName) is the SSOT for admin route, primary physical table, and UI Builder key —
 * kept in ManifestScreenDesignDraft and AdminManifestAssignScreenDataShapeRequestDto, not here.
 */
export type ScreenAxisContext = {
  manifestKey?: string | null;
  manifestId?: string | null;
};

export const SCREEN_OPERATION_OPTIONS: { kind: ScreenOperationKind; label: string }[] = [
  { kind: "list", label: "一覧" },
  { kind: "search", label: "検索" },
  { kind: "detail", label: "詳細" },
  { kind: "create", label: "登録" },
  { kind: "update", label: "更新" },
  { kind: "aggregation_view", label: "集計ビュー" },
];

function sanitizeTarget(raw: string): string {
  const sanitized = raw
    .split("")
    .map((c) => (/[a-zA-Z0-9._-]/.test(c) ? c : "_"))
    .join("")
    .replace(/^_+|_+$/g, "");
  return sanitized || "screen_unassigned";
}

function resolveManifestTarget(ctx?: ScreenAxisContext): string {
  const key = ctx?.manifestKey?.trim();
  if (key) return sanitizeTarget(key);
  const id = ctx?.manifestId?.trim();
  if (id) return `screen_${id.replace(/-/g, "")}`;
  return "screen_unassigned";
}

/**
 * Maps screen operation kind + manifest identity → dispatcher axes.
 */
export function screenOperationToDispatcherAxes(
  kind: ScreenOperationKind,
  ctx?: ScreenAxisContext,
): DispatcherAxes {
  const target = resolveManifestTarget(ctx);
  const base = {
    role: "admin",
    target,
    runtimeDestination: "topology_transform_runtime",
  };

  switch (kind) {
    case "list":
      return { ...base, layer: "screen_list", action: "Read" };
    case "search":
      return { ...base, layer: "screen_entity", action: "Search" };
    case "detail":
      return { ...base, layer: "screen_detail", action: "Read" };
    case "create":
      return { ...base, layer: "screen_entity", action: "Create" };
    case "update":
      return { ...base, layer: "screen_entity", action: "Update" };
    case "aggregation_view":
      return { ...base, layer: "screen_aggregation", action: "Read" };
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

  if (layer === "screen_aggregation") return "aggregation_view";
  if (action === "Search") return "search";
  if (action === "Create") return "create";
  if (action === "Update") return "update";
  if (layer === "screen_detail") return "detail";
  if (layer === "screen_list") return "list";
  return "list";
}

export function primaryOperationKind(draft: {
  operationKinds?: ScreenOperationKind[];
  operationKind: ScreenOperationKind;
}): ScreenOperationKind {
  return draft.operationKinds?.[0] ?? draft.operationKind;
}

/** Step 1: empty manifest shell with default list axes until step 3 assigns kinds. */
export function buildStep1DraftInput(manifestId?: string | null): AdminManifestDraftInput {
  return buildDraftInputFromScreenIntent({
    operationKind: "list",
    manifestId,
  });
}

export function buildDraftInputFromScreenIntent(input: {
  operationKind: ScreenOperationKind;
  manifestKey?: string | null;
  manifestId?: string | null;
  debugAxesOverride?: Partial<DispatcherAxes> | null;
}): AdminManifestDraftInput {
  const derived = screenOperationToDispatcherAxes(input.operationKind, {
    manifestKey: input.manifestKey,
    manifestId: input.manifestId,
  });
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
    screenOperationKind: input.operationKind,
  };
}

const SCREEN_LABEL_STORAGE_KEY = "topolactor_screen_labels_v1";

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
