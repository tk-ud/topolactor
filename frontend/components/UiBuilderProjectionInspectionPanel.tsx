import { useEffect, useState } from "preact/hooks";
import type { JSX } from "preact";
import {
  fetchDraftPreview,
  fetchDraftPreviewLayouts,
  type DraftPreviewLayout,
  type DraftPreviewResult,
} from "../api/draftPreview.ts";
import { resolvePreviewErrorMessage } from "../runtime/draftPreviewErrors.ts";
import {
  parseDraftPreviewQueryParams,
  renderTopologyIntentInSlot,
} from "../runtime/draftPreviewProjection.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import {
  type ComponentSpec,
  renderEmission,
} from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { queueClientCommand } from "../runtime/frontendScheduler.ts";
import { resolveProjectionEntryAxes } from "../runtime/projectionEntry.ts";
import type { UserOperation } from "../runtime/resolveOperationVector.ts";
import {
  type AppliedProjectionConfirmation,
  buildAppliedProjectionConfirmation,
  buildPropBindingsConfirmation,
  type CanvasAppliedComparison,
  compareCanvasPreviewWithApplied,
  type PropBindingConfirmationEntry,
} from "../lib/uiBuilderProjectionInspection.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import type { Emission } from "../api/dispatch.ts";

type InspectionState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "error"; code?: string; message: string }
  | {
    status: "done";
    previewSpecs: ComponentSpec[];
    previewResult: DraftPreviewResult;
    appliedSpecs: ComponentSpec[] | null;
    appliedEmission: Emission | null;
    appliedError: string | null;
    appliedConfirmation: AppliedProjectionConfirmation | null;
    propBindingEntries: PropBindingConfirmationEntry[];
    comparison: CanvasAppliedComparison | null;
  };

/**
 * /admin/ui-builder 投影インスペクション（読み取り専用）。
 *
 * Read-only inspection component scope (bundle
 * ui-projection-surface-architecture-reinforcement): production-equivalent
 * render confirmation, canvas preview vs applied projection comparison,
 * applied topology / route / package / manifest confirmation, read query
 * confirmation, propBindings confirmation (resolved from emission.data
 * branches), rows / activeColumns / displayColumnMode confirmation.
 *
 * Boundary: this panel is NOT persistence authority, NOT promotion authority,
 * NOT a canonical projection entry, and NOT a seed fallback. It issues only
 * read fetches (draft-preview reads + a Search dispatch) and renders with
 * previewMode inert bindings — no runtime effect executes from here.
 */
export default function UiBuilderProjectionInspectionPanel({
  scopedRouteKey,
  scopedLayoutId,
}: {
  scopedRouteKey?: string;
  scopedLayoutId?: string;
}): JSX.Element {
  const [layouts, setLayouts] = useState<DraftPreviewLayout[]>([]);
  const [layoutsError, setLayoutsError] = useState<string | null>(null);
  const [selectedLayoutId, setSelectedLayoutId] = useState(scopedLayoutId ?? "");
  const [inspection, setInspection] = useState<InspectionState>({
    status: "idle",
  });

  useEffect(() => {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
    // ?layoutId= deep-link preselection (e.g. /admin/ui-builder?layoutId=...#projection-inspection).
    const query = parseDraftPreviewQueryParams(
      globalThis.location?.search ?? "",
    );
    if (query.layoutId) setSelectedLayoutId(query.layoutId);
    (async () => {
      const res = await fetchDraftPreviewLayouts(token);
      if (!res.success) {
        setLayoutsError(
          res.errors?.[0]?.message ?? res.errors?.[0]?.code ??
            "レイアウト一覧の取得に失敗しました",
        );
        return;
      }
      setLayouts(res.layouts ?? []);
    })();
  }, []);

  useEffect(() => {
    if (scopedLayoutId && !selectedLayoutId) setSelectedLayoutId(scopedLayoutId);
    // Scope preselection only — the author's own selection is never overwritten.
  }, [scopedLayoutId]);

  async function runInspection() {
    if (!selectedLayoutId) return;
    setInspection({ status: "loading" });
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;

    // Canvas preview side: applied layout draft-preview read (renderEmission経路).
    const previewResult = await fetchDraftPreview(selectedLayoutId, token);
    if (!previewResult.success) {
      const code = previewResult.errors?.[0]?.code;
      const apiMessage = previewResult.errors?.[0]?.message;
      setInspection({
        status: "error",
        code,
        message: code
          ? resolvePreviewErrorMessage(code, apiMessage)
          : (apiMessage ?? "プレビューの取得に失敗しました"),
      });
      return;
    }
    const previewEmission = draftPreviewResultToEmission(previewResult);
    const previewSpecs = previewEmission
      ? renderEmission(previewEmission, defaultComponentRegistry, {
        previewMode: true,
      })
      : [];

    // Applied projection side: the same route/package/manifest-aware production
    // read entry (Search dispatch). Read query only — no mutation action.
    const routeTarget = previewResult.routeKey ??
      layouts.find((l) => l.layoutId === selectedLayoutId)?.routeKey ??
      scopedRouteKey;
    let appliedSpecs: ComponentSpec[] | null = null;
    let appliedEmission: Emission | null = null;
    let appliedError: string | null = null;
    let appliedConfirmation: AppliedProjectionConfirmation | null = null;
    let propBindingEntries: PropBindingConfirmationEntry[] = [];
    let comparison: CanvasAppliedComparison | null = null;

    if (!routeTarget) {
      appliedError = "ルートを解決できないため、実際の画面表示を読み取れません。";
    } else {
      const axes: UserOperation = resolveProjectionEntryAxes({
        routeTarget,
      });
      const dispatchResult = await queueClientCommand(axes, token);
      if (!dispatchResult.success || !dispatchResult.emission) {
        appliedError = dispatchResult.errors?.[0]?.message ??
          dispatchResult.errors?.[0]?.code ??
          "実際の画面表示の読み取りに失敗しました";
      } else {
        appliedEmission = dispatchResult.emission;
        // previewMode: read-only inspection must not execute runtime effects.
        appliedSpecs = renderEmission(
          appliedEmission,
          defaultComponentRegistry,
          { previewMode: true },
        );
        appliedConfirmation = buildAppliedProjectionConfirmation(
          axes,
          appliedEmission,
        );
        propBindingEntries = buildPropBindingsConfirmation(
          appliedEmission.layoutNodes,
          appliedEmission.data,
        );
        comparison = compareCanvasPreviewWithApplied(
          previewResult.layoutNodes,
          appliedEmission.layoutNodes,
        );
      }
    }

    setInspection({
      status: "done",
      previewSpecs,
      previewResult,
      appliedSpecs,
      appliedEmission,
      appliedError,
      appliedConfirmation,
      propBindingEntries,
      comparison,
    });
  }

  return (
    <section
      id="projection-inspection"
      data-uibuilder-inspection="read_only"
      aria-label="投影インスペクション（読み取り専用）"
      class="mb-4 rounded-lg border border-slate-200 bg-white p-3"
    >
      <div class="mb-2 flex flex-wrap items-center gap-2">
        <h2 class="m-0 text-sm font-semibold text-slate-900">
          投影インスペクション（読み取り専用）
        </h2>
        <span class="rounded bg-slate-100 px-2 py-0.5 text-[0.6rem] text-slate-600">
          保存・承認・反映は行いません
        </span>
      </div>
      <p class="mb-3 mt-0 text-xs text-slate-600">
        反映済みの配置プレビューと、実際の画面表示（本番と同じ読み取り）を読み取り専用で並べて確認します。
        内部識別子・データ対応の詳細は「技術情報」に表示します。
      </p>

      {layoutsError && (
        <div class="mb-3 rounded border border-red-200 bg-red-50 p-2 text-xs text-red-700">
          {layoutsError}
        </div>
      )}

      <div class="mb-3 flex flex-wrap items-end gap-2">
        <label class="block text-xs text-gray-700" htmlFor="inspection-layout-select">
          レイアウト（Apply 済み配置）
          <select
            id="inspection-layout-select"
            class="mt-1 block w-64 rounded border border-gray-300 p-1.5 text-xs"
            value={selectedLayoutId}
            onChange={(e) =>
              setSelectedLayoutId((e.target as HTMLSelectElement).value)}
          >
            <option value="">-- レイアウトを選択 --</option>
            {layouts.map((l) => (
              <option key={l.layoutId} value={l.layoutId}>
                {l.layoutKey} ({l.routeKey})
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          class="rounded bg-slate-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-slate-800 disabled:opacity-50"
          disabled={!selectedLayoutId || inspection.status === "loading"}
          onClick={() => runInspection()}
        >
          {inspection.status === "loading" ? "検査中..." : "投影を検査"}
        </button>
      </div>

      {inspection.status === "error" && (
        <div class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          <p class="m-0">{inspection.message}</p>
          {inspection.code && (
            <details class="mt-1 text-xs text-red-500">
              <summary class="cursor-pointer">技術情報（開発者向け）</summary>
              <p class="mt-0.5 font-mono">{inspection.code}</p>
            </details>
          )}
        </div>
      )}

      {inspection.status === "done" && (() => {
        const {
          previewSpecs,
          previewResult,
          appliedSpecs,
          appliedError,
          appliedConfirmation,
          propBindingEntries,
          comparison,
        } = inspection;
        const sampleRow = previewResult.initialDataRows?.[0];
        const sortedPreviewNodes = (previewResult.layoutNodes ?? [])
          .slice()
          .sort((a, b) => a.orderIndex - b.orderIndex);
        return (
          <div class="space-y-4">
            {comparison && !comparison.matches && (
              <div class="rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                編集中の配置と反映済みの画面に差分があります。詳細は「技術情報」を開いて確認してください。
              </div>
            )}

            {appliedError && (
              <div class="rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                実際の画面表示を読み取れませんでした: {appliedError}
              </div>
            )}

            {/* production-equivalent render confirmation (read-only, inert bindings) */}
            <div class="grid gap-3 md:grid-cols-2">
              <div>
                <p class="mb-1 mt-0 text-xs font-semibold text-slate-700">
                  配置プレビュー（編集サーフェスの読み取り）
                </p>
                <div
                  data-projection-island="inspection_preview"
                  data-projection-surface="ui_builder_inspection_preview"
                  class="rounded border border-gray-200 p-2"
                >
                  <LayoutProjectionTree
                    specs={previewSpecs}
                    layoutId={previewResult.layoutId}
                    rootLayoutClassRefs={previewResult.rootLayoutClassRefs}
                  />
                </div>
              </div>
              <div>
                <p class="mb-1 mt-0 text-xs font-semibold text-slate-700">
                  実際の画面表示（本番と同じ読み取り）
                </p>
                <div
                  data-projection-island="inspection_applied"
                  data-projection-surface="ui_builder_inspection_applied"
                  class="rounded border border-gray-200 p-2"
                >
                  {appliedSpecs
                    ? <LayoutProjectionTree specs={appliedSpecs} />
                    : (
                      <p class="m-0 text-xs italic text-gray-400">
                        実際の画面表示を取得できませんでした
                      </p>
                    )}
                </div>
              </div>
            </div>

            {/* Internal vocabulary stays behind this technical disclosure
                (adminUxGuard normal-view boundary). */}
            <details class="text-xs">
              <summary class="cursor-pointer rounded border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 hover:bg-slate-100">
                技術情報（適用内容の確認）
              </summary>
              <div class="mt-2 space-y-3">
                {/* applied topology / route / package / manifest / read query confirmation */}
                {appliedConfirmation && (
                  <div
                    class="rounded border border-blue-100 bg-blue-50 px-3 py-2 font-mono space-y-0.5"
                    data-inspection-block="applied_confirmation"
                  >
                    <div class="text-blue-900 font-semibold font-sans">
                      applied topology 確認
                    </div>
                    <div>
                      read query: target={appliedConfirmation.readQuery.target}
                      {" / "}layer={appliedConfirmation.readQuery.layer}
                      {" / "}action={appliedConfirmation.readQuery.action}
                      {appliedConfirmation.readQuery.targetRef && (
                        <>{" / "}target_ref={appliedConfirmation.readQuery.targetRef}</>
                      )}
                    </div>
                    <div>result kind: {appliedConfirmation.resultKind ?? "—"}</div>
                    <div>manifest: {appliedConfirmation.manifestId ?? "—"}</div>
                    <div>layout: {appliedConfirmation.layoutId ?? "—"}</div>
                    <div>package: {appliedConfirmation.packageId ?? "—"}</div>
                    <div>route: {previewResult.routeKey ?? "—"}</div>
                    <div>
                      rows: {appliedConfirmation.rowCount ?? "—"}
                      {" / "}activeColumns:{" "}
                      {appliedConfirmation.activeColumns?.join(", ") ?? "—"}
                      {" / "}displayColumnMode:{" "}
                      {appliedConfirmation.displayColumnMode ?? "—"}
                      {" / "}aggregationResults:{" "}
                      {appliedConfirmation.aggregationResultCount ?? "—"}
                    </div>
                  </div>
                )}

                {/* canvas preview vs applied projection comparison */}
                {comparison && (
                  <div
                    class="rounded border border-slate-200 bg-slate-50 px-3 py-2"
                    data-inspection-block="canvas_vs_applied"
                  >
                    <p class="m-0 font-semibold text-slate-800">
                      canvas preview vs applied projection
                    </p>
                    <p class="m-0 mt-1 font-mono">
                      preview nodes: {comparison.previewNodeCount}
                      {" / "}applied nodes: {comparison.appliedNodeCount}
                      {" / "}
                      {comparison.matches ? "一致" : "差分あり"}
                    </p>
                    {!comparison.matches && (
                      <p class="m-0 mt-1 font-mono text-amber-700">
                        {comparison.onlyInPreview.length > 0 &&
                          `preview のみ: ${comparison.onlyInPreview.join(", ")} `}
                        {comparison.onlyInApplied.length > 0 &&
                          `applied のみ: ${comparison.onlyInApplied.join(", ")}`}
                      </p>
                    )}
                  </div>
                )}

                {/* propBindings confirmation from emission.data branches */}
                {propBindingEntries.length > 0 && (
                  <div
                    class="rounded border border-slate-200 bg-white px-3 py-2"
                    data-inspection-block="prop_bindings"
                  >
                    <p class="m-0 font-semibold text-slate-800">
                      propBindings 確認（emission.data の枝から解決）
                    </p>
                    <table class="mt-1 w-full text-left font-mono text-[0.65rem]">
                      <thead>
                        <tr class="text-slate-500">
                          <th class="pr-2">node</th>
                          <th class="pr-2">prop</th>
                          <th class="pr-2">source</th>
                          <th class="pr-2">status</th>
                          <th>resolved</th>
                        </tr>
                      </thead>
                      <tbody>
                        {propBindingEntries.map((entry, i) => (
                          <tr key={`${entry.nodeId}-${entry.propName}-${i}`}>
                            <td class="pr-2">{entry.nodeId}</td>
                            <td class="pr-2">{entry.propName}</td>
                            <td class="pr-2">{entry.source}</td>
                            <td class="pr-2">{entry.status}</td>
                            <td>
                              {entry.status === "resolved"
                                ? `${entry.resolvedKind}${
                                  entry.resolvedCount !== undefined
                                    ? `(${entry.resolvedCount})`
                                    : ""
                                }`
                                : "—"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </details>

            {/* sample rows (topology intent) — reference display only, not proof */}
            {sampleRow && sortedPreviewNodes.length > 0 && (
              <details class="text-xs">
                <summary class="cursor-pointer text-slate-500">
                  サンプルデータ（initialDataRows）のスロット対応（参考表示）
                </summary>
                <div class="mt-1 space-y-1">
                  {sortedPreviewNodes.map((node, i) => {
                    const fields = renderTopologyIntentInSlot(
                      sampleRow,
                      node.slotKey,
                      i,
                      sortedPreviewNodes.length,
                    );
                    if (fields.length === 0) return null;
                    return (
                      <p
                        key={node.nodeId ?? `slot-${i}`}
                        class="m-0 font-mono text-[0.65rem] text-slate-600"
                      >
                        {node.slotKey ?? "(unnamed)"}:{" "}
                        {fields.map(({ key, value }) => `${key}=${value}`).join(", ")}
                      </p>
                    );
                  })}
                </div>
              </details>
            )}
          </div>
        );
      })()}
    </section>
  );
}
