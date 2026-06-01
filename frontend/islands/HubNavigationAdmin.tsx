import { useEffect, useState } from "preact/hooks";
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
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [loading, setLoading] = useState(false);
  const [backendUnavailable, setBackendUnavailable] = useState(false);

  const loadManifests = async () => {
    const [m, h] = await Promise.all([listHubNavigationManifests(), listContentHubs()]);
    if (m === null || h === null) { setBackendUnavailable(true); return; }
    setManifests(m);
    setHubs(h);
  };

  const loadHubRelations = async (manifestId: string) => {
    if (!manifestId) { setHubRelations([]); return; }
    const items = await getHubRelationsByManifest(manifestId);
    setHubRelations(items ?? []);
  };

  useEffect(() => { loadManifests(); }, []);

  useEffect(() => { loadHubRelations(selectedManifestId); }, [selectedManifestId]);

  const handleSelectManifest = (id: string) => {
    setSelectedManifestId(id);
    setEditing({ mode: "none" });
    setResult(null);
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
      setErrors([{ message: "マニフェストと遷移先 hub を選択してください。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      const res = await createHubRelation(selectedManifestId, draftRelatedHubId, draftSequencePosition);
      setResult(res);
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
    setLoading(true);
    setErrors([]);
    try {
      const res = await updateHubRelation(editing.hubRelationId, draftRelatedHubId);
      setResult(res);
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
    setLoading(true);
    setErrors([]);
    try {
      const res = await deprecateHubRelation(hubRelationId);
      setResult(res);
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
    setErrors([]);
  };

  const selectedManifest = manifests.find((m) => m.topologyManifestId === selectedManifestId);

  if (backendUnavailable) {
    return (
      <div class="alert-warning p-4 rounded">
        バックエンドが接続されていません。DEMO_BACKEND_URL / DATABASE_URL を確認してください。
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
        <h2 class="mb-3 text-sm font-semibold text-gray-800">1. マニフェスト選択</h2>
        {manifests.length === 0
          ? <p class="text-sm text-gray-500">topology_manifests がありません。先に /admin/manifests でマニフェストを登録してください。</p>
          : (
            <select
              class="input-base w-full max-w-lg"
              value={selectedManifestId}
              onChange={(e) => handleSelectManifest((e.target as HTMLSelectElement).value)}
            >
              <option value="">— マニフェストを選択 —</option>
              {manifests.map((m) => (
                <option key={m.topologyManifestId} value={m.topologyManifestId}>
                  {m.manifestKey}
                  {m.hasHubRelations ? ` (${m.hubRelationCount} 件)` : " — 未登録"}
                </option>
              ))}
            </select>
          )}
      </section>

      {selectedManifestId && (
        <>
          {/* Current hub_relations */}
          <section class="rounded-lg border border-gray-200 bg-white p-4">
            <div class="mb-3 flex items-center justify-between">
              <h2 class="text-sm font-semibold text-gray-800">
                2. ナビ順序一覧 — {selectedManifest?.manifestKey}
              </h2>
              {editing.mode === "none" && (
                <button
                  class="btn-secondary text-xs"
                  onClick={() => {
                    setEditing({ mode: "create" });
                    setDraftRelatedHubId("");
                    setDraftSequencePosition((hubRelations.filter(hr => hr.status === "active").length) + 1);
                    setResult(null);
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
                  このマニフェストの hub_relation はまだありません。追加してください。
                </p>
              )
              : (
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b text-left text-gray-500">
                      <th class="px-2 py-1">順序</th>
                      <th class="px-2 py-1">遷移先 hub</th>
                      <th class="px-2 py-1">status</th>
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
                            <span class="rounded bg-green-100 px-1 text-green-800">active</span>
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
                {editing.mode === "create" ? "hub_relation を追加" : "hub_relation を編集"}
              </h2>

              <div class="space-y-3">
                <div>
                  <label class="mb-1 block text-xs font-medium text-gray-700">遷移先 hub</label>
                  <select
                    class="input-base w-full max-w-md"
                    value={draftRelatedHubId}
                    onChange={(e) => setDraftRelatedHubId((e.target as HTMLSelectElement).value)}
                  >
                    <option value="">— hub を選択 —</option>
                    {hubs.map((h) => (
                      <option key={h.id} value={h.id}>
                        {h.label} ({h.id.slice(0, 8)}…)
                      </option>
                    ))}
                  </select>
                </div>

                {editing.mode === "create" && (
                  <div>
                    <label class="mb-1 block text-xs font-medium text-gray-700">
                      sequence_position（小さいほど先）
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
                    onClick={() => { setEditing({ mode: "none" }); setErrors([]); setResult(null); }}
                  >
                    キャンセル
                  </button>
                </div>
              </div>
            </section>
          )}
        </>
      )}

      {/* Result / Error */}
      {result && result.ok && (
        <div class="alert-success rounded p-3 text-sm">
          ✓ {result.message}（hub_relation_id: {result.hubRelationId}）
        </div>
      )}
      {errors.length > 0 && <ValidationErrorPanel errors={errors} />}

      <AdminHelpPanel {...ADMIN_HUB_NAVIGATION_GUIDE} />
    </div>
  );
}
