import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listAdminManifests,
  getAdminPromotionManifest,
  validateAdminPromotionManifest,
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
import { UX_STATUS_LABELS } from "../content/adminUxTerms.ts";

export default function ContentsPromotionPanel(): JSX.Element {
  const [manifests, setManifests] = useState<AdminManifestListItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState<AdminPromotionManifestDetail | null>(null);
  const [draft, setDraft] = useState<PromotionManifestDraft>(emptyPromotionManifestDraft());
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
    try {
      const saved = await updateAdminPromotionManifestDraft(
        buildPromotionUpdatePayload(selectedId, draft) as AdminPromotionManifestUpdateInput,
      );
      setDetail(saved);
      setStatus("公開・案内の下書きを保存しました。");
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
      const result = await validateAdminPromotionManifest(selectedId);
      setStatus(result?.valid ? "公開・案内: 問題なし" : "公開・案内: 要修正");
    } finally {
      setLoading(false);
    }
  };

  return (
    <section class="mb-8 rounded border p-4">
      <h2 class="section-title">公開・案内（manifest メタデータ）</h2>
      <p class="mb-3 text-xs text-muted-xs">
        案内文・キャンペーン情報は manifest 単体のメタデータとしてここで編集します（旧 Manifests 画面から移行）。
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

      <div class="mt-4 flex gap-2">
        <button type="button" class="btn-primary" disabled={loading || !selectedId} onClick={handleSave}>
          下書き保存
        </button>
        <button type="button" class="btn-secondary" disabled={loading || !selectedId} onClick={handleValidate}>
          内容を確認
        </button>
      </div>
    </section>
  );
}
