import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  getAdminManifest,
  validateAdminManifest,
  createAdminManifestDraft,
  updateAdminManifestDraft,
  promoteAdminManifest,
  deprecateAdminManifest,
  listAdminPromotionManifests,
  getAdminPromotionManifest,
  validateAdminPromotionManifest,
  updateAdminPromotionManifestDraft,
  RUNTIME_DESTINATION_OPTIONS,
  type AdminManifestListItem,
  type AdminManifestDetail,
  type AdminManifestValidateResult,
  type AdminManifestLifecycleResult,
  type AdminPromotionManifestListItem,
  type AdminPromotionManifestDetail,
  type AdminPromotionManifestValidateResult,
  type AdminPromotionManifestUpdateInput,
} from "../api/adminApi.ts";
import {
  buildProjectionDefinitionPayload,
  emptyManifestProjectionDraft,
  extractProjectionDefinitionFromTopology,
  formatProjectionSummary,
  parseProjectionDefinitionToDraft,
  PROJECTION_OUTPUT_KIND_OPTIONS,
  type ManifestProjectionDraft,
} from "../runtime/manifestProjectionEditor.ts";
import {
  buildPromotionUpdatePayload,
  emptyPromotionManifestDraft,
  formatPromotionSummary,
  parsePromotionMetadataToDraft,
  PROMOTION_ACTIVATION_POLICY_OPTIONS,
  type PromotionManifestDraft,
  type PromotionTargetRefDraft,
} from "../runtime/promotionManifestEditor.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_MANIFESTS_GUIDE } from "../content/adminGuides.ts";

type PanelError = { code?: string; message: string };
type EditorMode = "wiring" | "promotion";

const STATUS_FILTERS = ["", "draft", "active", "deprecated"] as const;

function statusBadgeClass(status: string): string {
  switch (status) {
    case "active": return "text-emerald-700";
    case "draft": return "text-amber-700";
    case "deprecated": return "text-slate-500";
    default: return "text-muted-xs";
  }
}

export default function ManifestsAdmin(): JSX.Element {
  const [editorMode, setEditorMode] = useState<EditorMode>("wiring");
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [selectedId, setSelectedId] = useState<string>("");
  const [detail, setDetail] = useState<AdminManifestDetail | null>(null);
  const [validation, setValidation] = useState<AdminManifestValidateResult | null>(null);
  const [lifecycleResult, setLifecycleResult] = useState<AdminManifestLifecycleResult | null>(null);
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);
  const [showDraftForm, setShowDraftForm] = useState(false);

  const [draftRole, setDraftRole] = useState("admin");
  const [draftTarget, setDraftTarget] = useState("admin");
  const [draftLayer, setDraftLayer] = useState("");
  const [draftAction, setDraftAction] = useState("");
  const [draftRuntimeDestination, setDraftRuntimeDestination] = useState("admin_runtime");
  const [projectionDraft, setProjectionDraft] = useState<ManifestProjectionDraft>(
    emptyManifestProjectionDraft(),
  );

  const [promotionManifests, setPromotionManifests] = useState<AdminPromotionManifestListItem[]>([]);
  const [promotionStatusFilter, setPromotionStatusFilter] = useState<string>("");
  const [promotionSelectedId, setPromotionSelectedId] = useState<string>("");
  const [promotionDetail, setPromotionDetail] = useState<AdminPromotionManifestDetail | null>(null);
  const [promotionDraft, setPromotionDraft] = useState<PromotionManifestDraft>(emptyPromotionManifestDraft());
  const [promotionValidation, setPromotionValidation] = useState<AdminPromotionManifestValidateResult | null>(null);
  const [promotionDraftManifestId, setPromotionDraftManifestId] = useState<string>("");

  const applyProjectionDraftFromDetail = (d: AdminManifestDetail) => {
    const def = extractProjectionDefinitionFromTopology(d.topologyRawJson);
    setProjectionDraft(parseProjectionDefinitionToDraft(def));
  };

  const updateProjectionDraft = (patch: Partial<ManifestProjectionDraft>) => {
    setProjectionDraft((prev) => ({ ...prev, ...patch }));
  };

  const updatePromotionDraft = (patch: Partial<PromotionManifestDraft>) => {
    setPromotionDraft((prev) => ({ ...prev, ...patch }));
  };

  const updatePromotionTargetRef = (index: number, patch: Partial<PromotionTargetRefDraft>) => {
    setPromotionDraft((prev) => ({
      ...prev,
      targetRefs: prev.targetRefs.map((ref, i) => (i === index ? { ...ref, ...patch } : ref)),
    }));
  };

  const loadPromotionList = async () => {
    setLoading(true);
    setErrors([]);
    setBackendUnavailable(false);
    try {
      const items = await listAdminPromotionManifests(promotionStatusFilter || undefined);
      if (items === null) {
        setBackendUnavailable(true);
        setPromotionManifests([]);
        setStatus("バックエンド未設定 — promotion manifest 操作は利用できません。");
        return;
      }
      setPromotionManifests(items);
      setStatus(`${items.length} 件の promotion manifest を読み込みました。`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
      setStatus("promotion manifest 一覧の読み込みに失敗しました。");
    } finally {
      setLoading(false);
    }
  };

  const loadPromotionDetail = async (manifestId: string) => {
    setPromotionSelectedId(manifestId);
    setPromotionDraftManifestId(manifestId);
    setPromotionValidation(null);
    setErrors([]);
    setLoading(true);
    try {
      const d = await getAdminPromotionManifest(manifestId);
      if (d === null) {
        setBackendUnavailable(true);
        setPromotionDetail(null);
        return;
      }
      setPromotionDetail(d);
      setPromotionDraft(parsePromotionMetadataToDraft(d.metadata ?? undefined));
      setStatus(`promotion detail: ${manifestId}`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePromotionValidate = async () => {
    if (!promotionSelectedId) return;
    setLoading(true);
    setPromotionValidation(null);
    setErrors([]);
    try {
      const result = await validateAdminPromotionManifest(promotionSelectedId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setPromotionValidation(result);
      setStatus(result.valid ? "promotion バリデーション成功。" : "promotion バリデーションで blocking error があります。");
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePromotionSaveDraft = async () => {
    const manifestId = promotionSelectedId || promotionDraftManifestId.trim();
    if (!manifestId) {
      setErrors([{ message: "manifestId を指定してください（既存 draft の UUID）。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      const saved = await updateAdminPromotionManifestDraft(
        buildPromotionUpdatePayload(manifestId, promotionDraft) as AdminPromotionManifestUpdateInput,
      );
      setPromotionDetail(saved);
      setPromotionSelectedId(saved.manifestId);
      setPromotionDraftManifestId(saved.manifestId);
      setStatus("promotion metadata ドラフトを更新しました。");
      await loadPromotionList();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const loadList = async () => {
    setLoading(true);
    setErrors([]);
    setBackendUnavailable(false);
    try {
      const items = await listAdminManifests(statusFilter || undefined);
      if (items === null) {
        setBackendUnavailable(true);
        setManifests([]);
        setStatus("バックエンド未設定 — DATABASE_URL / DEMO_BACKEND_URL を確認してください。");
        return;
      }
      setManifests(items);
      setStatus(`${items.length} 件のマニフェストを読み込みました。`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
      setStatus("一覧の読み込みに失敗しました。");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (editorMode === "wiring") {
      loadList();
    } else {
      loadPromotionList();
    }
  }, [editorMode]);

  const loadDetail = async (manifestId: string) => {
    setSelectedId(manifestId);
    setValidation(null);
    setLifecycleResult(null);
    setErrors([]);
    setLoading(true);
    try {
      const d = await getAdminManifest(manifestId);
      if (d === null) {
        setBackendUnavailable(true);
        setDetail(null);
        return;
      }
      setDetail(d);
      setDraftRole(d.summary.dispatcherMapping?.role ?? "admin");
      setDraftTarget(d.summary.dispatcherMapping?.target ?? "admin");
      setDraftLayer(d.summary.dispatcherMapping?.layer ?? "");
      setDraftAction(d.summary.dispatcherMapping?.action ?? "");
      setDraftRuntimeDestination(d.summary.runtimeMapping?.runtimeDestination ?? "admin_runtime");
      applyProjectionDraftFromDetail(d);
      setStatus(`詳細: ${manifestId}`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    if (!selectedId) return;
    setLoading(true);
    setValidation(null);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await validateAdminManifest(selectedId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setValidation(result);
      setStatus(result.valid ? "バリデーション成功。" : "バリデーションで blocking error があります。");
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePromote = async () => {
    if (!selectedId || detail?.status !== "draft") return;
    setLoading(true);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await promoteAdminManifest(selectedId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setLifecycleResult(result);
      if (!result.ok) {
        setErrors([{ code: result.errorCode, message: result.message }]);
        setStatus("プロモートに失敗しました。");
      } else {
        setStatus("draft → active にプロモートしました。");
        await loadList();
        await loadDetail(selectedId);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleDeprecate = async () => {
    if (!selectedId || detail?.status !== "active") return;
    setLoading(true);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await deprecateAdminManifest(selectedId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setLifecycleResult(result);
      if (!result.ok) {
        setErrors([{ code: result.errorCode, message: result.message }]);
      } else {
        setStatus("active → deprecated にしました。");
        await loadList();
        await loadDetail(selectedId);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleSaveDraft = async () => {
    setLoading(true);
    setErrors([]);
    setLifecycleResult(null);
    let projectionDefinition: Record<string, unknown> | null = null;
    try {
      projectionDefinition = buildProjectionDefinitionPayload(projectionDraft);
    } catch (e) {
      setErrors([{ message: String(e) }]);
      setLoading(false);
      return;
    }
    const input = {
      role: draftRole,
      target: draftTarget,
      layer: draftLayer,
      action: draftAction,
      runtimeDestination: draftRuntimeDestination,
      projectionDefinition,
    };
    try {
      const saved = detail?.status === "draft" && selectedId
        ? await updateAdminManifestDraft(selectedId, input)
        : await createAdminManifestDraft(input);
      setDetail(saved);
      setSelectedId(saved.manifestId);
      setShowDraftForm(false);
      setStatus(detail?.status === "draft" ? "ドラフトを更新しました。" : "ドラフトを作成しました。");
      await loadList();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const promoteDisabled = !selectedId || detail?.status !== "draft" ||
    (validation !== null && validation.isBlocking);
  const deprecateDisabled = !selectedId || detail?.status !== "active";

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / マニフェスト</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo steps={ADMIN_MANIFESTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_MANIFESTS_GUIDE} />

      {backendUnavailable && (
        <p class="alert-warning mb-4 text-sm" role="status">
          バックエンド未接続 — manifest 管理操作は DB + dispatch 経由でのみ利用できます。
        </p>
      )}

      <ValidationErrorPanel errors={errors} title="manifest errors" />

      {status && <p class="mb-4 text-sm text-muted-xs">{status}</p>}

      <div class="mb-6 flex flex-wrap gap-2">
        <button
          type="button"
          class={editorMode === "wiring" ? "btn-primary" : "btn-secondary"}
          onClick={() => setEditorMode("wiring")}
        >
          Runtime wiring
        </button>
        <button
          type="button"
          class={editorMode === "promotion" ? "btn-primary" : "btn-secondary"}
          onClick={() => setEditorMode("promotion")}
        >
          Promotion metadata
        </button>
      </div>

      {editorMode === "wiring" && (
      <>
      <section class="mb-8">
        <h2 class="section-title">1. マニフェスト一覧</h2>
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <label class="text-sm">
            ステータス:
            <select
              class="ml-2 rounded border px-2 py-1"
              value={statusFilter}
              onChange={(e) => setStatusFilter((e.target as HTMLSelectElement).value)}
            >
              {STATUS_FILTERS.map((s) => (
                <option key={s || "all"} value={s}>{s || "すべて"}</option>
              ))}
            </select>
          </label>
          <button type="button" class="btn-secondary" disabled={loading} onClick={loadList}>
            再読み込み
          </button>
          <button
            type="button"
            class="btn-secondary"
            disabled={loading}
            onClick={() => {
              setShowDraftForm(true);
              setDetail(null);
              setSelectedId("");
              setProjectionDraft(emptyManifestProjectionDraft());
            }}
          >
            新規ドラフト
          </button>
        </div>

        {manifests.length === 0 ? (
          <p class="text-sm text-muted-xs">マニフェストがありません。</p>
        ) : (
          <div class="overflow-x-auto">
            <table class="w-full border-collapse text-sm">
              <thead>
                <tr class="border-b bg-slate-50 text-left">
                  {["manifest_id", "status", "role", "target", "layer", "action", "runtime_destination"].map((h) => (
                    <th key={h} class="px-2 py-1 font-semibold">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {manifests.map((m) => (
                  <tr
                    key={m.manifestId}
                    class={`cursor-pointer border-b hover:bg-slate-50 ${selectedId === m.manifestId ? "bg-blue-50" : ""}`}
                    onClick={() => loadDetail(m.manifestId)}
                  >
                    <td class="px-2 py-1"><code class="text-xs">{m.manifestId.slice(0, 8)}…</code></td>
                    <td class={`px-2 py-1 ${statusBadgeClass(m.status)}`}>{m.status}</td>
                    <td class="px-2 py-1">{m.role ?? "—"}</td>
                    <td class="px-2 py-1">{m.target ?? "—"}</td>
                    <td class="px-2 py-1">{m.layer ?? "—"}</td>
                    <td class="px-2 py-1">{m.action ?? "—"}</td>
                    <td class="px-2 py-1">{m.runtimeDestination ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {(showDraftForm || detail) && (
        <section class="mb-8">
          <h2 class="section-title">2. 詳細 / ドラフト編集</h2>

          {detail && (
            <dl class="mb-4 grid gap-2 text-sm sm:grid-cols-2">
              <div><dt class="font-semibold">manifest_id</dt><dd><code>{detail.manifestId}</code></dd></div>
              <div><dt class="font-semibold">status</dt><dd class={statusBadgeClass(detail.status)}>{detail.status}</dd></div>
              <div><dt class="font-semibold">relation_registry_id</dt><dd>{detail.relationRegistryId ?? "—"}</dd></div>
              <div><dt class="font-semibold">updated_at</dt><dd>{detail.updatedAt}</dd></div>
            </dl>
          )}

          {(showDraftForm || detail?.status === "draft") && (
            <div class="mb-4 rounded border border-slate-200 bg-slate-50 p-4">
              <h3 class="mb-2 text-sm font-semibold">dispatcher axes + runtime_destination</h3>
              <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                {([
                  ["role", draftRole, setDraftRole],
                  ["target", draftTarget, setDraftTarget],
                  ["layer", draftLayer, setDraftLayer],
                  ["action", draftAction, setDraftAction],
                ] as const).map(([label, value, setter]) => (
                  <label key={label} class="text-xs">
                    {label}
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 font-mono"
                      value={value}
                      onInput={(e) => setter((e.target as HTMLInputElement).value)}
                    />
                  </label>
                ))}
                <label class="text-xs">
                  runtime_destination
                  <select
                    class="mt-1 w-full rounded border px-2 py-1 font-mono"
                    value={draftRuntimeDestination}
                    onChange={(e) => setDraftRuntimeDestination((e.target as HTMLSelectElement).value)}
                  >
                    {RUNTIME_DESTINATION_OPTIONS.map((d) => (
                      <option key={d} value={d}>{d}</option>
                    ))}
                  </select>
                </label>
              </div>

              <details class="mt-4 rounded border border-dashed border-slate-300 bg-white p-3">
                <summary class="cursor-pointer text-xs font-semibold">
                  advanced: projection_constructor_mapping（optional）
                </summary>
                <p class="mt-2 text-xs text-muted-xs">
                  backend validate が正本です。ここでは constructorKey / outputKind / packageIds を structured 入力し、
                  fieldDefs 等は nested JSON のみ（raw topology 全面編集は debug 参照）。
                </p>
                <label class="mt-3 flex items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={projectionDraft.enabled}
                    onChange={(e) => updateProjectionDraft({ enabled: (e.target as HTMLInputElement).checked })}
                  />
                  projection_constructor_mapping を含める
                </label>
                {projectionDraft.enabled && (
                  <div class="mt-3 grid gap-2 sm:grid-cols-2">
                    <label class="text-xs">
                      constructorKey
                      <input
                        class="mt-1 w-full rounded border px-2 py-1 font-mono"
                        value={projectionDraft.constructorKey}
                        onInput={(e) => updateProjectionDraft({ constructorKey: (e.target as HTMLInputElement).value })}
                      />
                    </label>
                    <label class="text-xs">
                      outputKind
                      <select
                        class="mt-1 w-full rounded border px-2 py-1 font-mono"
                        value={projectionDraft.outputKind}
                        onChange={(e) =>
                          updateProjectionDraft({
                            outputKind: (e.target as HTMLSelectElement).value as ManifestProjectionDraft["outputKind"],
                          })}
                      >
                        {PROJECTION_OUTPUT_KIND_OPTIONS.map((k) => (
                          <option key={k} value={k}>{k}</option>
                        ))}
                      </select>
                    </label>
                    <label class="text-xs sm:col-span-2">
                      packageIds（カンマ区切り UUID）
                      <input
                        class="mt-1 w-full rounded border px-2 py-1 font-mono"
                        value={projectionDraft.packageIds}
                        onInput={(e) => updateProjectionDraft({ packageIds: (e.target as HTMLInputElement).value })}
                        placeholder="00000000-0000-0000-0000-000000000020"
                      />
                    </label>
                    <label class="text-xs sm:col-span-2">
                      componentId（component_projection 時 optional）
                      <input
                        class="mt-1 w-full rounded border px-2 py-1 font-mono"
                        value={projectionDraft.componentId}
                        onInput={(e) => updateProjectionDraft({ componentId: (e.target as HTMLInputElement).value })}
                      />
                    </label>
                    <details class="sm:col-span-2">
                      <summary class="cursor-pointer text-xs text-muted-xs">nested JSON（fieldDefs / componentDefinition / overrides）</summary>
                      <label class="mt-2 block text-xs">
                        fieldDefs JSON array
                        <textarea
                          class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                          rows={4}
                          value={projectionDraft.fieldDefsJson}
                          onInput={(e) => updateProjectionDraft({ fieldDefsJson: (e.target as HTMLTextAreaElement).value })}
                          placeholder='[{"key":"name","label":"Name","kind":"text"}]'
                        />
                      </label>
                      <label class="mt-2 block text-xs">
                        componentDefinition JSON object
                        <textarea
                          class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                          rows={3}
                          value={projectionDraft.componentDefinitionJson}
                          onInput={(e) =>
                            updateProjectionDraft({ componentDefinitionJson: (e.target as HTMLTextAreaElement).value })}
                        />
                      </label>
                      <label class="mt-2 block text-xs">
                        projectionOverrides JSON object
                        <textarea
                          class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                          rows={3}
                          value={projectionDraft.projectionOverridesJson}
                          onInput={(e) =>
                            updateProjectionDraft({ projectionOverridesJson: (e.target as HTMLTextAreaElement).value })}
                        />
                      </label>
                    </details>
                  </div>
                )}
              </details>

              <button type="button" class="btn-primary mt-3" disabled={loading} onClick={handleSaveDraft}>
                {detail?.status === "draft" ? "ドラフト更新" : "ドラフト作成"}
              </button>
            </div>
          )}

          {detail?.summary && (
            <div class="mb-4 rounded border p-4 text-sm">
              <h3 class="mb-2 font-semibold">topology summary</h3>
              <p><strong>dispatcher_mapping:</strong>{" "}
                {detail.summary.dispatcherMapping
                  ? `[${detail.summary.dispatcherMapping.role}, ${detail.summary.dispatcherMapping.target}, ${detail.summary.dispatcherMapping.layer}, ${detail.summary.dispatcherMapping.action}]`
                  : "—"}
              </p>
              <p><strong>runtime_mapping:</strong>{" "}
                {detail.summary.runtimeMapping?.runtimeDestination ?? "—"}
              </p>
              <p><strong>projection_constructor_mapping:</strong>{" "}
                {detail.summary.projectionConstructorMapping?.hasProjectionDefinition
                  ? formatProjectionSummary(extractProjectionDefinitionFromTopology(detail.topologyRawJson))
                  : "none"}
              </p>
              <p><strong>entry types:</strong> {detail.summary.entryTypes.join(", ") || "—"}</p>
              <details class="mt-2">
                <summary class="cursor-pointer text-muted-xs">debug: raw topology JSON</summary>
                <pre class="mt-2 max-h-64 overflow-auto rounded bg-slate-900 p-2 text-xs text-slate-100">{detail.topologyRawJson}</pre>
              </details>
            </div>
          )}

          {selectedId && (
            <div class="flex flex-wrap gap-2">
              <button type="button" class="btn-secondary" disabled={loading} onClick={handleValidate}>
                バリデート
              </button>
              <button
                type="button"
                class="btn-primary"
                disabled={loading || promoteDisabled}
                title={promoteDisabled ? "draft かつ valid である必要があります" : ""}
                onClick={handlePromote}
              >
                プロモート (draft → active)
              </button>
              <button
                type="button"
                class="btn-secondary"
                disabled={loading || deprecateDisabled}
                onClick={handleDeprecate}
              >
                非推奨化 (active → deprecated)
              </button>
            </div>
          )}

          {validation && (
            <div class="mt-4 rounded border p-4 text-sm">
              <h3 class="mb-2 font-semibold">
                バリデーション結果: {validation.valid ? "valid" : "invalid"}
              </h3>
              {validation.issues.length > 0 ? (
                <ul class="list-inside list-disc">
                  {validation.issues.map((issue) => (
                    <li key={issue.code} class="text-red-700">
                      [{issue.code}] {issue.message}
                    </li>
                  ))}
                </ul>
              ) : (
                <p class="text-emerald-700">blocking error なし</p>
              )}
            </div>
          )}

          {lifecycleResult && (
            <p class={`mt-4 text-sm ${lifecycleResult.ok ? "text-emerald-700" : "text-red-700"}`}>
              {lifecycleResult.message} (status={lifecycleResult.status})
            </p>
          )}
        </section>
      )}
      </>
      )}

      {editorMode === "promotion" && (
        <>
          <section class="mb-8">
            <h2 class="section-title">1. Promotion manifest 一覧</h2>
            <p class="mb-3 text-sm text-muted-xs">
              promotion_metadata_mapping を含む manifest のみ表示します。新規作成は Runtime wiring タブで draft を作成後、
              ここで metadata を追加してください。
            </p>
            <div class="mb-3 flex flex-wrap items-center gap-2">
              <label class="text-sm">
                ステータス:
                <select
                  class="ml-2 rounded border px-2 py-1"
                  value={promotionStatusFilter}
                  onChange={(e) => setPromotionStatusFilter((e.target as HTMLSelectElement).value)}
                >
                  {STATUS_FILTERS.map((s) => (
                    <option key={s || "all"} value={s}>{s || "すべて"}</option>
                  ))}
                </select>
              </label>
              <button type="button" class="btn-secondary" disabled={loading} onClick={loadPromotionList}>
                再読み込み
              </button>
            </div>

            {promotionManifests.length === 0 ? (
              <p class="text-sm text-muted-xs">promotion metadata 付き manifest がありません。</p>
            ) : (
              <div class="overflow-x-auto">
                <table class="w-full border-collapse text-sm">
                  <thead>
                    <tr class="border-b bg-slate-50 text-left">
                      {["manifest_id", "status", "manifest_key", "version", "has_disclosure"].map((h) => (
                        <th key={h} class="px-2 py-1 font-semibold">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {promotionManifests.map((m) => (
                      <tr
                        key={m.manifestId}
                        class={`cursor-pointer border-b hover:bg-slate-50 ${promotionSelectedId === m.manifestId ? "bg-blue-50" : ""}`}
                        onClick={() => loadPromotionDetail(m.manifestId)}
                      >
                        <td class="px-2 py-1"><code class="text-xs">{m.manifestId.slice(0, 8)}…</code></td>
                        <td class={`px-2 py-1 ${statusBadgeClass(m.status)}`}>{m.status}</td>
                        <td class="px-2 py-1">{m.manifestKey}</td>
                        <td class="px-2 py-1">{m.versionLabel}</td>
                        <td class="px-2 py-1">{String(m.hasDisclosure)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section class="mb-8">
            <h2 class="section-title">2. Disclosure / campaign metadata 編集</h2>
            <div class="mb-4 rounded border border-slate-200 bg-slate-50 p-4">
              <label class="block text-xs">
                対象 manifest_id（draft）
                <input
                  class="mt-1 w-full rounded border px-2 py-1 font-mono"
                  value={promotionDraftManifestId}
                  onInput={(e) => setPromotionDraftManifestId((e.target as HTMLInputElement).value)}
                  placeholder="00000000-0000-0000-0000-000000000061"
                />
              </label>
              {promotionDetail && (
                <p class="mt-2 text-xs text-muted-xs">
                  status={promotionDetail.status} — summary: {formatPromotionSummary(promotionDetail.metadata ?? undefined)}
                </p>
              )}
              <div class="mt-4 grid gap-2 sm:grid-cols-2">
                {([
                  ["manifestKey", promotionDraft.manifestKey, (v: string) => updatePromotionDraft({ manifestKey: v })],
                  ["versionLabel", promotionDraft.versionLabel, (v: string) => updatePromotionDraft({ versionLabel: v })],
                  ["placementKey", promotionDraft.placementKey, (v: string) => updatePromotionDraft({ placementKey: v })],
                  ["projectionSurfaceType", promotionDraft.projectionSurfaceType, (v: string) => updatePromotionDraft({ projectionSurfaceType: v })],
                ] as const).map(([label, value, setter]) => (
                  <label key={label} class="text-xs">
                    {label}
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 font-mono"
                      value={value}
                      onInput={(e) => setter((e.target as HTMLInputElement).value)}
                    />
                  </label>
                ))}
                <label class="text-xs sm:col-span-2">
                  disclosureText
                  <textarea
                    class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                    rows={3}
                    value={promotionDraft.disclosureText}
                    onInput={(e) => updatePromotionDraft({ disclosureText: (e.target as HTMLTextAreaElement).value })}
                  />
                </label>
                <label class="text-xs">
                  disclosureCategoryLabel
                  <input
                    class="mt-1 w-full rounded border px-2 py-1 font-mono"
                    value={promotionDraft.disclosureCategoryLabel}
                    onInput={(e) => updatePromotionDraft({ disclosureCategoryLabel: (e.target as HTMLInputElement).value })}
                  />
                </label>
                <label class="text-xs">
                  activationPolicyType
                  <select
                    class="mt-1 w-full rounded border px-2 py-1 font-mono"
                    value={promotionDraft.activationPolicyType}
                    onChange={(e) =>
                      updatePromotionDraft({
                        activationPolicyType: (e.target as HTMLSelectElement).value as PromotionManifestDraft["activationPolicyType"],
                      })}
                  >
                    {PROMOTION_ACTIVATION_POLICY_OPTIONS.map((p) => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                  </select>
                </label>
                <label class="text-xs sm:col-span-2">
                  activationConditionExpression（conditional / scheduled 時）
                  <input
                    class="mt-1 w-full rounded border px-2 py-1 font-mono"
                    value={promotionDraft.activationConditionExpression}
                    onInput={(e) =>
                      updatePromotionDraft({ activationConditionExpression: (e.target as HTMLInputElement).value })}
                  />
                </label>
              </div>

              <h3 class="mt-4 text-xs font-semibold">targetTopologyRefs</h3>
              {promotionDraft.targetRefs.map((ref, index) => (
                <div key={index} class="mt-2 grid gap-2 sm:grid-cols-3">
                  {(["packageId", "schemaId", "componentId"] as const).map((field) => (
                    <label key={field} class="text-xs">
                      {field}
                      <input
                        class="mt-1 w-full rounded border px-2 py-1 font-mono"
                        value={ref[field]}
                        onInput={(e) => updatePromotionTargetRef(index, { [field]: (e.target as HTMLInputElement).value })}
                      />
                    </label>
                  ))}
                </div>
              ))}

              <div class="mt-4 flex flex-wrap gap-2">
                <button
                  type="button"
                  class="btn-primary"
                  disabled={loading || (promotionDetail !== null && promotionDetail.status !== "draft") ||
                    (!promotionSelectedId && !promotionDraftManifestId.trim())}
                  onClick={handlePromotionSaveDraft}
                >
                  metadata ドラフト更新
                </button>
                <button
                  type="button"
                  class="btn-secondary"
                  disabled={loading || !promotionSelectedId}
                  onClick={handlePromotionValidate}
                >
                  promotion バリデート
                </button>
              </div>
            </div>

            {promotionValidation && (
              <div class="rounded border p-4 text-sm">
                <h3 class="mb-2 font-semibold">
                  promotion バリデーション: {promotionValidation.valid ? "valid" : "invalid"}
                </h3>
                {promotionValidation.issues.length > 0 ? (
                  <ul class="list-inside list-disc">
                    {promotionValidation.issues.map((issue) => (
                      <li key={issue.code} class="text-red-700">
                        [{issue.code}] {issue.message}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p class="text-emerald-700">blocking error なし</p>
                )}
              </div>
            )}
          </section>
        </>
      )}
    </main>
  );
}
