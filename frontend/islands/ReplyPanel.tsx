import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type AuthState = "loading" | "authenticated" | "unauthenticated";

type ReplyState =
  | { status: "idle" }
  | { status: "submitting" }
  | { status: "submitted"; message: string }
  | { status: "error"; message: string };

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
    return <div class="text-sm text-gray-500">読み込み中...</div>;
  }

  if (authState === "unauthenticated") {
    return (
      <div class="card">
        <p class="mb-2">返信するにはログインが必要です。</p>
        <a href="/auth" class="btn-primary">ログインして返信する → /auth</a>
      </div>
    );
  }

  return (
    <div class="card">
      <h3 class="mb-3 font-semibold">返信する</h3>

      {replyState.status === "submitted" && (
        <div class="alert-success mb-3">
          返信を送信しました: <em>{replyState.message}</em>
        </div>
      )}

      {replyState.status === "error" && (
        <div class="alert-error mb-3">エラー: {replyState.message}</div>
      )}

      <form onSubmit={handleReplySubmit} class="flex flex-col gap-2">
        <textarea
          value={replyText}
          onInput={(e) => setReplyText((e.target as HTMLTextAreaElement).value)}
          placeholder="返信内容を入力..."
          rows={3}
          class="input resize-y"
          disabled={replyState.status === "submitting"}
        />
        <button
          type="submit"
          disabled={replyState.status === "submitting" || !replyText.trim()}
          class="btn-primary self-start"
        >
          {replyState.status === "submitting" ? "送信中..." : "返信する"}
        </button>
      </form>
    </div>
  );
}
