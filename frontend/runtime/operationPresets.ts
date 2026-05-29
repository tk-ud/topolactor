import type { UserOperation } from "./resolveOperationVector.ts";
import { resolveOperationVector } from "./resolveOperationVector.ts";

/** Demo seed UUIDs — must match db/demo_seed.sql and docs/demo-walkthrough.md Scenario E. */
export const DEMO_CONTEXT_SESSION_ID = "00000000-0000-0000-0000-000000000031";
export const DEMO_CONTEXT_TOKEN_ID_ACTIVE = "00000000-0000-0000-0000-000000000021";

export type OperationPresetContext = {
  ContextSessionId?: string;
  ContextTokenIds?: string;
};

export type OperationPresetGroup = "default" | "demo";

export type OperationPreset = {
  id: string;
  group: OperationPresetGroup;
  label: string;
  description: string;
  /** What the user should observe in the emission after a successful dispatch. */
  expectedObservation: string;
  operation: UserOperation;
  context?: OperationPresetContext;
};

export const OPERATION_PRESETS: OperationPreset[] = [
  {
    id: "default_entity_search",
    group: "default",
    label: "Default entity search",
    description: "基本の runtime emission を確認する（default トポロジ）",
    expectedObservation:
      "structureMapId / packageId / schemaId と componentIds が返り、default エンティティ検索の投影が表示される",
    operation: {
      operationType: "Search",
      target: "default",
      layer: "entity",
      action: "Search",
    },
  },
  {
    id: "demo_hub_overview",
    group: "demo",
    label: "Demo hub overview",
    description: "demo ハブの概要トポロジを backend runtime から取得する",
    expectedObservation:
      "demo_hub_overview_package 由来の component 展開と demo 用 structure map が emission に含まれる",
    operation: {
      operationType: "Search",
      target: "demo",
      layer: "hub",
      action: "overview",
    },
  },
  {
    id: "demo_entity_list",
    group: "demo",
    label: "Demo entity list",
    description: "demo エンティティ一覧を backend runtime から取得する",
    expectedObservation: "demo エンティティ一覧データが emission.data に含まれる",
    operation: {
      operationType: "Search",
      target: "demo",
      layer: "entity",
      action: "list",
    },
  },
  {
    id: "demo_hub_recommendation",
    group: "demo",
    label: "Demo hub + recommendation context",
    description:
      "demo:hub:overview を seed 済み session/token で実行し、context_route_recommendation を確認する",
    expectedObservation:
      'context_route_recommendation.status が "ok" になり、nextOperations に demo:entity:list が含まれる',
    operation: {
      operationType: "Search",
      target: "demo",
      layer: "hub",
      action: "overview",
    },
    context: {
      ContextSessionId: DEMO_CONTEXT_SESSION_ID,
      ContextTokenIds: DEMO_CONTEXT_TOKEN_ID_ACTIVE,
    },
  },
];

export function presetsForGroups(groups: OperationPresetGroup[]): OperationPreset[] {
  const set = new Set(groups);
  return OPERATION_PRESETS.filter((p) => set.has(p.group));
}

export function presetById(id: string): OperationPreset | undefined {
  return OPERATION_PRESETS.find((p) => p.id === id);
}

export function presetOperationKey(preset: OperationPreset): string {
  return resolveOperationVector(preset.operation).attractorKey;
}

export function presetHasContext(preset: OperationPreset): boolean {
  return Boolean(
    preset.context?.ContextSessionId?.trim() || preset.context?.ContextTokenIds?.trim(),
  );
}

export function buildDispatchContext(
  preset: OperationPreset,
  overrides?: OperationPresetContext,
): Record<string, string> {
  const context: Record<string, string> = {};
  const session = overrides?.ContextSessionId?.trim() ?? preset.context?.ContextSessionId?.trim();
  const tokens = overrides?.ContextTokenIds?.trim() ?? preset.context?.ContextTokenIds?.trim();
  if (session) context["ContextSessionId"] = session;
  if (tokens) context["ContextTokenIds"] = tokens;
  return context;
}

/** Match initial route defaults to a preset when no explicit preset id is passed. */
export function inferPresetId(
  initialOperation: Partial<UserOperation> | undefined,
  groups: OperationPresetGroup[],
): string {
  const candidates = presetsForGroups(groups);
  if (!initialOperation) return candidates[0]?.id ?? OPERATION_PRESETS[0].id;

  const key = `${initialOperation.target ?? ""}:${initialOperation.layer ?? ""}:${initialOperation.action ?? ""}`
    .toLowerCase();

  const match = candidates.find((p) => {
    const op = p.operation;
    const pk = `${op.target}:${op.layer}:${op.action}`.toLowerCase();
    return pk === key;
  });
  return match?.id ?? candidates[0]?.id ?? OPERATION_PRESETS[0].id;
}
