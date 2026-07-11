import { Handlers } from "$fresh/server.ts";
import { JSX } from "preact";
import AdminCredentialsShell from "../../../islands/AdminCredentialsShell.tsx";

/**
 * Canonical credential/admin-user management projection entry (owner decision, fixed —
 * admin-surface-topology-seed-conversion). Renders the existing manifest-092
 * (auth.external.credential_management.projection) ProjectionShell projection, which already
 * carries the user_auth / external / instance_settings category switcher plus (per this Bundle's
 * change) the embedded admin_user category. This route does NOT own auth.users canonical data
 * authority — see docs/design/admin-master-roster-management-ssot.yaml
 * projection_entry_vs_data_authority_split.
 *
 * Server-redirects to an explicit ?manifest=<092 uuid> selection when absent, so this route is a
 * self-contained entry contract and does not implicitly depend on the separate
 * canonical_default_entry hubs.hub_relations marker (docs/design/runtime-orchestration-ssot.yaml
 * canonical_default_entry_contract) coincidentally resolving the same manifest today — that
 * marker's target could change independently of this route in the future.
 */
const CREDENTIAL_MANAGEMENT_MANIFEST_ID =
  "00000000-0000-0000-0000-000000000092";

export const handler: Handlers = {
  GET(req, ctx) {
    const url = new URL(req.url);
    if (!url.searchParams.has("manifest")) {
      url.searchParams.set("manifest", CREDENTIAL_MANAGEMENT_MANIFEST_ID);
      return new Response(null, {
        status: 302,
        headers: { Location: url.pathname + url.search },
      });
    }
    return ctx.render();
  },
};

export default function AdminCredentials(): JSX.Element {
  return (
    <main class="page-main max-w-4xl font-sans">
      <h1 class="page-title">認証情報 / ユーザー管理</h1>
      <p class="mb-4 text-sm text-gray-600">
        user_auth（既存ユーザー認証の参照専用表示）、external（外部連携の認証情報）、
        instance_settings（DB /
        ランタイム接続設定）、admin_user（管理ユーザーCRUD）を
        カテゴリ切り替えで表示します。
      </p>

      <section class="mb-8">
        <AdminCredentialsShell />
      </section>
    </main>
  );
}
