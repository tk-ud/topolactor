import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import {
  listCredentialReferences,
  registerCredentialReference,
  validateCredentialReference,
  rotateCredentialReference,
  type CredentialReferenceItem,
} from "../api/adminApi.ts";
import { getTokenClaims } from "../api/authApi.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";

function getAdminUsername(): string {
  if (typeof globalThis.sessionStorage === "undefined") return "";
  const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
  if (!token) return "";
  const claims = getTokenClaims(token);
  return typeof claims?.sub === "string" ? claims.sub : "";
}

type OpStatus = { ok: boolean; message: string } | null;

export default function AdminCredentialPanel(): JSX.Element {
  const [items, setItems] = useState<CredentialReferenceItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [backendUnavailable, setBackendUnavailable] = useState(false);

  // register form
  const [regKey, setRegKey] = useState("");
  const [regKind, setRegKind] = useState("env_var");
  const [regStatus, setRegStatus] = useState<OpStatus>(null);
  const [regLoading, setRegLoading] = useState(false);

  // per-row op status
  const [rowStatus, setRowStatus] = useState<Record<string, OpStatus>>({});
  const [rowLoading, setRowLoading] = useState<Record<string, boolean>>({});

  const load = async () => {
    setLoading(true);
    setBackendUnavailable(false);
    try {
      const list = await listCredentialReferences();
      if (list === null) {
        setBackendUnavailable(true);
        return;
      }
      setItems(list);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const handleRegister = async () => {
    const key = regKey.trim();
    const kind = regKind.trim();
    if (!key || !kind) {
      setRegStatus({ ok: false, message: "reference_key と provider_kind は必須です。" });
      return;
    }
    setRegLoading(true);
    setRegStatus(null);
    try {
      const result = await registerCredentialReference(key, kind);
      if (result === null) {
        setRegStatus({ ok: false, message: "バックエンドと通信できませんでした。" });
        return;
      }
      setRegStatus({ ok: true, message: `登録しました: ${result.reference_key}` });
      setRegKey("");
      await load();
    } catch (e) {
      setRegStatus({ ok: false, message: String(e) });
    } finally {
      setRegLoading(false);
    }
  };

  const handleValidate = async (referenceKey: string) => {
    setRowLoading((prev) => ({ ...prev, [referenceKey]: true }));
    setRowStatus((prev) => ({ ...prev, [referenceKey]: null }));
    try {
      const result = await validateCredentialReference(referenceKey);
      if (result === null) {
        setRowStatus((prev) => ({
          ...prev,
          [referenceKey]: { ok: false, message: "バックエンドと通信できませんでした。" },
        }));
        return;
      }
      setRowStatus((prev) => ({
        ...prev,
        [referenceKey]: {
          ok: result.is_valid,
          message: result.is_valid
            ? "検証成功: secret store に credential が存在します。"
            : "検証失敗: secret store に credential が見つかりません。",
        },
      }));
      await load();
    } catch (e) {
      setRowStatus((prev) => ({
        ...prev,
        [referenceKey]: { ok: false, message: String(e) },
      }));
    } finally {
      setRowLoading((prev) => ({ ...prev, [referenceKey]: false }));
    }
  };

  const handleRotate = async (referenceKey: string) => {
    const actorId = getAdminUsername();
    if (!actorId) {
      setRowStatus((prev) => ({
        ...prev,
        [referenceKey]: { ok: false, message: "管理者セッションが取得できません。再ログインしてください。" },
      }));
      return;
    }
    if (!globalThis.confirm(`credential "${referenceKey}" の rotation を記録します。\n\nrotation 後に secret store の検証も実行されます。続けますか？`)) return;

    setRowLoading((prev) => ({ ...prev, [referenceKey]: true }));
    setRowStatus((prev) => ({ ...prev, [referenceKey]: null }));
    try {
      const result = await rotateCredentialReference(referenceKey, actorId);
      if (result === null) {
        setRowStatus((prev) => ({
          ...prev,
          [referenceKey]: { ok: false, message: "バックエンドと通信できませんでした。" },
        }));
        return;
      }
      setRowStatus((prev) => ({
        ...prev,
        [referenceKey]: {
          ok: result.rotated,
          message: result.rotated
            ? "rotation 記録 + 検証成功。"
            : "rotation に失敗しました。",
        },
      }));
      await load();
    } catch (e) {
      setRowStatus((prev) => ({
        ...prev,
        [referenceKey]: { ok: false, message: String(e) },
      }));
    } finally {
      setRowLoading((prev) => ({ ...prev, [referenceKey]: false }));
    }
  };

  const validationStatusLabel = (s: string) => {
    if (s === "valid") return <span class="text-green-700 font-medium">valid</span>;
    if (s === "invalid") return <span class="text-red-600 font-medium">invalid</span>;
    return <span class="text-muted">unchecked</span>;
  };

  const statusLabel = (s: string) => {
    if (s === "active") return <span class="text-green-700">active</span>;
    if (s === "rotation_pending") return <span class="text-yellow-700">rotation_pending</span>;
    if (s === "revoked") return <span class="text-red-600">revoked</span>;
    return <span class="text-muted">{s}</span>;
  };

  return (
    <div class="page-main-wide">
      <h1 class="page-title">Credential Registry</h1>
      <p class="text-muted mb-6">
        credential 参照（reference_key / provider_kind）の登録・検証・rotation を管理します。
        <br />
        <span class="text-xs text-muted">
          ※ credential 実値はここに保存・表示されません。secret store（環境変数等）で管理してください。
        </span>
      </p>

      {/* Register form */}
      <div class="card p-4 mb-6">
        <h2 class="section-title mb-3">新規 credential 参照登録</h2>
        <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div class="flex-1">
            <label class="block text-sm font-medium text-gray-700 mb-1">
              reference_key
            </label>
            <input
              class="input w-full"
              type="text"
              placeholder="例: smtp_api_key"
              value={regKey}
              onInput={(e) => setRegKey((e.target as HTMLInputElement).value)}
              disabled={regLoading}
            />
          </div>
          <div class="w-48">
            <label class="block text-sm font-medium text-gray-700 mb-1">
              provider_kind
            </label>
            <select
              class="input w-full"
              value={regKind}
              onChange={(e) => setRegKind((e.target as HTMLSelectElement).value)}
              disabled={regLoading}
            >
              <option value="env_var">env_var</option>
            </select>
          </div>
          <button
            class="btn-primary"
            onClick={handleRegister}
            disabled={regLoading || !regKey.trim()}
          >
            {regLoading ? "登録中…" : "登録"}
          </button>
        </div>
        {regStatus && (
          <div class={`mt-3 text-sm ${regStatus.ok ? "text-green-700" : "alert-error"}`}>
            {regStatus.message}
          </div>
        )}
      </div>

      {/* List */}
      <div class="card">
        <div class="flex items-center justify-between p-4 border-b">
          <h2 class="section-title">登録済み credential 参照一覧</h2>
          <button class="btn-secondary text-sm" onClick={load} disabled={loading}>
            {loading ? "読込中…" : "更新"}
          </button>
        </div>

        {backendUnavailable && (
          <div class="alert-error m-4">バックエンドと通信できませんでした。</div>
        )}

        {!backendUnavailable && items.length === 0 && !loading && (
          <p class="text-muted p-4">登録済みの credential 参照はありません。</p>
        )}

        {items.length > 0 && (
          <div class="table-wrap">
            <table class="table w-full">
              <thead>
                <tr>
                  <th>reference_key</th>
                  <th>provider_kind</th>
                  <th>status</th>
                  <th>validation</th>
                  <th>最終検証</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.credential_reference_id}>
                    <td class="font-mono text-sm">{item.reference_key}</td>
                    <td class="text-sm">{item.provider_kind}</td>
                    <td class="text-sm">{statusLabel(item.status)}</td>
                    <td class="text-sm">{validationStatusLabel(item.validation_status)}</td>
                    <td class="text-muted-xs">
                      {item.last_validated_at
                        ? new Date(item.last_validated_at).toLocaleString("ja-JP")
                        : "—"}
                    </td>
                    <td>
                      <div class="flex gap-2 items-center">
                        <button
                          class="btn-secondary text-xs"
                          onClick={() => handleValidate(item.reference_key)}
                          disabled={rowLoading[item.reference_key]}
                        >
                          検証
                        </button>
                        <button
                          class="btn-secondary text-xs"
                          onClick={() => handleRotate(item.reference_key)}
                          disabled={rowLoading[item.reference_key]}
                        >
                          Rotate
                        </button>
                      </div>
                      {rowStatus[item.reference_key] && (
                        <div
                          class={`mt-1 text-xs ${
                            rowStatus[item.reference_key]!.ok
                              ? "text-green-700"
                              : "text-red-600"
                          }`}
                        >
                          {rowStatus[item.reference_key]!.message}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
