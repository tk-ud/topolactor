/** @jsxImportSource preact */
import { useEffect, useMemo, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  adminListUserSessions,
  adminRevokeUserCredential,
  adminRevokeUserSessions,
  createAuthUser,
  deleteAuthUser,
  getEnumDictionaryGroup,
  listAuthUsers,
  updateAuthUser,
  USER_STATUS_ENUM_GROUP_ID,
  type AdminSessionSummary,
  type AuthUserRoster,
  type EnumDictionaryItem,
} from "../api/adminApi.ts";
import { Modal } from "../components/Modal.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { useConfirm } from "../hooks/useConfirm.tsx";
import { adminSubmitConfirm } from "../lib/adminSubmitUx.ts";

type PanelError = { code?: string; message: string };

function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function AdminUsersRoster(): JSX.Element {
  const [users, setUsers] = useState<AuthUserRoster[]>([]);
  const [statusItems, setStatusItems] = useState<EnumDictionaryItem[]>([]);
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [modalOpen, setModalOpen] = useState(false);
  const [newUsername, setNewUsername] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newApprove, setNewApprove] = useState(true);
  const [newStatus, setNewStatus] = useState("active");
  const [draft, setDraft] = useState<Partial<AuthUserRoster>>({});
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [loading, setLoading] = useState(false);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const [sessions, setSessions] = useState<AdminSessionSummary[]>([]);
  const [sessionNotice, setSessionNotice] = useState<string | null>(null);
  const { confirm, ConfirmDialogHost } = useConfirm();

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return users;
    return users.filter((u) =>
      u.username.toLowerCase().includes(q) ||
      (u.status?.toLowerCase().includes(q) ?? false));
  }, [users, search]);

  const selected = users.find((u) => u.userId === selectedId) ?? null;

  const loadUsers = async (query?: string) => {
    const list = await listAuthUsers(query);
    if (list === null) { setBackendUnavailable(true); return; }
    setUsers(list);
    setBackendUnavailable(false);
  };

  useEffect(() => {
    loadUsers();
    getEnumDictionaryGroup(USER_STATUS_ENUM_GROUP_ID).then((g) => {
      if (g?.items) setStatusItems(g.items);
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (selected) setDraft({ ...selected });
    else setDraft({});
    setSessions([]);
    setSessionNotice(null);
    if (selected) {
      adminListUserSessions(selected.userId).then((res) => {
        if (res.success) setSessions(res.sessions ?? []);
      }).catch(() => {});
    }
  }, [selectedId, users]);

  const handleShowAll = async () => {
    setSearch("");
    await loadUsers();
  };

  const handleCreate = async () => {
    if (!(await adminSubmitConfirm(confirm, "create"))) return;
    setLoading(true);
    setErrors([]);
    try {
      await createAuthUser({
        username: newUsername.trim(),
        password: newPassword,
        approve: newApprove,
        status: newStatus,
      });
      setModalOpen(false);
      setNewUsername("");
      setNewPassword("");
      await loadUsers();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!selectedId || !draft.userId) return;
    if (!(await adminSubmitConfirm(confirm, "update"))) return;
    setLoading(true);
    setErrors([]);
    try {
      await updateAuthUser({
        userId: selectedId,
        username: draft.username,
        active: draft.active,
        approve: draft.approve,
        status: draft.status ?? undefined,
        suspendedFrom: draft.suspendedFrom || null,
        suspendedUntil: draft.suspendedUntil || null,
        stateNote: draft.stateNote ?? null,
        roleName: draft.role === "admin" ? "admin" : "user",
      });
      await loadUsers();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!selectedId) return;
    if (!(await confirm("このユーザーを削除します。よろしいですか？", { variant: "danger" }))) {
      return;
    }
    setLoading(true);
    try {
      await deleteAuthUser(selectedId);
      setSelectedId("");
      await loadUsers();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const reloadSessions = async () => {
    if (!selectedId) return;
    const res = await adminListUserSessions(selectedId);
    if (res.success) setSessions(res.sessions ?? []);
  };

  const handleRevokeSession = async (sessionId: string) => {
    if (!selectedId) return;
    setSessionNotice(null);
    try {
      const res = await adminRevokeUserSessions(selectedId, sessionId);
      if (!res.success) {
        setErrors([{ message: res.errors?.[0]?.message ?? "セッション無効化に失敗しました" }]);
        return;
      }
      setSessionNotice(`セッション ${sessionId} を無効化しました。`);
      await reloadSessions();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    }
  };

  const handleRevokeAllSessions = async () => {
    if (!selectedId) return;
    if (!(await confirm("このユーザーの全セッションを無効化します。よろしいですか？", { variant: "danger" }))) {
      return;
    }
    setSessionNotice(null);
    try {
      const res = await adminRevokeUserSessions(selectedId);
      if (!res.success) {
        setErrors([{ message: res.errors?.[0]?.message ?? "セッション無効化に失敗しました" }]);
        return;
      }
      setSessionNotice(`全セッション（${res.sessionsRevoked ?? 0}件）を無効化しました。`);
      await reloadSessions();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    }
  };

  const handleRevokeCredential = async () => {
    if (!selectedId) return;
    if (
      !(await confirm(
        "このユーザーの認証情報を無効化します（パスワード無効化・全セッション無効化）。新しいパスワードはこの画面から設定できません。よろしいですか？",
        { variant: "danger" },
      ))
    ) {
      return;
    }
    setSessionNotice(null);
    try {
      const res = await adminRevokeUserCredential(selectedId);
      if (!res.success) {
        setErrors([{ message: res.errors?.[0]?.message ?? "認証情報の無効化に失敗しました" }]);
        return;
      }
      setSessionNotice("認証情報を無効化しました。このアカウントはログインできなくなりました。");
      await reloadSessions();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    }
  };

  return (
    <div class="mx-auto max-w-5xl space-y-4 p-4">
      <h1 class="text-lg font-semibold text-gray-900">ユーザー名簿</h1>
      {backendUnavailable && (
        <p class="text-sm text-amber-700">バックエンドに接続できません。</p>
      )}
      <ValidationErrorPanel errors={errors} />

      <section class="rounded-lg border border-gray-200 bg-white p-4 space-y-3">
        <div class="flex flex-wrap gap-2 items-center">
          <input
            type="search"
            class="input-base flex-1 min-w-[200px]"
            placeholder="ユーザー名・状態で検索"
            value={search}
            onInput={(e) => setSearch((e.target as HTMLInputElement).value)}
          />
          <button type="button" class="btn-secondary text-sm" onClick={handleShowAll} disabled={loading}>
            全件出力
          </button>
          <button type="button" class="btn-primary text-sm" onClick={() => setModalOpen(true)}>
            新規追加
          </button>
          <span class="text-xs text-gray-500">{filtered.length} 件</span>
        </div>

        <table class="w-full text-xs">
          <thead>
            <tr class="border-b text-left text-gray-500">
              <th class="px-2 py-1">ユーザー名</th>
              <th class="px-2 py-1">role</th>
              <th class="px-2 py-1">承認</th>
              <th class="px-2 py-1">状態</th>
              <th class="px-2 py-1">最終ログイン</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((u) => (
              <tr
                key={u.userId}
                class={`border-b cursor-pointer hover:bg-gray-50 ${selectedId === u.userId ? "bg-blue-50" : ""}`}
                onClick={() => setSelectedId(u.userId)}
              >
                <td class="px-2 py-1">{u.username}</td>
                <td class="px-2 py-1"><code class="rounded bg-gray-100 px-1">{u.role}</code></td>
                <td class="px-2 py-1">{u.approve ? "はい" : "いいえ"}</td>
                <td class="px-2 py-1">{u.status ?? "—"}</td>
                <td class="px-2 py-1 font-mono text-gray-600">
                  {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {selected && (
        <section class="rounded-lg border border-blue-200 bg-blue-50 p-4 space-y-3">
          <h2 class="text-sm font-semibold text-blue-900">インライン編集 — {selected.username}</h2>
          <label class="block text-xs font-medium">ユーザー名</label>
          <input class="input-base max-w-md w-full" value={draft.username ?? ""}
            onInput={(e) => setDraft({ ...draft, username: (e.target as HTMLInputElement).value })} />
          <label class="block text-xs font-medium">role</label>
          <select class="input-base max-w-md w-full" value={draft.role ?? "user"}
            onChange={(e) => setDraft({ ...draft, role: (e.target as HTMLSelectElement).value })}>
            <option value="user">user</option>
            <option value="admin">admin</option>
          </select>
          <label class="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={draft.approve ?? false}
              onChange={(e) => setDraft({ ...draft, approve: (e.target as HTMLInputElement).checked })} />
            承認 (approve)
          </label>
          <label class="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={draft.active ?? true}
              onChange={(e) => setDraft({ ...draft, active: (e.target as HTMLInputElement).checked })} />
            有効 (active)
          </label>
          <label class="block text-xs font-medium">状態 (status)</label>
          <select class="input-base max-w-md w-full" value={draft.status ?? ""}
            onChange={(e) => setDraft({ ...draft, status: (e.target as HTMLSelectElement).value })}>
            <option value="">—</option>
            {statusItems.map((it) => (
              <option key={it.indexNum} value={it.name}>{it.name}</option>
            ))}
          </select>
          <label class="block text-xs font-medium">停止開始</label>
          <input type="datetime-local" class="input-base max-w-md w-full"
            value={toLocalInput(draft.suspendedFrom ?? null)}
            onInput={(e) => setDraft({
              ...draft,
              suspendedFrom: (e.target as HTMLInputElement).value
                ? new Date((e.target as HTMLInputElement).value).toISOString()
                : null,
            })} />
          <label class="block text-xs font-medium">停止終了（空=無期限）</label>
          <input type="datetime-local" class="input-base max-w-md w-full"
            value={toLocalInput(draft.suspendedUntil ?? null)}
            onInput={(e) => setDraft({
              ...draft,
              suspendedUntil: (e.target as HTMLInputElement).value
                ? new Date((e.target as HTMLInputElement).value).toISOString()
                : null,
            })} />
          <label class="block text-xs font-medium">管理メモ (state_note)</label>
          <textarea class="input-base w-full max-w-lg" rows={2} value={draft.stateNote ?? ""}
            onInput={(e) => setDraft({ ...draft, stateNote: (e.target as HTMLTextAreaElement).value })} />
          <p class="text-xs text-gray-600">
            最終ログイン (readonly): {selected.lastLoginAt
              ? new Date(selected.lastLoginAt).toLocaleString()
              : "—"}
          </p>
          <div class="flex gap-2">
            <button type="button" class="btn-primary text-sm" disabled={loading} onClick={handleSave}>保存</button>
            <button type="button" class="btn-danger text-sm" disabled={loading} onClick={handleDelete}>削除</button>
          </div>

          <div class="mt-3 border-t border-blue-200 pt-3">
            <h3 class="text-sm font-semibold text-blue-900">セッション / 認証情報</h3>
            <p class="mt-1 text-xs text-gray-600">
              このユーザーのパスワード値をここから指定・変更することはできません。無効化のみ行えます。
            </p>
            <div class="mt-2 flex gap-2">
              <button type="button" class="btn-secondary text-sm" onClick={handleRevokeAllSessions}>
                全セッションを無効化
              </button>
              <button type="button" class="btn-danger text-sm" onClick={handleRevokeCredential}>
                認証情報を無効化（ログイン不可にする）
              </button>
            </div>
            {sessionNotice && <p class="mt-2 text-xs text-green-700">{sessionNotice}</p>}
            <ul class="mt-2 space-y-1">
              {sessions.map((s) => (
                <li key={s.sessionId} class="flex items-center justify-between gap-2 text-xs">
                  <span>{s.realm} / {s.audience} — 期限: {s.expiresAt}</span>
                  <button type="button" class="btn-secondary text-xs" onClick={() => handleRevokeSession(s.sessionId)}>
                    無効化
                  </button>
                </li>
              ))}
              {sessions.length === 0 && <li class="text-xs text-gray-500">有効なセッションはありません。</li>}
            </ul>
          </div>
        </section>
      )}

      <Modal open={modalOpen} title="ユーザーを追加" onClose={() => setModalOpen(false)}
        footer={(
          <div class="flex justify-end gap-2">
            <button type="button" class="btn-secondary text-sm" onClick={() => setModalOpen(false)}>キャンセル</button>
            <button type="button" class="btn-primary text-sm"
              disabled={loading || !newUsername.trim() || !newPassword}
              onClick={handleCreate}>登録</button>
          </div>
        )}
      >
        <div class="space-y-3">
          <div>
            <label class="block text-xs font-medium mb-1">ユーザー名</label>
            <input class="input-base w-full" value={newUsername}
              onInput={(e) => setNewUsername((e.target as HTMLInputElement).value)} />
          </div>
          <div>
            <label class="block text-xs font-medium mb-1">初期パスワード</label>
            <input type="password" class="input-base w-full" value={newPassword}
              onInput={(e) => setNewPassword((e.target as HTMLInputElement).value)} />
          </div>
          <label class="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={newApprove}
              onChange={(e) => setNewApprove((e.target as HTMLInputElement).checked)} />
            承認済みで作成
          </label>
          <div>
            <label class="block text-xs font-medium mb-1">状態</label>
            <select class="input-base w-full" value={newStatus}
              onChange={(e) => setNewStatus((e.target as HTMLSelectElement).value)}>
              {statusItems.map((it) => (
                <option key={it.indexNum} value={it.name}>{it.name}</option>
              ))}
            </select>
          </div>
        </div>
      </Modal>

      <ConfirmDialogHost />
    </div>
  );
}
