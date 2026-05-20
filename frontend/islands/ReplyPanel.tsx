import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type AuthState = "loading" | "authenticated" | "unauthenticated";

type ReplyState =
  | { status: "idle" }
  | { status: "submitting" }
  | { status: "submitted"; message: string }
  | { status: "error"; message: string };

/**
 * Component-level auth guard demo.
 *
 * Demonstrates the component-level auth guard pattern:
 * - Unauthenticated: shows "Login to reply" link pointing to /auth
 * - Authenticated: shows reply form
 *
 * Auth state is determined by demo_jwt_token presence in sessionStorage.
 * This is a demo scaffold only; not for production use.
 */
export default function ReplyPanel(): JSX.Element {
  const [authState, setAuthState] = useState<AuthState>("loading");
  const [replyText, setReplyText] = useState("");
  const [replyState, setReplyState] = useState<ReplyState>({ status: "idle" });

  useEffect(() => {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
    setAuthState(token ? "authenticated" : "unauthenticated");
  }, []);

  async function handleReplySubmit(e: JSX.TargetedEvent<HTMLFormElement, Event>) {
    e.preventDefault();
    if (!replyText.trim()) return;

    setReplyState({ status: "submitting" });

    await new Promise((r) => setTimeout(r, 400));
    setReplyState({ status: "submitted", message: replyText });
    setReplyText("");
  }

  if (authState === "loading") {
    return <div style={{ color: "#888", fontSize: "0.9em" }}>Loading…</div>;
  }

  if (authState === "unauthenticated") {
    return (
      <div style={{ padding: "12px", background: "#f5f5f5", border: "1px solid #ddd", borderRadius: "4px" }}>
        <p style={{ margin: "0 0 8px" }}>
          返信するにはログインが必要です。
        </p>
        <a
          href="/auth"
          style={{
            display: "inline-block",
            padding: "6px 16px",
            background: "#1976d2",
            color: "#fff",
            textDecoration: "none",
            borderRadius: "4px",
            fontWeight: "bold",
          }}
        >
          ログインして返信する → /auth
        </a>
      </div>
    );
  }

  return (
    <div style={{ padding: "12px", background: "#f9f9f9", border: "1px solid #ddd", borderRadius: "4px" }}>
      <h3 style={{ margin: "0 0 12px" }}>返信する</h3>

      {replyState.status === "submitted" && (
        <div style={{ marginBottom: "12px", padding: "8px", background: "#e6f9e6", border: "1px solid #4caf50", borderRadius: "4px" }}>
          返信を送信しました: <em>{replyState.message}</em>
        </div>
      )}

      {replyState.status === "error" && (
        <div style={{ marginBottom: "12px", padding: "8px", background: "#fdecea", border: "1px solid #f44336", borderRadius: "4px" }}>
          エラー: {replyState.message}
        </div>
      )}

      <form onSubmit={handleReplySubmit} style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
        <textarea
          value={replyText}
          onInput={(e) => setReplyText((e.target as HTMLTextAreaElement).value)}
          placeholder="返信内容を入力…"
          rows={3}
          style={{ padding: "8px", fontFamily: "inherit", resize: "vertical" }}
          disabled={replyState.status === "submitting"}
        />
        <button
          type="submit"
          disabled={replyState.status === "submitting" || !replyText.trim()}
          style={{ alignSelf: "flex-start", padding: "6px 16px" }}
        >
          {replyState.status === "submitting" ? "送信中…" : "返信する"}
        </button>
      </form>
    </div>
  );
}
