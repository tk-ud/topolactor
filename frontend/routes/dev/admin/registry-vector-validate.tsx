import RegistryVectorValidator from "../../../islands/RegistryVectorValidator.tsx";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import AdminHowTo from "../../../components/AdminHowTo.tsx";
import AdminHelpPanel from "../../../components/AdminHelpPanel.tsx";
import { ADMIN_REGISTRY_VECTOR_GUIDE } from "../../../content/adminGuides.ts";

export default function RegistryVectorValidatePage() {
  return (
    <AdminAuthGate>
      <main class="page-main-wide font-mono">
        <h1 class="page-title">レジストリベクター検証</h1>
        <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

        <AdminHowTo
          steps={ADMIN_REGISTRY_VECTOR_GUIDE.howToSteps}
          prerequisites={ADMIN_REGISTRY_VECTOR_GUIDE.prerequisites}
        />
        <AdminHelpPanel {...ADMIN_REGISTRY_VECTOR_GUIDE} />

        <RegistryVectorValidator />
      </main>
    </AdminAuthGate>
  );
}
