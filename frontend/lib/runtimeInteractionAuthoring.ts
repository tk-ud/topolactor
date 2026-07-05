import {
  UX_RUNTIME_INTERACTION_INTENT_CLOSE,
  UX_RUNTIME_INTERACTION_INTENT_OPEN,
  UX_RUNTIME_INTERACTION_INTENT_TOGGLE,
  UX_RUNTIME_INTERACTION_SURFACE_DIALOG,
  UX_RUNTIME_INTERACTION_SURFACE_DRAWER,
  UX_RUNTIME_INTERACTION_SURFACE_MODAL,
  uxRuntimeInteractionTriggerLabel,
} from "../content/adminUxTerms.ts";

export type OverlayIntent = "open" | "close" | "toggle";

export type OverlayTargetNode = {
  nodeId: string;
  componentKey?: string;
  componentKind?: string;
  propsJson?: string;
};

/** Component kinds that can be opened/closed via disclosure runtime interactions. */
export const OVERLAY_OPENABLE_COMPONENT_KINDS = [
  "disclosure/modal",
  "disclosure/drawer",
  "disclosure/dialog",
  "table_op/row_detail_drawer",
  "inline_edit/audit_diff_drawer",
] as const;

export function overlaySurfaceFamily(
  componentKind: string | undefined,
): "modal" | "drawer" | "dialog" {
  if (componentKind === "disclosure/modal") return "modal";
  if (componentKind === "disclosure/dialog") return "dialog";
  return "drawer";
}

export function resolveDisclosureActionType(
  intent: OverlayIntent,
  componentKind: string,
): string {
  const family = overlaySurfaceFamily(componentKind);
  const familyCap = family.charAt(0).toUpperCase() + family.slice(1);
  return `${intent}${familyCap}`;
}

export function isOverlayDisclosureAction(actionType: string): boolean {
  return /^(open|close|toggle)(Modal|Drawer|Dialog)$/.test(actionType);
}

export type RuntimeInteractionCategory =
  | "overlay"
  | "external_port"
  | "instance_operation"
  | "legacy";

export function runtimeInteractionCategory(actionType: string): RuntimeInteractionCategory {
  if (isOverlayDisclosureAction(actionType)) return "overlay";
  if (actionType === "dispatchExternalPort") return "external_port";
  if (actionType === "dispatchInstanceOperation") return "instance_operation";
  return "legacy";
}

export function defaultExternalPortInteraction(): {
  trigger: string;
  actionType: string;
} {
  return { trigger: "click", actionType: "dispatchExternalPort" };
}

export function defaultInstanceOperationInteraction(): {
  trigger: string;
  actionType: string;
} {
  return { trigger: "click", actionType: "dispatchInstanceOperation" };
}

export function isActionLikeComponentKind(componentKind?: string): boolean {
  if (!componentKind) return false;
  return componentKind.startsWith("action/") ||
    componentKind.startsWith("form_input/") ||
    componentKind === "form_input/button";
}

export function overlayIntentFromAction(actionType: string): OverlayIntent {
  if (actionType.startsWith("close")) return "close";
  if (actionType.startsWith("toggle")) return "toggle";
  return "open";
}

export function parsePropsJsonTitle(propsJson?: string): string | null {
  if (!propsJson?.trim()) return null;
  try {
    const parsed = JSON.parse(propsJson) as Record<string, unknown>;
    const title = parsed.title ?? parsed.label;
    return typeof title === "string" && title.trim() ? title.trim() : null;
  } catch {
    return null;
  }
}

export function friendlyOverlayTargetLabel(node: OverlayTargetNode): string {
  const kind = node.componentKind ?? "";
  const surface = overlaySurfaceFamily(kind);
  const surfaceJa = surface === "modal"
    ? UX_RUNTIME_INTERACTION_SURFACE_MODAL
    : surface === "dialog"
    ? UX_RUNTIME_INTERACTION_SURFACE_DIALOG
    : UX_RUNTIME_INTERACTION_SURFACE_DRAWER;
  const title = parsePropsJsonTitle(node.propsJson);
  if (title) return `${surfaceJa}：${title}`;
  const slug = node.componentKey?.split("/").pop() ?? node.nodeId.slice(0, 12);
  return `${surfaceJa}（${slug}）`;
}

export function listOverlayTargetNodes(
  nodes: readonly OverlayTargetNode[],
): OverlayTargetNode[] {
  return nodes.filter((n) =>
    OVERLAY_OPENABLE_COMPONENT_KINDS.includes(
      (n.componentKind ?? "") as typeof OVERLAY_OPENABLE_COMPONENT_KINDS[number],
    )
  );
}

export function defaultOverlayOpenInteraction(
  candidates: readonly OverlayTargetNode[],
): {
  trigger: string;
  actionType: string;
  targetNodeId?: string;
  statePath: string;
} {
  const overlayTargets = listOverlayTargetNodes(candidates);
  const preferred = overlayTargets.find((n) => n.componentKind === "disclosure/modal") ??
    overlayTargets[0];
  if (!preferred?.componentKind) {
    return { trigger: "click", actionType: "openModal", statePath: "open" };
  }
  return {
    trigger: "click",
    actionType: resolveDisclosureActionType("open", preferred.componentKind),
    targetNodeId: preferred.nodeId,
    statePath: "open",
  };
}

export function applyOverlayIntentToInteraction(
  interaction: {
    trigger?: string;
    actionType: string;
    targetNodeId?: string;
    statePath?: string;
  },
  intent: OverlayIntent,
  targetNode: OverlayTargetNode | undefined,
): typeof interaction {
  if (!targetNode?.componentKind) return interaction;
  return {
    ...interaction,
    actionType: resolveDisclosureActionType(intent, targetNode.componentKind),
    statePath: interaction.statePath ?? "open",
  };
}

export const RUNTIME_INTERACTION_INTENT_OPTIONS = [
  { value: "open" as const, label: UX_RUNTIME_INTERACTION_INTENT_OPEN },
  { value: "close" as const, label: UX_RUNTIME_INTERACTION_INTENT_CLOSE },
  { value: "toggle" as const, label: UX_RUNTIME_INTERACTION_INTENT_TOGGLE },
];

export function runtimeInteractionTriggerOptions(triggers: readonly string[]) {
  return triggers.map((value) => ({
    value,
    label: uxRuntimeInteractionTriggerLabel(value),
  }));
}
