import ContextTokenRegistryEditor from "../../islands/ContextTokenRegistryEditor.tsx";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import AdminHowTo from "../../components/AdminHowTo.tsx";
import AdminHelpPanel from "../../components/AdminHelpPanel.tsx";
import { ADMIN_CONTEXT_TOKEN_GUIDE } from "../../content/adminGuides.ts";

export default function ContextTokenRegistryPage() {
  return (
    <AdminAuthGate>
      <main class="page-main-wide font-mono">
        <h1 class="page-title">context_token_registry — ハブ Registry（トークン管理）</h1>
        <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

        <AdminHowTo
          steps={ADMIN_CONTEXT_TOKEN_GUIDE.howToSteps}
          prerequisites={ADMIN_CONTEXT_TOKEN_GUIDE.prerequisites}
        />
        <AdminHelpPanel {...ADMIN_CONTEXT_TOKEN_GUIDE} />

        <p class="mb-6 text-muted text-sm">
          操作には <a href="/auth" class="link">ログイン</a>（JWT）が必要です。
          DEMO_BACKEND_URL と DATABASE_URL 設定時のみ DB 永続化されます。
        </p>

        <hr class="mb-6 border-gray-200" />

        <ContextTokenRegistryEditor />

        <hr class="my-6 border-gray-200" />
        <p><a href="/admin" class="link">&larr; 管理トップへ</a></p>
      </main>
    </AdminAuthGate>
  );
}
