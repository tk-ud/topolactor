import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  type ContextToken,
  createContextToken,
  deprecateContextToken,
  fetchContextTokens,
} from "../api/adminApi.ts";
import { AdminActionHint } from "../components/AdminHelpPanel.tsx";

export default function ContextTokenRegistryEditor(): JSX.Element {
  const [tokens, setTokens] = useState<ContextToken[]>([]);
  const [notBound, setNotBound] = useState(false);
  const [notAuthed, setNotAuthed] = useState(false);
  const [newLabel, setNewLabel] = useState("");
  const [newGroup, setNewGroup] = useState("");
  const [newValue, setNewValue] = useState("0.0");
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchContextTokens().then((result) => {
      if (result === null) {
        setNotBound(true);
      } else {
        setTokens(result);
      }
    }).catch((err: unknown) => {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("HTTP 401")) {
        setNotAuthed(true);
      } else {
        setStatus(msg);
      }
    });
  }, []);

  if (notBound) {
    return (
      <p class="text-muted">
        レジストリ未接続 — DEMO_BACKEND_URL が未設定です (501)。
        Docker Compose 起動後に <a href="/auth" class="link">ログイン</a> してください。
      </p>
    );
  }

  if (notAuthed) {
    return (
      <p class="text-muted">
        ログインが必要です — <a href="/auth" class="link">ログインページ</a> で認証してから再度アクセスしてください。
      </p>
    );
  }

  async function handleAdd(e: Event): Promise<void> {
    e.preventDefault();
    const value = parseFloat(newValue);
    if (!newLabel || isNaN(value) || value < -1 || value > 1) {
      setStatus("ラベルと value（-1.0〜1.0）は必須です。");
      return;
    }
    setLoading(true);
    setStatus(null);
    try {
      const result = await createContextToken({
        label: newLabel,
        group: newGroup || null,
        value,
      });
      if (result.ok) {
        setTokens((prev) => [
          ...prev,
          {
            tokenId: result.tokenId ?? crypto.randomUUID(),
            label: newLabel,
            group: newGroup || null,
            value,
            status: "active",
          },
        ]);
        setNewLabel("");
        setNewGroup("");
        setNewValue("0.0");
      }
      setStatus(result.message);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("HTTP 401")) {
        setNotAuthed(true);
      } else {
        setStatus(msg);
      }
    }
    setLoading(false);
  }

  async function handleDeprecate(tokenId: string): Promise<void> {
    setLoading(true);
    try {
      const result = await deprecateContextToken(tokenId);
      if (result.ok) {
        setTokens((prev) =>
          prev.map((t) => t.tokenId === tokenId ? { ...t, status: "deprecated" } : t)
        );
      }
      setStatus(result.message);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("HTTP 401")) {
        setNotAuthed(true);
      } else {
        setStatus(msg);
      }
    }
    setLoading(false);
  }

  return (
    <div class="max-w-3xl">
      <h3 class="section-title">アクティブトークン一覧</h3>
      <div class="table-wrap mb-6">
        <table class="table">
          <thead>
            <tr>
              {["ラベル", "グループ", "値", "ステータス", ""].map((h) => (
                <th key={h}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {tokens.map((t) => (
              <tr key={t.tokenId} class={t.status === "deprecated" ? "opacity-50" : ""}>
                <td>{t.label}</td>
                <td>{t.group ?? "—"}</td>
                <td class="font-mono">{t.value.toFixed(2)}</td>
                <td>{t.status === "active" ? "有効" : "非推奨"}</td>
                <td>
                  {t.status === "active" && (
                    <button
                      onClick={() => handleDeprecate(t.tokenId)}
                      disabled={loading}
                      class="btn-danger px-2 py-1 text-xs"
                      title="DB 上 status=deprecated。推薦対象から外す（物理削除ではない）"
                    >
                      非推奨にする
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {tokens.length === 0 && (
              <tr>
                <td colSpan={5} class="py-4 text-center text-muted">トークンなし</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <h3 class="section-title">新規トークン追加</h3>
      <form onSubmit={handleAdd} class="flex flex-wrap items-end gap-3">
        <div>
          <label class="mb-1 block text-xs font-medium">
            ラベル <span class="text-red-600">*</span>
          </label>
          <input class="input-mono" type="text" placeholder="例: 点検中" value={newLabel} onInput={(e) => setNewLabel((e.target as HTMLInputElement).value)} required />
        </div>
        <div>
          <label class="mb-1 block text-xs font-medium">グループ</label>
          <input class="input-mono" type="text" placeholder="例: status" value={newGroup} onInput={(e) => setNewGroup((e.target as HTMLInputElement).value)} />
        </div>
        <div>
          <label class="mb-1 block text-xs font-medium">
            値 [-1.0〜1.0] <span class="text-red-600">*</span>
          </label>
          <input class="input-mono w-24" type="number" step="0.1" min="-1.0" max="1.0" value={newValue} onInput={(e) => setNewValue((e.target as HTMLInputElement).value)} required />
        </div>
        <button type="submit" disabled={loading} class="btn-primary">
          {loading ? "追加中..." : "トークン追加"}
        </button>
      </form>
      <AdminActionHint>
        label は表示名、value は [-1,1] の意味方向。backend が範囲検証後 DB に create します。
      </AdminActionHint>

      {status && (
        <div class="mt-3">
          <p class="pre-box">{status}</p>
          <AdminActionHint>
            401/501 は /auth と DEMO_BACKEND_URL。value 範囲外はフォームを修正。
          </AdminActionHint>
        </div>
      )}
    </div>
  );
}
