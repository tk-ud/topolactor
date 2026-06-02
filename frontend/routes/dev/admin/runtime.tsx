import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import OperationPanel from "../../../islands/OperationPanel.tsx";
import AdminHowTo from "../../../components/AdminHowTo.tsx";
import AdminHelpPanel from "../../../components/AdminHelpPanel.tsx";
import { ADMIN_RUNTIME_GUIDE } from "../../../content/adminGuides.ts";

/**
 * /dev/admin/runtime — 開発者向け runtime dispatch 検証（Registrar 登録 UI とは別経路）。
 * SSOT: registrar-admin-ui-specification.md — admin は登録境界; 本画面は promoted topology の動作確認。
 */
export default function AdminRuntimePage(): JSX.Element {
  return (
    <AdminAuthGate>
      <main class="page-main max-w-3xl font-sans">
        <h1 class="page-title">topolactor — 管理 / ランタイム検証</h1>
        <p class="mb-4">
          <a href="/admin" class="link">&larr; 管理インデックス</a>
        </p>

        <AdminHowTo
          steps={ADMIN_RUNTIME_GUIDE.howToSteps}
          prerequisites={ADMIN_RUNTIME_GUIDE.prerequisites}
        />
        <AdminHelpPanel {...ADMIN_RUNTIME_GUIDE} />

        <hr class="mb-6 border-gray-200" />

        <OperationPanel mode="home" initialPresetId="default_entity_search" />
      </main>
    </AdminAuthGate>
  );
}
