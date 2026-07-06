import { useState } from "preact/hooks";
import { JSX } from "preact";
import {
  applyWiringDropEdit,
  buildWiringGraphProjection,
  findRuntimeInteractionPolicyErrors,
  type InternalApiWiringInput,
  type WiringGraphEdge,
  type WiringNode,
  type WiringSettingCategory,
} from "../lib/uiBuilderWiringProjection.ts";
import {
  UX_WIRING_CATEGORY_EXTERNAL_API,
  UX_WIRING_CATEGORY_EXTERNAL_INSTANCE,
  UX_WIRING_CATEGORY_INTERNAL_API,
  UX_WIRING_CATEGORY_SIDE_EFFECT,
  UX_WIRING_CATEGORY_UI_STATE_UPDATE,
  UX_WIRING_CATEGORY_UI_WATCH_BINDING,
  UX_WIRING_DROP_GUIDANCE,
  UX_WIRING_MODE_EMPTY,
  UX_WIRING_MODE_HINT,
  uxRuntimeInteractionTriggerLabel,
} from "../content/adminUxTerms.ts";

/**
 * Wiring canvas — switchable projection/edit mode over runtimeInteractions.
 *
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml wiring_mode
 * - View/edit projection only: the graph below is rebuilt from the given draft
 *   nodes on every render (rehydrate from runtimeInteractions) and is never a
 *   persistence source.
 * - Edits are expressed as typed runtimeInteraction patches handed back through
 *   onApplyNodes; persistence stays on the existing layout_patch draft lanes.
 * - Invalid drops surface an explicit error and do not mutate the draft.
 */

/**
 * Canonical setting category labels — no implementation-derived catch-all.
 * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml setting_category_taxonomy
 */
const CATEGORY_LABELS: Record<WiringSettingCategory, string> = {
  ui_watch_binding: UX_WIRING_CATEGORY_UI_WATCH_BINDING,
  ui_state_update: UX_WIRING_CATEGORY_UI_STATE_UPDATE,
  side_effect_setting: UX_WIRING_CATEGORY_SIDE_EFFECT,
  internal_api: UX_WIRING_CATEGORY_INTERNAL_API,
  external_api_integration: UX_WIRING_CATEGORY_EXTERNAL_API,
  external_instance_integration: UX_WIRING_CATEGORY_EXTERNAL_INSTANCE,
};

function edgeTargetLabel(
  edge: WiringGraphEdge,
  nodes: readonly WiringNode[],
): string {
  if (edge.targetKind === "node" && edge.targetRef) {
    const target = nodes.find((n) => n.nodeId === edge.targetRef);
    return target?.componentKey || edge.targetRef;
  }
  if (edge.targetRef) return edge.targetRef;
  return "（未設定）";
}

type Props<T extends WiringNode> = {
  nodes: readonly T[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
  onApplyNodes: (nodes: T[], label: string) => void;
  /** 内部API package wiring lane rows for projection (view only; persistence stays on package wiring). */
  internalApiWirings?: readonly InternalApiWiringInput[];
};

export default function WiringGraphPanel<T extends WiringNode>({
  nodes,
  selectedNodeId,
  onSelectNode,
  onApplyNodes,
  internalApiWirings,
}: Props<T>): JSX.Element {
  const [dropError, setDropError] = useState<string | null>(null);
  // Rehydrated per render from runtimeInteractions — no cached graph state.
  const projection = buildWiringGraphProjection(
    nodes,
    internalApiWirings ?? [],
  );
  const policyErrors = findRuntimeInteractionPolicyErrors(nodes);

  const handleDrop = (targetNodeId: string, e: DragEvent) => {
    e.preventDefault();
    const sourceNodeId =
      e.dataTransfer?.getData("application/x-wiring-source-node") ?? "";
    if (!sourceNodeId) return;
    const result = applyWiringDropEdit(nodes, sourceNodeId, targetNodeId);
    if (!result.ok) {
      setDropError(result.error);
      return;
    }
    setDropError(null);
    onApplyNodes(result.nodes, "配線を追加（ドラッグ＆ドロップ）");
  };

  return (
    <section
      class="min-h-[320px] rounded-lg border border-indigo-200 bg-indigo-50/30 p-3"
      data-wiring-canvas="true"
      aria-label="配線ビュー"
    >
      <p class="mb-2 text-[0.65rem] text-indigo-800">{UX_WIRING_MODE_HINT}</p>
      <p class="mb-2 text-[0.6rem] text-slate-600">{UX_WIRING_DROP_GUIDANCE}</p>

      {dropError && (
        <div
          class="mb-2 rounded border border-red-200 bg-red-50 px-2 py-1 text-[0.65rem] text-red-800"
          role="alert"
        >
          {dropError}
        </div>
      )}
      {policyErrors.length > 0 && (
        <div
          class="mb-2 rounded border border-amber-200 bg-amber-50 px-2 py-1"
          role="alert"
        >
          <p class="text-[0.62rem] font-semibold text-amber-900">
            配線ポリシー違反（適用前に解消が必要）
          </p>
          <ul class="ml-3 list-disc text-[0.6rem] text-amber-800">
            {policyErrors.map((msg) => <li key={msg}>{msg}</li>)}
          </ul>
        </div>
      )}

      {projection.edges.length === 0 && (
        <p class="rounded border border-dashed border-indigo-200 px-2 py-4 text-center text-xs text-slate-500">
          {UX_WIRING_MODE_EMPTY}
        </p>
      )}

      {projection.watchBindings.length > 0 && (
        <div class="mb-2 rounded border border-sky-200 bg-sky-50/50 px-2 py-1">
          <p class="text-[0.62rem] font-semibold text-sky-900">
            {UX_WIRING_CATEGORY_UI_WATCH_BINDING}（宣言済み状態スロット）
          </p>
          <ul class="flex flex-wrap gap-1 text-[0.58rem] text-sky-800">
            {projection.watchBindings.map((b) => (
              <li
                key={`${b.nodeId}-${b.stateKey}`}
                class="rounded bg-white px-1 py-0.5"
              >
                {b.nodeId}.{b.stateKey}
              </li>
            ))}
          </ul>
        </div>
      )}

      <ul class="space-y-1">
        {projection.edges.map((edge) => (
          <li
            key={`${edge.sourceNodeId}-${edge.interactionIndex}-${edge.effect}`}
          >
            <button
              type="button"
              class={`flex w-full flex-wrap items-center gap-1 rounded border px-2 py-1 text-left text-[0.65rem] ${
                edge.sourceNodeId === selectedNodeId
                  ? "border-blue-400 bg-blue-50"
                  : "border-slate-200 bg-white hover:border-blue-300"
              }`}
              onClick={() => onSelectNode(edge.sourceNodeId)}
            >
              <span class="font-semibold text-slate-800">
                {edge.sourceLabel}
              </span>
              <span class="text-slate-400">→</span>
              <span class="rounded bg-slate-100 px-1 text-slate-700">
                {uxRuntimeInteractionTriggerLabel(edge.trigger)}
              </span>
              <span class="text-slate-400">→</span>
              <span
                class={`rounded px-1 ${
                  edge.category === null
                    ? "bg-red-100 text-red-800"
                    : "bg-indigo-100 text-indigo-800"
                }`}
              >
                {edge.category === null
                  ? "分類語彙外（要修正）"
                  : CATEGORY_LABELS[edge.category]}
              </span>
              {edge.hasSideEffect && (
                <span class="rounded bg-emerald-100 px-1 text-emerald-800">
                  {UX_WIRING_CATEGORY_SIDE_EFFECT}
                </span>
              )}
              <span class="text-slate-400">→</span>
              <span class="text-slate-700">{edgeTargetLabel(edge, nodes)}</span>
              {edge.previewInert && (
                <span class="ml-auto rounded bg-slate-200 px-1 text-[0.55rem] text-slate-600">
                  プレビュー非実行
                </span>
              )}
            </button>
          </li>
        ))}
      </ul>

      <h4 class="mb-1 mt-3 text-[0.62rem] font-semibold text-slate-700">
        部品（ドラッグして配線）
      </h4>
      <ul class="flex flex-wrap gap-1">
        {nodes.map((node) => (
          <li key={node.nodeId}>
            <span
              draggable
              class={`inline-block cursor-grab rounded border px-2 py-0.5 text-[0.62rem] ${
                node.nodeId === selectedNodeId
                  ? "border-blue-400 bg-blue-50 text-blue-900"
                  : "border-slate-200 bg-white text-slate-700"
              }`}
              onDragStart={(e: DragEvent) => {
                e.dataTransfer?.setData(
                  "application/x-wiring-source-node",
                  node.nodeId,
                );
              }}
              onDragOver={(e: DragEvent) => e.preventDefault()}
              onDrop={(e: DragEvent) => handleDrop(node.nodeId, e)}
              onClick={() => onSelectNode(node.nodeId)}
            >
              {node.componentKey || node.nodeId}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
