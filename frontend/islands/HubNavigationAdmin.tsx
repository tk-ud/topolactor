/** @jsxImportSource preact */
import { useEffect, useMemo, useState } from "preact/hooks";
import { Fragment } from "preact";
import { JSX } from "preact";
import {
  listHubNavigationManifests,
  getHubRelationsByManifest,
  createHubRelation,
  updateHubRelation,
  deprecateHubRelation,
  reorderHubRelations,
  listContentHubs,
  type HubNavigationManifestItem,
  type HubNavigationHubRelationItem,
  type HubNavigationLifecycleResult,
  type ContentBundleListItem,
} from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_HUB_NAVIGATION_GUIDE } from "../content/adminGuides.ts";
import { UX_STATUS_LABELS, UX_HUB_NAV_DESTINATION_LABEL } from "../content/adminUxTerms.ts";
import {
  hubDestinationOptionLabel,
  hubDestinationPickerOptions,
  hubNavigationErrorFriendlyText,
  hubNavigationSuccessFriendlyText,
  type HubNavigationLifecycleAction,
} from "../lib/hubNavigationPicker.ts";
import { hubNavigationManifestVisibleLabel } from "../lib/manifestTopologyExtensions.ts";
import { useConfirm } from "../hooks/useConfirm.tsx";

type PanelError = { code?: string; message: string };

type EditingState =
  | { mode: "none" }
  | { mode: "create" }
  | { mode: "edit"; hubRelationId: string; relatedHubId: string };

export default function HubNavigationAdmin(): JSX.Element {
  const [manifests, setManifests] = useState<HubNavigationManifestItem[]>([]);
  const [hubs, setHubs] = useState<ContentBundleListItem[]>([]);
  const [selectedManifestId, setSelectedManifestId] = useState("");
  const [hubRelations, setHubRelations] = useState<HubNavigationHubRelationItem[]>([]);
  const [editing, setEditing] = useState<EditingState>({ mode: "none" });
  const [draftRelatedHubId, setDraftRelatedHubId] = useState("");
  const [draftSequencePosition, setDraftSequencePosition] = useState(1);
  const [result, setResult] = useState<HubNavigationLifecycleResult | null>(null);
  const [resultAction, setResultAction] = useState<HubNavigationLifecycleAction | null>(null);
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [loading, setLoading] = useState(false);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const { confirm, ConfirmDialogHost } = useConfirm();

  const destinationHubOptions = useMemo(
    () => hubDestinationPickerOptions(hubs),
    [hubs],
  );

  const loadManifests = async () => {
    try {
      const [m, h] = await Promise.all([listHubNavigationManifests(), listContentHubs()]);
      if (m === null || h === null) {
        setBackendUnavailable(true);
        return;
      }
      setManifests(m);
      setHubs(h);
    } catch (e) {
      console.error("HUB_NAVIGATION_LOAD_FAILED", e);
      setErrors([{
        message: e instanceof Error ? e.message : "ナビ設定の読み込みに失敗しました。",
      }]);
    }
  };

  const loadHubRelations = async (manifestId: string) => {
    if (!manifestId) { setHubRelations([]); return; }
    try {
      const items = await getHubRelationsByManifest(manifestId);
      if (items === null) {
        setBackendUnavailable(true);
        setHubRelations([]);
        return;
      }
      setHubRelations(items);
    } catch (e) {
      console.error("HUB_RELATIONS_LOAD_FAILED", e);
      setErrors([{
        message: e instanceof Error ? e.message : "ナビ遷移の読み込みに失敗しました。",
      }]);
      setHubRelations([]);
    }
  };

  useEffect(() => { loadManifests(); }, []);

  useEffect(() => { loadHubRelations(selectedManifestId); }, [selectedManifestId]);

  const handleSelectManifest = (id: string) => {
    setSelectedManifestId(id);
    setEditing({ mode: "none" });
    setResult(null);
    setResultAction(null);
    setErrors([]);
    const manifest = manifests.find((m) => m.topologyManifestId === id);
    if (manifest && !manifest.hasHubRelations) {
      setEditing({ mode: "create" });
      setDraftSequencePosition(1);
      setDraftRelatedHubId("");
    }
  };

  const handleCreate = async () => {
    if (!selectedManifestId || !draftRelatedHubId) {
      setErrors([{ message: "設定と遷移先の画面を選択してください。" }]);
      return;
    }
    if (!(await confirm("ナビ遷移を登録します。よろしいですか？"))) {
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      const res = await createHubRelation(selectedManifestId, draftRelatedHubId, draftSequencePosition);
      setResult(res);
      setResultAction("create");
      if (res.ok) {
        setEditing({ mode: "none" });
        await loadHubRelations(selectedManifestId);
        await loadManifests();
      } else {
        setErrors([{ code: res.errorCode, message: res.message }]);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleUpdate = async () => {
    if (editing.mode !== "edit") return;
    if (!(await confirm("ナビ遷移を更新します。よろしいですか？"))) {
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      const res = await updateHubRelation(editing.hubRelationId, draftRelatedHubId);
      setResult(res);
      setResultAction("update");
      if (res.ok) {
        setEditing({ mode: "none" });
        await loadHubRelations(selectedManifestId);
      } else {
        setErrors([{ code: res.errorCode, message: res.message }]);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleDeprecate = async (hubRelationId: string) => {
    if (!(await confirm("ナビ遷移を削除（無効化）します。よろしいですか？", {
      variant: "danger",
      confirmLabel: "無効化する",
    }))) {
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      const res = await deprecateHubRelation(hubRelationId);
      setResult(res);
      setResultAction("deprecate");
      if (res.ok) {
        await loadHubRelations(selectedManifestId);
        await loadManifests();
      } else {
        setErrors([{ code: res.errorCode, message: res.message }]);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleReorder = async (targetId: string, direction: "up" | "down") => {
    const active = hubRelations.filter((hr) => hr.status === "active")
      .sort((a, b) => a.sequencePosition - b.sequencePosition);
    const idx = active.findIndex((hr) => hr.hubRelationId === targetId);
    const swapIdx = direction === "up" ? idx - 1 : idx + 1;
    if (idx < 0 || swapIdx < 0 || swapIdx >= active.length) return;
    setLoading(true);
    setErrors([]);
    try {
      const a = active[idx];
      const b = active[swapIdx];
      await reorderHubRelations(selectedManifestId, [
        { hubRelationId: a.hubRelationId, newSequencePosition: b.sequencePosition },
        { hubRelationId: b.hubRelationId, newSequencePosition: a.sequencePosition },
      ]);
      await loadHubRelations(selectedManifestId);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const startEdit = (hr: HubNavigationHubRelationItem) => {
    setEditing({ mode: "edit", hubRelationId: hr.hubRelationId, relatedHubId: hr.relatedHubId });
    setDraftRelatedHubId(hr.relatedHubId);
    setResult(null);
    setResultAction(null);
    setErrors([]);
  };

  const selectedManifest = manifests.find((m) => m.topologyManifestId === selectedManifestId);

  if (backendUnavailable) {
    return (
      <div class="alert-warning p-4 rounded">
        サーバーへの接続が確立されていません。環境の設定を確認し、
        <a href="/super_auth" class="link ml-1">管理ログイン</a> してから再度お試しください。
      </div>
    );
  }

  return (
    <div class="space-y-6">
      <AdminHowTo
        title={ADMIN_HUB_NAVIGATION_GUIDE.title}
        steps={ADMIN_HUB_NAVIGATION_GUIDE.howToSteps}
        prerequisites={ADMIN_HUB_NAVIGATION_GUIDE.prerequisites}
      />

      {/* Manifest selector */}
      <section class="rounded-lg border border-gray-200 bg-white p-4">
        <h2 class="mb-3 text-sm font-semibold text-gray-800">1. 設定選択</h2>
        {manifests.length === 0
          ? <p class="text-sm text-gray-500">画面がまだありません。先にコンテンツ管理で画面の内容を定義し、ページ管理で画面群に登録してください。</p>
          : (
            <select
              class="input-base w-full max-w-lg"
              value={selectedManifestId}
              onChange={(e) => handleSelectManifest((e.target as HTMLSelectElement).value)}
            >
              <option value="">— 設定を選択 —</option>
              {manifests.map((m) => (
                <option key={m.topologyManifestId} value={m.topologyManifestId}>
                  {hubNavigationManifestVisibleLabel(m)}
                  {m.hasHubRelations ? ` (${m.hubRelationCount} 件)` : " — 未登録"}
                </option>
              ))}
            </select>
          )}
        {selectedManifest && (
          <details class="mt-1">
            <summary class="cursor-pointer text-xs text-gray-400 hover:text-gray-600">技術情報</summary>
            <dl class="mt-0.5 grid grid-cols-[auto_1fr] gap-x-2 font-mono text-xs text-gray-500">
              <dt>manifest_key</dt>
              <dd>{selectedManifest.manifestKey}</dd>
              <dt>topology_manifest_id</dt>
              <dd>{selectedManifest.topologyManifestId}</dd>
            </dl>
          </details>
        )}
      </section>

      {selectedManifestId && (
        <Fragment>
          {/* Current hub_relations */}
          <section class="rounded-lg border border-gray-200 bg-white p-4">
            <div class="mb-3 flex items-center justify-between">
              <h2 class="text-sm font-semibold text-gray-800">
                2. ナビ順序一覧 — {selectedManifest ? hubNavigationManifestVisibleLabel(selectedManifest) : ""}
              </h2>
              {editing.mode === "none" && (
                <button
                  class="btn-secondary text-xs"
                  onClick={() => {
                    setEditing({ mode: "create" });
                    setDraftRelatedHubId("");
                    setDraftSequencePosition((hubRelations.filter(hr => hr.status === "active").length) + 1);
                    setResult(null);
                    setResultAction(null);
                    setErrors([]);
                  }}
                >
                  + 追加
                </button>
              )}
            </div>

            {hubRelations.filter((hr) => hr.status === "active").length === 0
              ? (
                <p class="text-sm text-gray-500">
                  ナビ遷移がまだ設定されていません。「＋追加」から設定してください。
                </p>
              )
              : (
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b text-left text-gray-500">
                      <th class="px-2 py-1">順序</th>
                      <th class="px-2 py-1">遷移先</th>
                      <th class="px-2 py-1">状態</th>
                      <th class="px-2 py-1" />
                    </tr>
                  </thead>
                  <tbody>
                    {hubRelations
                      .filter((hr) => hr.status === "active")
                      .sort((a, b) => a.sequencePosition - b.sequencePosition)
                      .map((hr, i, arr) => (
                        <tr key={hr.hubRelationId} class="border-b hover:bg-gray-50">
                          <td class="px-2 py-1 font-mono">{hr.sequencePosition}</td>
                          <td class="px-2 py-1">{hr.relatedHubLabel}</td>
                          <td class="px-2 py-1">
                            <span class="rounded bg-green-100 px-1 text-green-800">{UX_STATUS_LABELS["active"] ?? "active"}</span>
                          </td>
                          <td class="px-2 py-1 space-x-1">
                            <button
                              class="btn-secondary text-xs"
                              onClick={() => handleReorder(hr.hubRelationId, "up")}
                              disabled={loading || i === 0}
                              title="上へ"
                            >▲</button>
                            <button
                              class="btn-secondary text-xs"
                              onClick={() => handleReorder(hr.hubRelationId, "down")}
                              disabled={loading || i === arr.length - 1}
                              title="下へ"
                            >▼</button>
                            <button
                              class="btn-secondary text-xs"
                              onClick={() => startEdit(hr)}
                            >
                              編集
                            </button>
                            <button
                              class="btn-danger text-xs"
                              onClick={() => handleDeprecate(hr.hubRelationId)}
                              disabled={loading}
                            >
                              削除
                            </button>
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              )}
          </section>

          {/* Create / Edit form */}
          {(editing.mode === "create" || editing.mode === "edit") && (
            <section class="rounded-lg border border-blue-200 bg-blue-50 p-4">
              <h2 class="mb-3 text-sm font-semibold text-blue-900">
                {editing.mode === "create" ? "ナビ遷移を追加" : "ナビ遷移を編集"}
              </h2>

              <div class="space-y-3">
                <div>
                  <label class="mb-1 block text-xs font-medium text-gray-700">
                    {UX_HUB_NAV_DESTINATION_LABEL}
                  </label>
                  <select
                    class="input-base w-full max-w-md"
                    value={draftRelatedHubId}
                    onChange={(e) => setDraftRelatedHubId((e.target as HTMLSelectElement).value)}
                  >
                    <option value="">— 画面を選択 —</option>
                    {destinationHubOptions.map((h) => (
                      <option key={h.id} value={h.id}>
                        {hubDestinationOptionLabel(h)}
                      </option>
                    ))}
                  </select>
                  {draftRelatedHubId && (
                    <details class="mt-1">
                      <summary class="cursor-pointer text-xs text-gray-400 hover:text-gray-600">技術情報</summary>
                      <code class="block mt-0.5 font-mono text-xs text-gray-500">{draftRelatedHubId}</code>
                    </details>
                  )}
                </div>

                {editing.mode === "create" && (
                  <p class="text-xs text-gray-500">
                    順序は自動で末尾（{draftSequencePosition} 番目）に追加されます。追加後に▲▼で変更できます。
                  </p>
                )}

                {editing.mode === "create" && (
                  <details class="rounded border border-orange-300 bg-orange-50 p-2">
                    <summary class="cursor-pointer text-xs font-bold text-orange-800">
                      上級者向け設定 — 順序番号を直接指定
                    </summary>
                    <div class="mt-2">
                      <label class="mb-1 block text-xs font-medium text-gray-700">
                        順序番号（小さいほど先 — 通常は自動設定）
                      </label>
                      <input
                        type="number"
                        class="input-base w-32"
                        min={1}
                        value={draftSequencePosition}
                        onInput={(e) =>
                          setDraftSequencePosition(parseInt((e.target as HTMLInputElement).value, 10) || 1)}
                      />
                    </div>
                  </details>
                )}

                <div class="flex gap-2">
                  <button
                    class="btn-primary"
                    onClick={editing.mode === "create" ? handleCreate : handleUpdate}
                    disabled={loading || !draftRelatedHubId}
                  >
                    {loading ? "処理中…" : editing.mode === "create" ? "登録" : "更新"}
                  </button>
                  <button
                    class="btn-secondary"
                    onClick={() => {
                      setEditing({ mode: "none" });
                      setErrors([]);
                      setResult(null);
                      setResultAction(null);
                    }}
                  >
                    キャンセル
                  </button>
                </div>
              </div>
            </section>
          )}
        </Fragment>
      )}

      {/* Result / Error */}
      {result && result.ok && (
        <div class="alert-success rounded p-3 text-sm">
          ✓ {resultAction ? hubNavigationSuccessFriendlyText(resultAction) : result.message}
          {(result.hubRelationId || resultAction) && (
            <details class="mt-1">
              <summary class="cursor-pointer text-xs text-green-700 hover:text-green-900">技術情報</summary>
              <dl class="mt-0.5 grid grid-cols-[auto_1fr] gap-x-2 font-mono text-xs text-green-800">
                {result.hubRelationId && (
                  <>
                    <dt>hub_relation_id</dt>
                    <dd>{result.hubRelationId}</dd>
                  </>
                )}
                <dt>message</dt>
                <dd>{result.message}</dd>
              </dl>
            </details>
          )}
        </div>
      )}
      {errors.length > 0 && (
        <div>
          <ValidationErrorPanel
            errors={errors.map((e) => ({ code: e.code, message: hubNavigationErrorFriendlyText(e) }))}
          />
          <details class="mt-1">
            <summary class="cursor-pointer text-xs text-gray-400 hover:text-gray-600">技術情報（開発者向け）</summary>
            <ul class="mt-0.5 list-inside list-disc font-mono text-xs text-gray-500">
              {errors.map((e, i) => (
                <li key={i}>{e.code ? `[${e.code}] ${e.message}` : e.message}</li>
              ))}
            </ul>
          </details>
        </div>
      )}

      <AdminHelpPanel {...ADMIN_HUB_NAVIGATION_GUIDE} />
      <ConfirmDialogHost />
    </div>
  );
}
