import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  fetchDraftPreviewDrafts,
  fetchDraftPreviewLayouts,
  fetchDraftPreview,
  type DraftPreviewDraft,
  type DraftPreviewLayout,
  type DraftPreviewLayoutNode,
} from "../api/draftPreview.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type PreviewState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "error"; message: string }
  | {
      status: "success";
      layoutId: string;
      draftId: string;
      layoutNodes: DraftPreviewLayoutNode[];
      draftEntityJson: Record<string, unknown> | undefined;
      draftStatus: string | undefined;
    };

/**
 * /demo — draft preview surface.
 * Fetches admin-authored layouts and draft content entities on mount.
 * When both are selected, fetches preview: layout tensor nodes + draft entity JSON.
 * Renders slot-ordered layout nodes reflecting admin-authored tensor ordering.
 */
export default function DraftPreviewShell(): JSX.Element {
  const [layouts, setLayouts] = useState<DraftPreviewLayout[]>([]);
  const [drafts, setDrafts] = useState<DraftPreviewDraft[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [initLoading, setInitLoading] = useState(true);
  const [selectedLayoutId, setSelectedLayoutId] = useState("");
  const [selectedDraftId, setSelectedDraftId] = useState("");
  const [preview, setPreview] = useState<PreviewState>({ status: "idle" });

  useEffect(() => {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
    (async () => {
      const [layoutsRes, draftsRes] = await Promise.all([
        fetchDraftPreviewLayouts(token),
        fetchDraftPreviewDrafts(token),
      ]);

      if (!layoutsRes.success) {
        const msg =
          layoutsRes.errors?.[0]?.message ??
          layoutsRes.errors?.[0]?.code ??
          "レイアウト一覧の取得に失敗しました";
        setLoadError(msg);
        setInitLoading(false);
        return;
      }
      if (!draftsRes.success) {
        const msg =
          draftsRes.errors?.[0]?.message ??
          draftsRes.errors?.[0]?.code ??
          "ドラフト一覧の取得に失敗しました";
        setLoadError(msg);
        setInitLoading(false);
        return;
      }

      setLayouts(layoutsRes.layouts ?? []);
      setDrafts(draftsRes.drafts ?? []);
      setInitLoading(false);
    })();
  }, []);

  async function handlePreview() {
    if (!selectedLayoutId || !selectedDraftId) return;
    setPreview({ status: "loading" });
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
    const result = await fetchDraftPreview(selectedLayoutId, selectedDraftId, token);

    if (!result.success) {
      const msg =
        result.errors?.[0]?.message ??
        result.errors?.[0]?.code ??
        "プレビューの取得に失敗しました";
      setPreview({ status: "error", message: msg });
      return;
    }

    setPreview({
      status: "success",
      layoutId: result.layoutId ?? selectedLayoutId,
      draftId: result.draftId ?? selectedDraftId,
      layoutNodes: result.layoutNodes ?? [],
      draftEntityJson: result.draftEntityJson,
      draftStatus: result.draftStatus,
    });
  }

  if (initLoading) {
    return (
      <div class="py-8 text-center text-gray-400" aria-busy="true" aria-live="polite">
        レイアウト・ドラフト一覧を取得中...
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

  return (
    <div class="space-y-6">
      <section class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1" htmlFor="layout-select">
            レイアウト
          </label>
          {layouts.length === 0 ? (
            <p class="text-sm text-gray-400">
              利用可能なレイアウトがありません。
              <a href="/admin" class="link ml-1">管理画面</a>でレイアウトを作成してください。
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

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1" htmlFor="draft-select">
            ドラフトコンテンツ
          </label>
          {drafts.length === 0 ? (
            <p class="text-sm text-gray-400">
              ドラフトがありません。
              <a href="/admin" class="link ml-1">管理画面</a>でドラフトを作成してください。
            </p>
          ) : (
            <select
              id="draft-select"
              class="w-full rounded border border-gray-300 p-2 text-sm"
              value={selectedDraftId}
              onChange={(e) => setSelectedDraftId((e.target as HTMLSelectElement).value)}
            >
              <option value="">-- ドラフトを選択 --</option>
              {drafts.map((d) => (
                <option key={d.draftId} value={d.draftId}>
                  {d.label}
                </option>
              ))}
            </select>
          )}
        </div>

        <button
          class="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          disabled={!selectedLayoutId || !selectedDraftId || preview.status === "loading"}
          onClick={handlePreview}
        >
          {preview.status === "loading" ? "取得中..." : "プレビュー"}
        </button>
      </section>

      {preview.status === "error" && (
        <div class="rounded-lg border border-red-200 bg-red-50 p-4">
          <p class="font-semibold text-red-700">プレビューエラー</p>
          <p class="mt-1 text-sm text-red-600">{preview.message}</p>
        </div>
      )}

      {preview.status === "success" && (
        <section class="space-y-4">
          <div class="rounded-lg border border-blue-100 bg-blue-50 p-3 text-xs font-mono">
            <p>layout: {preview.layoutId}</p>
            <p>draft: {preview.draftId}</p>
            {preview.draftStatus && <p>status: {preview.draftStatus}</p>}
          </div>

          <div>
            <h2 class="mb-2 text-sm font-semibold text-gray-700">
              レイアウトスロット ({preview.layoutNodes.length}件)
            </h2>
            {preview.layoutNodes.length === 0 ? (
              <p class="text-sm text-gray-400">スロットなし</p>
            ) : (
              <div class="space-y-2">
                {preview.layoutNodes
                  .slice()
                  .sort((a, b) => a.orderIndex - b.orderIndex)
                  .map((node, i) => (
                    <div
                      key={node.slotKey ?? `node-${i}`}
                      class="rounded border border-gray-200 bg-white p-3"
                    >
                      <p class="text-xs font-mono text-blue-500">
                        slot: {node.slotKey ?? "(unnamed)"} — order: {node.orderIndex}
                      </p>
                      {node.layoutPatchJson && (
                        <p class="mt-1 text-xs text-gray-500 font-mono truncate">
                          patch: {node.layoutPatchJson}
                        </p>
                      )}
                    </div>
                  ))}
              </div>
            )}
          </div>

          {preview.draftEntityJson && (
            <div>
              <h2 class="mb-2 text-sm font-semibold text-gray-700">ドラフトエンティティ</h2>
              <pre class="rounded border border-gray-200 bg-gray-50 p-3 text-xs font-mono overflow-auto max-h-64 whitespace-pre-wrap">
                {JSON.stringify(preview.draftEntityJson, null, 2)}
              </pre>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
