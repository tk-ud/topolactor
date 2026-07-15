/**
 * PersonalPage — self account info / role / status / session management / password change.
 * All operations target only the authenticated caller's own account (backend resolves the
 * target from the validated JWT subject; no userId is ever sent from this component). See
 * docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.personal_page.
 */
import { useCallback, useEffect, useState } from "preact/hooks";
import {
  changeOwnPassword,
  getCurrentAccount,
  listOwnSessions,
  logoutUser,
  revokeOtherSessions,
  revokeOwnSession,
  type CurrentAccountResponse,
  type SessionSummary,
} from "../api/authApi.ts";
import { clearSessionToken } from "../lib/demoSession.ts";
import { useCurrentSession } from "../hooks/useCurrentSession.ts";

function AccountInfoPanel({ account }: { account: CurrentAccountResponse }) {
  return (
    <section class="page-section" aria-label="Account information">
      <h2 class="text-lg font-semibold text-gray-900">アカウント情報</h2>
      <dl class="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        <dt class="text-gray-500">ユーザー名</dt>
        <dd>{account.username}</dd>
        <dt class="text-gray-500">role</dt>
        <dd><code class="rounded bg-gray-100 px-1">{account.role}</code></dd>
        <dt class="text-gray-500">realm</dt>
        <dd>{account.realm}</dd>
        <dt class="text-gray-500">status</dt>
        <dd>{account.status ?? "—"}</dd>
        <dt class="text-gray-500">active</dt>
        <dd>{String(account.active)}</dd>
        <dt class="text-gray-500">approve</dt>
        <dd>{String(account.approve)}</dd>
      </dl>
    </section>
  );
}

function PasswordChangeForm({ token }: { token: string }) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError(null);
    setNotice(null);
    if (newPassword !== confirmPassword) {
      setError("新しいパスワードが一致しません。");
      return;
    }
    setBusy(true);
    try {
      const result = await changeOwnPassword(token, currentPassword, newPassword);
      if (!result.success) {
        setError(result.errors?.[0]?.message ?? "パスワード変更に失敗しました。");
        return;
      }
      setNotice(
        `パスワードを変更しました。全セッション（${result.sessionsRevoked ?? 0}件）が無効化されたため、再ログインが必要です。移動します…`,
      );
      // The change already revoked every server-side session (backend-authoritative), so the token
      // this browser is holding is now dead. Clear every client-side carrier (sessionStorage, JS-
      // visible cookie, and the httpOnly refresh cookie via /api/auth/logout) and send the user back
      // to /auth — never keep operating as if the just-invalidated token were still usable.
      try {
        await logoutUser();
      } finally {
        clearSessionToken();
      }
      globalThis.location.href = "/auth";
    } catch (err) {
      setError(err instanceof Error ? err.message : "パスワード変更に失敗しました。");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section class="page-section" aria-label="Change password">
      <h2 class="text-lg font-semibold text-gray-900">パスワード変更</h2>
      <p class="mt-1 text-xs text-gray-500">
        変更すると全セッションが無効化され、再ログインが必要になります。
      </p>
      <form onSubmit={handleSubmit} class="mt-3 flex max-w-sm flex-col gap-2">
        <label class="text-sm">
          現在のパスワード
          <input
            type="password"
            value={currentPassword}
            onInput={(e) => setCurrentPassword((e.target as HTMLInputElement).value)}
            required
          />
        </label>
        <label class="text-sm">
          新しいパスワード（8文字以上）
          <input
            type="password"
            value={newPassword}
            onInput={(e) => setNewPassword((e.target as HTMLInputElement).value)}
            minLength={8}
            required
          />
        </label>
        <label class="text-sm">
          新しいパスワード（確認）
          <input
            type="password"
            value={confirmPassword}
            onInput={(e) => setConfirmPassword((e.target as HTMLInputElement).value)}
            minLength={8}
            required
          />
        </label>
        <button type="submit" class="btn-primary mt-2" disabled={busy}>
          {busy ? "変更中…" : "パスワードを変更"}
        </button>
        {notice && <div class="md-dashboard-action-notice" role="status">{notice}</div>}
        {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      </form>
    </section>
  );
}

function SessionListPanel({ token }: { token: string }) {
  const [sessions, setSessions] = useState<SessionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listOwnSessions(token);
      if (!result.success) {
        setError(result.errors?.[0]?.message ?? "セッション一覧の取得に失敗しました。");
        return;
      }
      setSessions(result.sessions ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "セッション一覧の取得に失敗しました。");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const handleRevoke = async (sessionId: string) => {
    setNotice(null);
    setError(null);
    try {
      const result = await revokeOwnSession(token, sessionId);
      if (!result.success) {
        setError(result.errors?.[0]?.message ?? "セッションの無効化に失敗しました。");
        return;
      }
      setNotice(`セッション ${sessionId} を無効化しました。`);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "セッションの無効化に失敗しました。");
    }
  };

  const handleRevokeOthers = async () => {
    setNotice(null);
    setError(null);
    try {
      const result = await revokeOtherSessions(token);
      if (!result.success) {
        setError(result.errors?.[0]?.message ?? "他セッションの無効化に失敗しました。");
        return;
      }
      setNotice(`他のセッション ${result.sessionsRevoked ?? 0} 件を無効化しました。`);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : "他セッションの無効化に失敗しました。");
    }
  };

  return (
    <section class="page-section" aria-label="Session management">
      <h2 class="text-lg font-semibold text-gray-900">セッション管理</h2>
      <button type="button" class="mt-2" onClick={() => void handleRevokeOthers()}>
        自分以外の全セッションを無効化
      </button>
      {loading && <p class="text-muted">読み込み中…</p>}
      {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      {notice && <div class="md-dashboard-action-notice" role="status">{notice}</div>}
      {!loading && sessions.length === 0 && <p class="text-muted">有効なセッションはありません。</p>}
      <ul class="mt-2 space-y-2">
        {sessions.map((s) => (
          <li key={s.sessionId} class="flex items-center justify-between gap-2 border-b border-gray-100 py-1 text-sm">
            <span>
              {s.realm} / {s.audience} — 作成: {s.createdAt} / 期限: {s.expiresAt}
              {s.isCurrent && <strong class="ml-2">（現在のセッション）</strong>}
            </span>
            <button type="button" onClick={() => void handleRevoke(s.sessionId)}>無効化</button>
          </li>
        ))}
      </ul>
    </section>
  );
}

export default function PersonalPage() {
  const session = useCurrentSession();
  const [account, setAccount] = useState<CurrentAccountResponse | null>(null);
  const [accountError, setAccountError] = useState<string | null>(null);

  useEffect(() => {
    if (session.status !== "present") return;
    void (async () => {
      try {
        const result = await getCurrentAccount(session.token);
        if (!result.success) {
          setAccountError(result.errors?.[0]?.message ?? "アカウント情報の取得に失敗しました。");
          return;
        }
        setAccount(result);
      } catch (err) {
        setAccountError(err instanceof Error ? err.message : "アカウント情報の取得に失敗しました。");
      }
    })();
  }, [session.status]);

  if (session.status !== "present") {
    return <p class="text-muted">ログイン状態を確認しています...</p>;
  }

  return (
    <div class="page-main-wide flex flex-col gap-6">
      <h1 class="text-xl font-bold text-gray-900">個人ページ</h1>
      {accountError && <div class="md-dashboard-error" role="alert">{accountError}</div>}
      {account && <AccountInfoPanel account={account} />}
      <PasswordChangeForm token={session.token} />
      <SessionListPanel token={session.token} />
    </div>
  );
}
