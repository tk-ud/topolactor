import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listContentHubs,
  listContentEntities,
  listContentRelations,
  listContentStates,
  getContentEntity,
  getContentHub,
  getContentRelation,
  searchContentBundle,
  createContentEntityDraft,
  updateContentEntityDraft,
  validateContentDraft,
  previewContentDraft,
  promoteContentDraft,
  type ContentBundleListItem,
  type ContentBundleEntityDetail,
  type ContentBundleHubDetail,
  type ContentBundleRelationDetail,
  type ContentBundleStateItem,
  type ContentBundleDraftDetail,
  type ContentBundleValidateResult,
  type ContentBundlePreviewResult,
  type ContentBundleLifecycleResult,
} from "../api/adminApi.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { ADMIN_CONTENTS_GUIDE } from "../content/adminGuides.ts";

type PanelError = { code?: string; message: string };

const KIND_FILTERS = ["", "hub", "entity", "relation", "hub_relation"] as const;

function buildEntityJsonb(label: string, stateName: string, hubId: string): Record<string, unknown> {
  return { label, state: stateName, hub_id: hubId };
}

export default function ContentsAdmin(): JSX.Element {
  const [items, setItems] = useState<ContentBundleListItem[]>([]);
  const [hubs, setHubs] = useState<ContentBundleListItem[]>([]);
  const [relations, setRelations] = useState<ContentBundleListItem[]>([]);
  const [states, setStates] = useState<ContentBundleStateItem[]>([]);
  const [keyword, setKeyword] = useState("");
  const [kindFilter, setKindFilter] = useState("");
  const [stateFilter, setStateFilter] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [selectedKind, setSelectedKind] = useState("");
  const [entityDetail, setEntityDetail] = useState<ContentBundleEntityDetail | null>(null);
  const [hubDetail, setHubDetail] = useState<ContentBundleHubDetail | null>(null);
  const [relationDetail, setRelationDetail] = useState<ContentBundleRelationDetail | null>(null);
  const [draft, setDraft] = useState<ContentBundleDraftDetail | null>(null);
  const [validation, setValidation] = useState<ContentBundleValidateResult | null>(null);
  const [preview, setPreview] = useState<ContentBundlePreviewResult | null>(null);
  const [lifecycleResult, setLifecycleResult] = useState<ContentBundleLifecycleResult | null>(null);
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  const [draftHubId, setDraftHubId] = useState("");
  const [draftStateName, setDraftStateName] = useState("active");
  const [draftLabel, setDraftLabel] = useState("");
  const [draftRelationIds, setDraftRelationIds] = useState<string[]>([]);
  const [advancedJson, setAdvancedJson] = useState("");

  const loadSelectors = async () => {
    const [h, r, s] = await Promise.all([
      listContentHubs(),
      listContentRelations(),
      listContentStates(),
    ]);
    if (h === null || r === null || s === null) {
      setBackendUnavailable(true);
      return;
    }
    setHubs(h);
    setRelations(r);
    setStates(s);
    if (!draftHubId && h.length > 0) setDraftHubId(h[0].id);
    if (r.length > 0 && draftRelationIds.length === 0) setDraftRelationIds([r[0].id]);
  };

  const loadBrowse = async () => {
    setLoading(true);
    setErrors([]);
    setBackendUnavailable(false);
    try {
      const result = await searchContentBundle(
        keyword || undefined,
        kindFilter || undefined,
        stateFilter || undefined,
      );
      if (result === null) {
        setBackendUnavailable(true);
        setItems([]);
        setStatus("バックエンド未設定 — DATABASE_URL / DEMO_BACKEND_URL を確認してください。");
        return;
      }
      setItems(result);
      setStatus(`${result.length} 件の content bundle 項目を読み込みました。`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSelectors();
    loadBrowse();
  }, []);

  const selectItem = async (item: ContentBundleListItem) => {
    setSelectedId(item.id);
    setSelectedKind(item.kind);
    setEntityDetail(null);
    setHubDetail(null);
    setRelationDetail(null);
    setValidation(null);
    setPreview(null);
    setLifecycleResult(null);
    setErrors([]);

    if (item.kind === "entity") {
      setLoading(true);
      try {
        const detail = await getContentEntity(item.id);
        if (detail === null) {
          setBackendUnavailable(true);
          return;
        }
        setEntityDetail(detail);
        setStatus(`entity 詳細: ${detail.label}`);
      } catch (e) {
        setErrors([{ message: String(e) }]);
      } finally {
        setLoading(false);
      }
    } else if (item.kind === "hub") {
      setLoading(true);
      try {
        const detail = await getContentHub(item.id);
        if (detail === null) {
          setBackendUnavailable(true);
          return;
        }
        setHubDetail(detail);
        setStatus(`hub 詳細: ${detail.entityCount} entities`);
      } catch (e) {
        setErrors([{ message: String(e) }]);
      } finally {
        setLoading(false);
      }
    } else if (item.kind === "relation") {
      setLoading(true);
      try {
        const detail = await getContentRelation(item.id);
        if (detail === null) {
          setBackendUnavailable(true);
          return;
        }
        setRelationDetail(detail);
        setStatus(`relation 詳細: ${detail.name}`);
      } catch (e) {
        setErrors([{ message: String(e) }]);
      } finally {
        setLoading(false);
      }
    } else {
      setStatus(`${item.kind} 選択: ${item.label}`);
    }
  };

  const buildDraftPayload = (): Record<string, unknown> | null => {
    if (showAdvanced && advancedJson.trim()) {
      try {
        return JSON.parse(advancedJson) as Record<string, unknown>;
      } catch {
        setErrors([{ message: "Advanced entityJsonb の JSON が不正です。" }]);
        return null;
      }
    }
    return buildEntityJsonb(draftLabel.trim(), draftStateName, draftHubId);
  };

  const handleCreateDraft = async () => {
    if (!draftHubId || !draftLabel.trim() || draftRelationIds.length === 0 || !draftStateName) {
      setErrors([{ message: "hub / label / relation / state は必須です。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    setValidation(null);
    setPreview(null);
    setLifecycleResult(null);

    const entityJsonb = buildDraftPayload();
    if (entityJsonb === null) {
      setLoading(false);
      return;
    }

    try {
      const created = await createContentEntityDraft({
        hubId: draftHubId,
        entityJsonb,
        relationIds: draftRelationIds,
        stateName: draftStateName,
      });
      setDraft(created);
      setStatus(`ドラフト作成: ${created.draftId}`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateDraft = async () => {
    if (!draft?.draftId || draft.status !== "draft") return;
    if (!draftHubId || !draftLabel.trim() || draftRelationIds.length === 0 || !draftStateName) {
      setErrors([{ message: "hub / label / relation / state は必須です。" }]);
      return;
    }
    setLoading(true);
    setErrors([]);
    setValidation(null);
    setPreview(null);
    setLifecycleResult(null);

    const entityJsonb = buildDraftPayload();
    if (entityJsonb === null) {
      setLoading(false);
      return;
    }

    try {
      const updated = await updateContentEntityDraft({
        draftId: draft.draftId,
        hubId: draftHubId,
        entityJsonb,
        relationIds: draftRelationIds,
        stateName: draftStateName,
      });
      setDraft(updated);
      setStatus(`ドラフト更新: ${updated.draftId}`);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    if (!draft?.draftId) return;
    setLoading(true);
    setValidation(null);
    setPreview(null);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await validateContentDraft(draft.draftId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setValidation(result);
      setStatus(result.valid ? "バリデーション成功。" : "blocking error があります。");
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePreview = async () => {
    if (!draft?.draftId) return;
    setLoading(true);
    setPreview(null);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await previewContentDraft(draft.draftId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setPreview(result);
      setValidation(result.validation);
      setStatus(result.canPromote ? "プレビュー: プロモート可能。" : "プレビュー: プロモート不可。");
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handlePromote = async () => {
    if (!draft?.draftId) return;
    setLoading(true);
    setLifecycleResult(null);
    setErrors([]);
    try {
      const result = await promoteContentDraft(draft.draftId);
      if (result === null) {
        setBackendUnavailable(true);
        return;
      }
      setLifecycleResult(result);
      if (!result.ok) {
        setErrors([{ code: result.errorCode ?? undefined, message: result.message }]);
        setStatus("プロモートに失敗しました。");
      } else {
        setStatus(`プロモート成功 — entity_id: ${result.entityId}`);
        await loadBrowse();
        const entities = await listContentEntities();
        if (entities) setItems(entities);
      }
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const toggleRelation = (relationId: string) => {
    setDraftRelationIds((prev) =>
      prev.includes(relationId)
        ? prev.filter((id) => id !== relationId)
        : [...prev, relationId],
    );
  };

  const promoteDisabled = !draft?.draftId || draft.status !== "draft" ||
    (validation !== null && validation.isBlocking) ||
    (preview !== null && !preview.canPromote);

  useEffect(() => {
    if (showAdvanced && draftHubId) {
      setAdvancedJson(JSON.stringify(
        buildEntityJsonb(draftLabel || "Untitled", draftStateName, draftHubId),
        null,
        2,
      ));
    }
  }, [showAdvanced, draftHubId, draftLabel, draftStateName]);

  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / コンテンツ</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <AdminHowTo steps={ADMIN_CONTENTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_CONTENTS_GUIDE} />

      <p class="mb-4 text-sm text-muted-xs">
        この画面は topology content の <strong>registration boundary</strong> です。
        direct DB editor ではありません。browse → draft → validate → preview → promote の流れで
        backend 検証を経て active topology content として登録します。
      </p>

      {backendUnavailable && (
        <p class="alert-warning mb-4 text-sm" role="status">
          バックエンド未接続 — content bundle 操作は DB + dispatch 経由でのみ利用できます。
        </p>
      )}

      <ValidationErrorPanel errors={errors} title="content bundle errors" />
      {status && <p class="mb-4 text-sm text-muted-xs">{status}</p>}

      <section class="mb-8">
        <h2 class="section-title">1. Content Bundle Browser</h2>
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <input
            type="search"
            class="rounded border px-2 py-1 text-sm"
            placeholder="keyword"
            value={keyword}
            onInput={(e) => setKeyword((e.target as HTMLInputElement).value)}
          />
          <select
            class="rounded border px-2 py-1 text-sm"
            value={kindFilter}
            onChange={(e) => setKindFilter((e.target as HTMLSelectElement).value)}
          >
            {KIND_FILTERS.map((k) => (
              <option key={k || "all"} value={k}>{k || "すべての kind"}</option>
            ))}
          </select>
          <input
            type="text"
            class="rounded border px-2 py-1 text-sm"
            placeholder="state filter"
            value={stateFilter}
            onInput={(e) => setStateFilter((e.target as HTMLInputElement).value)}
          />
          <button type="button" class="btn-secondary" disabled={loading} onClick={loadBrowse}>
            検索
          </button>
        </div>

        {items.length === 0 ? (
          <p class="text-sm text-muted-xs">項目がありません。</p>
        ) : (
          <div class="overflow-x-auto">
            <table class="w-full border-collapse text-sm">
              <thead>
                <tr class="border-b bg-slate-50 text-left">
                  {["kind", "label", "state", "summary"].map((h) => (
                    <th key={h} class="px-2 py-1 font-semibold">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={`${item.kind}-${item.id}`}
                    class={`cursor-pointer border-b hover:bg-slate-50 ${selectedId === item.id ? "bg-blue-50" : ""}`}
                    onClick={() => selectItem(item)}
                  >
                    <td class="px-2 py-1">{item.kind}</td>
                    <td class="px-2 py-1">{item.label}</td>
                    <td class="px-2 py-1">{item.state}</td>
                    <td class="px-2 py-1 text-xs text-muted-xs">{item.summary}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {entityDetail && selectedKind === "entity" && (
          <div class="mt-4 rounded border border-slate-200 bg-slate-50 p-4 text-sm">
            <h3 class="mb-2 font-semibold">Entity Detail</h3>
            <dl class="grid gap-2 sm:grid-cols-2">
              <div><dt class="font-semibold">label</dt><dd>{entityDetail.label}</dd></div>
              <div><dt class="font-semibold">state</dt><dd>{entityDetail.stateName}</dd></div>
              <div><dt class="font-semibold">hub</dt><dd>{entityDetail.hubLabel} ({entityDetail.hubId.slice(0, 8)}…)</dd></div>
              <div><dt class="font-semibold">relations</dt><dd>{entityDetail.relationLabels.join(", ") || "—"}</dd></div>
            </dl>
            <p class="mt-2 text-xs text-muted-xs">{entityDetail.summary}</p>
          </div>
        )}

        {hubDetail && selectedKind === "hub" && (
          <div class="mt-4 rounded border border-slate-200 bg-slate-50 p-4 text-sm">
            <h3 class="mb-2 font-semibold">Hub Detail</h3>
            <dl class="grid gap-2 sm:grid-cols-2">
              <div><dt class="font-semibold">state</dt><dd>{hubDetail.stateName}</dd></div>
              <div><dt class="font-semibold">relation</dt><dd>{hubDetail.relationLabel ?? "—"}</dd></div>
              <div><dt class="font-semibold">entities</dt><dd>{hubDetail.entityCount}</dd></div>
              <div><dt class="font-semibold">hub_relations</dt><dd>{hubDetail.hubRelationCount}</dd></div>
            </dl>
            {hubDetail.entityIds.length > 0 && (
              <p class="mt-2 text-xs text-muted-xs">entity ids: {hubDetail.entityIds.map((id) => id.slice(0, 8) + "…").join(", ")}</p>
            )}
            <p class="mt-2 text-xs text-muted-xs">{hubDetail.summary}</p>
          </div>
        )}

        {relationDetail && selectedKind === "relation" && (
          <div class="mt-4 rounded border border-slate-200 bg-slate-50 p-4 text-sm">
            <h3 class="mb-2 font-semibold">Relation Detail</h3>
            <dl class="grid gap-2 sm:grid-cols-2">
              <div><dt class="font-semibold">name</dt><dd>{relationDetail.name}</dd></div>
              <div><dt class="font-semibold">active</dt><dd>{String(relationDetail.active)}</dd></div>
              <div><dt class="font-semibold">entities</dt><dd>{relationDetail.entityCount}</dd></div>
              <div><dt class="font-semibold">hub_relations</dt><dd>{relationDetail.hubRelationCount}</dd></div>
            </dl>
            <p class="mt-2 text-xs text-muted-xs">{relationDetail.summary}</p>
          </div>
        )}
      </section>

      <section class="mb-8">
        <h2 class="section-title">2. Entity Draft Editor</h2>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="text-sm">
            Hub
            <select
              class="mt-1 block w-full rounded border px-2 py-1"
              value={draftHubId}
              onChange={(e) => setDraftHubId((e.target as HTMLSelectElement).value)}
            >
              {hubs.map((h) => (
                <option key={h.id} value={h.id}>{h.label} — {h.summary}</option>
              ))}
            </select>
          </label>
          <label class="text-sm">
            State
            <select
              class="mt-1 block w-full rounded border px-2 py-1"
              value={draftStateName}
              onChange={(e) => setDraftStateName((e.target as HTMLSelectElement).value)}
            >
              {states.map((s) => (
                <option key={s.stateId} value={s.name}>{s.name} ({s.owner ?? "—"})</option>
              ))}
            </select>
          </label>
          <label class="text-sm sm:col-span-2">
            Label
            <input
              type="text"
              class="mt-1 block w-full rounded border px-2 py-1"
              value={draftLabel}
              onInput={(e) => setDraftLabel((e.target as HTMLInputElement).value)}
              placeholder="Entity label"
            />
          </label>
        </div>

        <fieldset class="mt-3">
          <legend class="text-sm font-semibold">Relations</legend>
          <div class="mt-1 flex flex-wrap gap-3">
            {relations.map((r) => (
              <label key={r.id} class="flex items-center gap-1 text-sm">
                <input
                  type="checkbox"
                  checked={draftRelationIds.includes(r.id)}
                  onChange={() => toggleRelation(r.id)}
                />
                {r.label}
              </label>
            ))}
          </div>
        </fieldset>

        <details class="mt-4 rounded border border-amber-200 bg-amber-50 p-3">
          <summary class="cursor-pointer text-sm font-semibold text-amber-900">
            Advanced / Debug — entityJsonb raw edit & UUID override
          </summary>
          <p class="mt-2 text-xs text-amber-800">
            通常導線では selector 入力のみ使用してください。UUID 直入力や raw JSON は debug 用途に限定されます。
          </p>
          <label class="mt-2 block text-sm">
            <input
              type="checkbox"
              checked={showAdvanced}
              onChange={(e) => setShowAdvanced((e.target as HTMLInputElement).checked)}
            />
            {" "}Advanced entityJsonb を使用
          </label>
          {showAdvanced && (
            <textarea
              class="mt-2 block w-full rounded border px-2 py-1 font-mono text-xs"
              rows={6}
              value={advancedJson}
              onInput={(e) => setAdvancedJson((e.target as HTMLTextAreaElement).value)}
            />
          )}
        </details>

        <div class="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            class="btn-secondary"
            disabled={loading || backendUnavailable}
            onClick={handleCreateDraft}
          >
            ドラフト作成
          </button>
          {draft?.status === "draft" && (
            <button
              type="button"
              class="btn-secondary"
              disabled={loading || backendUnavailable}
              onClick={handleUpdateDraft}
            >
              ドラフト更新
            </button>
          )}
        </div>

        {draft && (
          <p class="mt-2 text-sm">
            現在のドラフト: <code>{draft.draftId}</code> ({draft.status})
          </p>
        )}
      </section>

      <section class="mb-8">
        <h2 class="section-title">3. Validation / Preview</h2>
        <div class="flex flex-wrap gap-2">
          <button type="button" class="btn-secondary" disabled={loading || !draft} onClick={handleValidate}>
            Validate
          </button>
          <button type="button" class="btn-secondary" disabled={loading || !draft} onClick={handlePreview}>
            Preview (read-only)
          </button>
        </div>

        {validation && (
          <div class="mt-4 rounded border p-3 text-sm">
            <p class={validation.valid ? "text-emerald-700" : "text-red-700"}>
              valid={String(validation.valid)} blocking={String(validation.isBlocking)}
            </p>
            {validation.issues.length > 0 && (
              <ul class="mt-2 list-inside list-disc">
                {validation.issues.map((issue) => (
                  <li key={issue.code} class={issue.isBlocking ? "text-red-700" : "text-amber-700"}>
                    [{issue.code}] {issue.message}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {preview && (
          <div class="mt-4 rounded border border-blue-200 bg-blue-50 p-3 text-sm">
            <h3 class="font-semibold">Preview</h3>
            <p>label: {preview.label} / hub: {preview.hubLabel ?? preview.hubId}</p>
            <p>relations: {preview.relationLabels.join(", ") || "—"}</p>
            <p>state: {preview.stateName ?? "—"}</p>
            <p>canPromote: {String(preview.canPromote)}</p>
          </div>
        )}
      </section>

      <section class="mb-8">
        <h2 class="section-title">4. Promote</h2>
        <button
          type="button"
          class="btn-secondary"
          disabled={loading || promoteDisabled}
          onClick={handlePromote}
        >
          Promote to active entity
        </button>
        {promoteDisabled && draft && (
          <p class="mt-2 text-xs text-muted-xs">
            プロモートには validation pass（blocking error なし）が必要です。
          </p>
        )}

        {lifecycleResult && (
          <div class="mt-4 rounded border p-3 text-sm">
            <p class={lifecycleResult.ok ? "text-emerald-700" : "text-red-700"}>
              {lifecycleResult.message}
            </p>
            {lifecycleResult.entityId && (
              <p>created entity_id: <code>{lifecycleResult.entityId}</code></p>
            )}
            {lifecycleResult.readback && (
              <p class="mt-2 text-xs">
                readback: {lifecycleResult.readback.label} / {lifecycleResult.readback.stateName}
              </p>
            )}
            {lifecycleResult.ok && (
              <p class="mt-3 text-xs text-muted-xs">
                次のステップ:{" "}
                <a href="/admin/ui-builder" class="link">UI Builder</a> でレイアウト登録、または{" "}
                <a href="/admin/runtime" class="link">Runtime確認</a> で dispatch を検証してください。
              </p>
            )}
          </div>
        )}
      </section>
    </main>
  );
}
