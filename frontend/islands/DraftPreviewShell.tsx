import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  fetchDraftPreviewLayouts,
  fetchDraftPreview,
  type DraftPreviewInitialDataRow,
  type DraftPreviewLayout,
  type DraftPreviewLayoutNode,
} from "../api/draftPreview.ts";
import { resolvePreviewErrorMessage } from "../runtime/draftPreviewErrors.ts";
import {
  parseDraftPreviewQueryParams,
  renderTopologyIntentInSlot,
} from "../runtime/draftPreviewProjection.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type PreviewState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "error"; code?: string; message: string }
  | {
      status: "success";
      layoutId: string;
      previewMode: "layout_only" | "layout_and_topology_intent";
      routeKey?: string;
      packageId?: string;
      layoutNodes: DraftPreviewLayoutNode[];
      rootLayoutClassRefs: string[];
      manifestId?: string;
      manifestStatus?: string;
      topologySystemName?: string;
      initialDataRows: DraftPreviewInitialDataRow[];
    };

/**
 * /demo — pre-publish projection preview (read-only).
 * Layout: UI Builder Apply 済み layout_patch_json + component_style_design.
 * Content: manifest screen_data_shape.initialDataRows (Contents Step 3 topology intent).
 */
export default function DraftPreviewShell(): JSX.Element {
  const [layouts, setLayouts] = useState<DraftPreviewLayout[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [initLoading, setInitLoading] = useState(true);
  const [selectedLayoutId, setSelectedLayoutId] = useState("");
  const [preview, setPreview] = useState<PreviewState>({ status: "idle" });

  useEffect(() => {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
    const query = parseDraftPreviewQueryParams(globalThis.location?.search ?? "");

    (async () => {
      const layoutsRes = await fetchDraftPreviewLayouts(token);

      if (!layoutsRes.success) {
        setLoadError(
          layoutsRes.errors?.[0]?.message ??
            layoutsRes.errors?.[0]?.code ??
            "レイアウト一覧の取得に失敗しました",
        );
        setInitLoading(false);
        return;
      }

      setLayouts(layoutsRes.layouts ?? []);
      if (query.layoutId) setSelectedLayoutId(query.layoutId);
      setInitLoading(false);
    })();
  }, []);

  async function runPreview() {
    if (!selectedLayoutId) return;

    setPreview({ status: "loading" });
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
    const result = await fetchDraftPreview(selectedLayoutId, token);

    if (!result.success) {
      const code = result.errors?.[0]?.code;
      const apiMessage = result.errors?.[0]?.message;
      const specificMessage = code
        ? resolvePreviewErrorMessage(code, apiMessage)
        : (apiMessage ?? "プレビューの取得に失敗しました");
      setPreview({
        status: "error",
        code,
        message: specificMessage,
      });
      return;
    }

    setPreview({
      status: "success",
      layoutId: result.layoutId ?? selectedLayoutId,
      previewMode: result.previewMode ?? "layout_only",
      routeKey: result.routeKey,
      packageId: result.packageId,
      layoutNodes: result.layoutNodes ?? [],
      rootLayoutClassRefs: result.rootLayoutClassRefs ?? [],
      manifestId: result.manifestId,
      manifestStatus: result.manifestStatus,
      topologySystemName: result.topologySystemName,
      initialDataRows: result.initialDataRows ?? [],
    });
  }

  if (initLoading) {
    return (
      <div class="py-8 text-center text-gray-400" aria-busy="true" aria-live="polite">
        レイアウト一覧を取得中...
      </div>
    );
  }

  if (loadError) {
    return (
      <div class="rounded-lg border border-red-200 bg-red-50 p-4">
        <p class="font-semibold text-red-700">一覧取得エラー</p>
        <p class="mt-1 text-sm text-red-600">{loadError}</p>
      </div>
    );
  }

  const selectedLayout = layouts.find((l) => l.layoutId === selectedLayoutId);

  return (
    <div class="space-y-6">
      <div class="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-800">
        <p class="m-0 font-semibold">投影の2層（SSOT）</p>
        <ul class="my-2 mb-0 pl-4 text-xs text-slate-600">
          <li>
            <strong>配置レイアウト</strong> — UIビルダーで Apply した{" "}
            <code>layout_patch_json</code>（+ デザイン <code>component_style_design</code>）
          </li>
          <li>
            <strong>コンテンツ（topology intent）</strong> — Contents Step 3 で保存した manifest の{" "}
            <code>screen_data_shape.initialDataRows</code>
            （routeKey = topologySystemName で自動解決）
          </li>
        </ul>
        <p class="m-0 text-xs text-slate-500">
          実データ行の登録（content_bundle → promote）は別ルートです。この画面では扱いません。
        </p>
      </div>

      <section class="space-y-4">
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700" htmlFor="layout-select">
            レイアウト（Apply 済み配置）
          </label>
          {layouts.length === 0 ? (
            <p class="text-sm text-gray-400">
              利用可能なレイアウトがありません。
              <a href="/admin/ui-builder" class="link ml-1">UIビルダー</a>
              で部品を配置して Apply してください。
            </p>
          ) : (
            <select
              id="layout-select"
              class="w-full rounded border border-gray-300 p-2 text-sm"
              value={selectedLayoutId}
              onChange={(e) => setSelectedLayoutId((e.target as HTMLSelectElement).value)}
            >
              <option value="">-- レイアウトを選択 --</option>
              {layouts.map((l) => (
                <option key={l.layoutId} value={l.layoutId}>
                  {l.layoutKey} ({l.routeKey})
                </option>
              ))}
            </select>
          )}
        </div>

        <div class="flex flex-wrap gap-2">
          <button
            class="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            disabled={!selectedLayoutId || preview.status === "loading"}
            onClick={() => runPreview()}
          >
            {preview.status === "loading" ? "取得中..." : "投影プレビュー"}
          </button>
        </div>
        <p class="text-xs text-gray-500">
          選択したレイアウトの routeKey に一致する draft manifest から initialDataRows を読み込みます。
          サンプル行が未入力の場合は配置のみ表示されます。
        </p>
      </section>

      {preview.status === "error" && (
        <div class="rounded-lg border border-red-200 bg-red-50 p-4">
          <p class="font-semibold text-red-700">プレビューエラー</p>
          {preview.code && (
            <p class="mt-1 font-mono text-xs text-red-500">コード: {preview.code}</p>
          )}
          <p class="mt-1 text-sm text-red-600">{preview.message}</p>
          {(preview.code === "LAYOUT_NODES_NOT_FOUND" ||
            preview.code === "LAYOUT_HAS_NO_NODES") && (
            <p class="mt-2 text-xs text-red-700">
              <a href="/admin/ui-builder" class="link">UIビルダー</a>
              で部品を配置し、適用 を完了してください。
            </p>
          )}
        </div>
      )}

      {preview.status === "success" && (() => {
        const sortedNodes = preview.layoutNodes
          .slice()
          .sort((a, b) => a.orderIndex - b.orderIndex);
        const emission = draftPreviewResultToEmission({
          success: true,
          layoutId: preview.layoutId,
          previewMode: preview.previewMode,
          routeKey: preview.routeKey,
          packageId: preview.packageId,
          layoutNodes: sortedNodes,
          rootLayoutClassRefs: preview.rootLayoutClassRefs,
          initialDataRows: preview.initialDataRows,
        });
        const specs = emission
          ? renderEmission(emission, defaultComponentRegistry)
          : [];
        const sampleRow = preview.initialDataRows[0];
        const showTopologyIntent = preview.previewMode === "layout_and_topology_intent" && sampleRow;

        return (
          <section class="space-y-4">
            <div class="rounded border border-blue-100 bg-blue-50 px-3 py-2 text-xs font-mono space-y-0.5">
              <div>
                <span class="text-blue-900">mode: {preview.previewMode}</span>
                {" · "}
                <span>layout: {preview.layoutId}</span>
              </div>
              {preview.routeKey && <div>routeKey: {preview.routeKey}</div>}
              {preview.packageId && <div>packageId: {preview.packageId}</div>}
              {selectedLayout && <div>layoutKey: {selectedLayout.layoutKey}</div>}
              {preview.manifestId && (
                <div>
                  manifest: {preview.manifestId} ({preview.manifestStatus})
                </div>
              )}
              {preview.topologySystemName && (
                <div>topologySystemName: {preview.topologySystemName}</div>
              )}
            </div>

            {!preview.manifestId && preview.routeKey && (
              <div class="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                routeKey「{preview.routeKey}」に一致する draft manifest が見つかりません。
                <a href="/admin/contents" class="link ml-1">Contents</a>
                で Step 1〜3 を完了してください。
              </div>
            )}

            {preview.manifestId && preview.initialDataRows.length === 0 && (
              <div class="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                manifest は見つかりましたが initialDataRows が空です（配置のみ表示）。
                <a href="/admin/contents" class="link ml-1">Contents Step 3</a>
                のデータ入力でサンプル行を追加できます。
              </div>
            )}

            {sortedNodes.length === 0 ? (
              <div class="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                このレイアウトにはノードが定義されていません。
                <a href="/admin/ui-builder" class="link ml-1">UIビルダー</a>
                で部品を配置して Apply してください。
              </div>
            ) : (
              <>
                <div
                  data-projection-island="main_projection_island"
                  data-projection-surface="demo"
                >
                  <LayoutProjectionTree
                    specs={specs}
                    layoutId={preview.layoutId}
                    rootLayoutClassRefs={preview.rootLayoutClassRefs}
                  />
                </div>
                <p class="text-xs text-gray-500">
                  公開前プレビュー（renderEmission 経路・本番と同型）。
                </p>

                {showTopologyIntent && sampleRow && (
                  <div class="space-y-2">
                    <h3 class="text-sm font-semibold text-gray-800">
                      サンプルデータ投影（initialDataRows 先頭行 → スロット）
                    </h3>
                    {sortedNodes.map((node, i) => {
                      const fields = renderTopologyIntentInSlot(
                        sampleRow,
                        node.slotKey,
                        i,
                        sortedNodes.length,
                      );
                      return (
                        <div
                          key={node.nodeId ?? node.slotKey ?? `node-${i}`}
                          class="rounded-lg border border-gray-200 bg-white"
                        >
                          <div class="border-b border-gray-100 bg-gray-50 px-3 py-1.5 text-xs font-mono text-blue-600">
                            [{i}] {node.nodeId ?? "—"} — slot: {node.slotKey ?? "(unnamed)"}
                          </div>
                          <div class="px-3 py-2">
                            {fields.length === 0 ? (
                              <p class="text-xs italic text-gray-400">
                                (このスロットにマップされたフィールドなし)
                              </p>
                            ) : (
                              <dl class="space-y-1 text-sm">
                                {fields.map(({ key, value }) => (
                                  <div key={key} class="flex gap-2">
                                    <dt class="shrink-0 font-medium text-gray-600">{key}:</dt>
                                    <dd class="break-all text-gray-800">{value}</dd>
                                  </div>
                                ))}
                              </dl>
                            )}
                          </div>
                        </div>
                      );
                    })}

                    {preview.initialDataRows.length > 1 && (
                      <div class="rounded border border-gray-200 bg-white p-3">
                        <h4 class="m-0 text-xs font-semibold text-gray-700">
                          initialDataRows 全行（{preview.initialDataRows.length} 件）
                        </h4>
                        <table class="mt-2 w-full text-xs">
                          <tbody>
                            {preview.initialDataRows.map((row, ri) => (
                              <tr key={ri} class="border-t border-gray-100">
                                {Object.entries(row).map(([k, v]) => (
                                  <td key={k} class="px-2 py-1">
                                    <span class="text-gray-500">{k}:</span> {v}
                                  </td>
                                ))}
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                )}
              </>
            )}
          </section>
        );
      })()}
    </div>
  );
}
