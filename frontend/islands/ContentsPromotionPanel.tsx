import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  getAdminPromotionManifest,
  validateAdminManifest,
  validateAdminPromotionManifest,
  promoteAdminManifest,
  updateAdminPromotionManifestDraft,
  type AdminManifestListItem,
  type AdminPromotionManifestDetail,
  type AdminPromotionManifestUpdateInput,
} from "../api/adminApi.ts";
import {
  buildPromotionUpdatePayload,
  emptyPromotionManifestDraft,
  formatPromotionSummary,
  parsePromotionMetadataToDraft,
  PROMOTION_ACTIVATION_POLICY_OPTIONS,
  type PromotionManifestDraft,
  type PromotionTargetRefDraft,
} from "../runtime/promotionManifestEditor.ts";
import { UX_STATUS_LABELS, UX_HUB_MANIFESTS_PAGE } from "../content/adminUxTerms.ts";

type ValidationState = { isBlocking: boolean } | null;

export default function ContentsPromotionPanel(): JSX.Element {
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState<AdminPromotionManifestDetail | null>(null);
  const [draft, setDraft] = useState<PromotionManifestDraft>(emptyPromotionManifestDraft());
  const [validation, setValidation] = useState<ValidationState>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    (async () => {
      const m = await listAdminManifests("draft");
      if (m) setManifests(m);
    })();
  }, []);

  const loadDetail = async (manifestId: string) => {
    setSelectedId(manifestId);
    setLoading(true);
    try {
      const d = await getAdminPromotionManifest(manifestId);
      setDetail(d);
      setDraft(parsePromotionMetadataToDraft(d?.metadata ?? undefined));
    } finally {
      setLoading(false);
    }
  };

  const updateDraft = (patch: Partial<PromotionManifestDraft>) => {
    setDraft((prev) => ({ ...prev, ...patch }));
  };

  const updateTargetRef = (index: number, patch: Partial<PromotionTargetRefDraft>) => {
    setDraft((prev) => ({
      ...prev,
      targetRefs: prev.targetRefs.map((ref, i) => (i === index ? { ...ref, ...patch } : ref)),
    }));
  };

  const handleSave = async () => {
    if (!selectedId) return;
    setLoading(true);
    setValidation(null);
    try {
      const saved = await updateAdminPromotionManifestDraft(
        buildPromotionUpdatePayload(selectedId, draft) as AdminPromotionManifestUpdateInput,
      );
      setDetail(saved);
      setStatus("公開・案内の下書きを保存しました。内容確認後に有効化してください。");
    } catch (e) {
      setStatus(String(e));
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    if (!selectedId) return;
    setLoading(true);
    try {
      // manifest data shape validation + promotion metadata validation の両面を確認する。
      // どちらか一方でも blocking なら有効化不可。backend promote は独立した fail-close を持つ。
      const [manifestResult, promotionResult] = await Promise.all([
        validateAdminManifest(selectedId),
        validateAdminPromotionManifest(selectedId),
      ]);
      const manifestBlocking = manifestResult ? !manifestResult.valid : true;
      const promotionBlocking = promotionResult ? promotionResult.isBlocking : true;
      const isBlocking = manifestBlocking || promotionBlocking;
      setValidation({ isBlocking });
      setStatus(
        isBlocking
          ? "内容確認: 要修正 — manifest データまたは公開・案内メタデータに問題があります（修正後に再確認してください）"
          : "内容確認: 問題なし — 有効化が可能です",
      );
    } finally {
      setLoading(false);
    }
  };

  const handlePromote = async () => {
    if (!selectedId) return;
    setLoading(true);
    try {
      const result = await promoteAdminManifest(selectedId);
      if (!result?.ok) {
        setStatus(`有効化エラー: ${result?.message ?? "promote failed"}`);
        return;
      }
      setStatus(`有効化完了 — topology_manifests へ投影済み。次: ${UX_HUB_MANIFESTS_PAGE}`);
      setValidation(null);
      const m = await listAdminManifests("draft");
      if (m) setManifests(m);
    } catch (e) {
      setStatus(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">③ 公開・案内 — 内容確認 → 有効化</h2>
      <p class="mb-2 text-xs text-muted-xs">
        ① 下書き作成 → ② 設計保存（上のパネル） → ③ 内容確認 → 有効化（このパネル）
      </p>
      <p class="mb-3 text-xs text-muted-xs">
        案内文・キャンペーン情報を入力して保存し、内容確認が通ったら有効化してください。
      </p>
      {status && <p class="mb-3 text-sm text-muted-xs">{status}</p>}

      <label class="block text-xs">
        対象下書き manifest
        <select
          class="mt-1 w-full rounded border px-2 py-1 font-mono"
          value={selectedId}
          onChange={(e) => {
            const id = (e.target as HTMLSelectElement).value;
            if (id) loadDetail(id);
          }}
        >
          <option value="">— 選択 —</option>
          {manifests.map((m) => (
            <option key={m.manifestId} value={m.manifestId}>
              {m.manifestId.slice(0, 8)}… [{UX_STATUS_LABELS[m.status] ?? m.status}]
            </option>
          ))}
        </select>
      </label>

      {detail && (
        <p class="mt-2 text-xs text-muted-xs">
          {formatPromotionSummary(detail.metadata ?? undefined)}
        </p>
      )}

      <div class="mt-4 grid gap-2 sm:grid-cols-2">
        {([
          ["設定キー", draft.manifestKey, (v: string) => updateDraft({ manifestKey: v })],
          ["版ラベル", draft.versionLabel, (v: string) => updateDraft({ versionLabel: v })],
          ["配置キー", draft.placementKey, (v: string) => updateDraft({ placementKey: v })],
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
          案内文
          <textarea
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            rows={3}
            value={draft.disclosureText}
            onInput={(e) => updateDraft({ disclosureText: (e.target as HTMLTextAreaElement).value })}
          />
        </label>
        <label class="text-xs">
          有効化タイミング
          <select
            class="mt-1 w-full rounded border px-2 py-1 font-mono"
            value={draft.activationPolicyType}
            onChange={(e) =>
              updateDraft({
                activationPolicyType: (e.target as HTMLSelectElement).value as PromotionManifestDraft["activationPolicyType"],
              })}
          >
            {PROMOTION_ACTIVATION_POLICY_OPTIONS.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
        </label>
      </div>

      {draft.targetRefs.map((ref, index) => (
        <div key={index} class="mt-2 grid gap-2 sm:grid-cols-3">
          {(["packageId", "schemaId", "componentId"] as const).map((field) => (
            <label key={field} class="text-xs">
              {field}
              <input
                class="mt-1 w-full rounded border px-2 py-1 font-mono"
                value={ref[field]}
                onInput={(e) => updateTargetRef(index, { [field]: (e.target as HTMLInputElement).value })}
              />
            </label>
          ))}
        </div>
      ))}

      <div class="mt-4 flex flex-wrap gap-2">
        <button type="button" class="btn-primary" disabled={loading || !selectedId} onClick={handleSave}>
          下書き保存
        </button>
        <button type="button" class="btn-secondary" disabled={loading || !selectedId} onClick={handleValidate}>
          内容を確認
        </button>
        <button
          type="button"
          class="btn-primary"
          disabled={loading || !selectedId || validation === null || validation.isBlocking}
          onClick={handlePromote}
        >
          有効化
        </button>
      </div>
    </section>
  );
}
