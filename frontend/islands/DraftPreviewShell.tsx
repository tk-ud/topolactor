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
 * Maps entity JSON fields into a single slot frame.
 *
 * Strategy:
 *   1. If a field name matches the slotKey (case-insensitive), show it alone.
 *   2. Otherwise distribute all fields across slots by position so every slot
 *      receives a distinct subset of the entity data.
 *   This simulates component projection: ordered slots drive content placement.
 */
function renderEntityInSlot(
  entityJson: Record<string, unknown>,
  slotKey: string | undefined,
  slotIndex: number,
  totalSlots: number,
): JSX.Element {
  const entries = Object.entries(entityJson).filter(
    ([, v]) => v !== null && v !== undefined,
  );

  const slotName = (slotKey ?? "").toLowerCase();
  const directMatch = entries.find(([k]) => k.toLowerCase() === slotName);
  if (directMatch) {
    return (
      <dl class="space-y-1 text-sm">
        <div class="flex gap-2">
          <dt class="font-medium text-gray-600 shrink-0">{directMatch[0]}:</dt>
          <dd class="break-all text-gray-800">{String(directMatch[1])}</dd>
        </div>
      </dl>
    );
  }

  const fieldsPerSlot = Math.max(1, Math.ceil(entries.length / Math.max(totalSlots, 1)));
  const start = slotIndex * fieldsPerSlot;
  const slotFields = entries.slice(start, start + fieldsPerSlot);

  if (slotFields.length === 0) {
    return <p class="text-xs italic text-gray-400">(このスロットにマップされたフィールドなし)</p>;
  }

  return (
    <dl class="space-y-1 text-sm">
      {slotFields.map(([k, v]) => (
        <div key={k} class="flex gap-2">
          <dt class="shrink-0 font-medium text-gray-600">{k}:</dt>
          <dd class="break-all text-gray-800">
            {typeof v === "object" ? JSON.stringify(v) : String(v)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * /demo — draft preview surface.
 * Fetches admin-authored layouts and draft content entities on mount.
 * When both are selected, fetches preview and renders draft content projected
 * through layout slots ordered by tensor orderIndex.
 * Each slot frame contains its slice of the draft entity data — not a raw JSON blob.
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
        setLoadError(
          layoutsRes.errors?.[0]?.message ??
            layoutsRes.errors?.[0]?.code ??
            "レイアウト一覧の取得に失敗しました",
        );
        setInitLoading(false);
        return;
      }
      if (!draftsRes.success) {
        setLoadError(
          draftsRes.errors?.[0]?.message ??
            draftsRes.errors?.[0]?.code ??
            "ドラフト一覧の取得に失敗しました",
        );
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
      setPreview({
        status: "error",
        message:
          result.errors?.[0]?.message ??
          result.errors?.[0]?.code ??
          "プレビューの取得に失敗しました",
      });
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
      {/* Selectors */}
      <section class="space-y-4">
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700" htmlFor="layout-select">
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
          <label class="mb-1 block text-sm font-medium text-gray-700" htmlFor="draft-select">
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

      {/* Error */}
      {preview.status === "error" && (
        <div class="rounded-lg border border-red-200 bg-red-50 p-4">
          <p class="font-semibold text-red-700">プレビューエラー</p>
          <p class="mt-1 text-sm text-red-600">{preview.message}</p>
        </div>
      )}

      {/* Projection: draft content flowed into ordered layout slots */}
      {preview.status === "success" && (() => {
        const sortedNodes = preview.layoutNodes
          .slice()
          .sort((a, b) => a.orderIndex - b.orderIndex);
        const entityJson = preview.draftEntityJson ?? {};

        return (
          <section class="space-y-3">
            <div class="rounded border border-blue-100 bg-blue-50 px-3 py-2 text-xs font-mono">
              <span>layout: {preview.layoutId}</span>
              {"  "}
              <span>draft: {preview.draftId}</span>
              {preview.draftStatus && <span>{"  "}status: {preview.draftStatus}</span>}
            </div>

            <p class="text-xs text-gray-400">
              ドラフトコンテンツを tensor orderIndex 順のスロットに投影しています ({sortedNodes.length} スロット)
            </p>

            {sortedNodes.length === 0 ? (
              <p class="text-sm text-gray-400">スロットなし</p>
            ) : (
              <div class="space-y-2">
                {sortedNodes.map((node, i) => (
                  <div
                    key={node.slotKey ?? `node-${i}`}
                    class="rounded-lg border border-gray-200 bg-white"
                  >
                    {/* Slot header — driven by orderIndex */}
                    <div class="border-b border-gray-100 bg-gray-50 px-3 py-1.5 text-xs font-mono text-blue-600">
                      [{i}] slot: {node.slotKey ?? "(unnamed)"} — order: {node.orderIndex}
                      {node.layoutPatchJson && (
                        <span class="ml-2 text-gray-400">patch: {node.layoutPatchJson}</span>
                      )}
                    </div>
                    {/* Draft content projected into this slot */}
                    <div class="px-3 py-2">
                      {renderEntityInSlot(entityJson, node.slotKey, i, sortedNodes.length)}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>
        );
      })()}
    </div>
  );
}
