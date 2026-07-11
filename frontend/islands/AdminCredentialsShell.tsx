import { useState } from "preact/hooks";
import { JSX } from "preact";
import ProjectionShell from "./ProjectionShell.tsx";
import AdminUsersRoster from "./AdminUsersRoster.tsx";

/**
 * /admin/credentials category shell (admin-surface-topology-seed-conversion, owner decision:
 * credential/admin-user projection entry consolidation).
 *
 * Two tabs:
 *   - "projection" (default): the existing manifest-092
 *     (auth.external.credential_management.projection) seed-backed ProjectionShell rendering —
 *     user_auth (readonly) / external / instance_settings categories, already switched by that
 *     projection's own select/mode/category field per
 *     docs/projection_design/credential-management-projection-design.md.
 *   - "admin_user": embeds the existing, already-production-wired AdminUsersRoster.tsx CRUD
 *     island (auth_users:* AdminRuntime actions — production dispatcher_mapping added by this
 *     Bundle in db/seed_empty.sql). This is an interim, honestly-not-yet-seed-backed embedding:
 *     making admin_user a true seed-authored category (like instance_settings) would require a
 *     new runtimeInteractions actionType capable of calling AdminRuntime layer:action axes
 *     directly, which does not exist in
 *     docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml today (only
 *     dispatchExternalPort/dispatchInstanceOperation/localStateMutation actionTypes exist) — that
 *     is a separate, cross-cutting runtime-vocabulary design_change this Bundle does not invent
 *     unilaterally. Tracked as remaining_gap in
 *     .agent/reports/admin-surface-topology-seed-conversion-design-resolution.json. Reusing the
 *     existing, already-verified AdminUsersRoster.tsx here (rather than leaving admin_user
 *     entirely absent from this route) is what satisfies the owner decision that /admin/credentials
 *     must not degrade to the user_auth-readonly-only boundary.
 *
 * auth.users canonical data authority is unaffected by this embedding — see
 * docs/design/admin-master-roster-management-ssot.yaml projection_entry_vs_data_authority_split.
 */
type CredentialsTab = "projection" | "admin_user";

export default function AdminCredentialsShell(): JSX.Element {
  const [tab, setTab] = useState<CredentialsTab>("projection");

  return (
    <div>
      <div class="flex gap-2 border-b border-gray-200 mb-4" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={tab === "projection"}
          class={`px-3 py-2 text-sm font-medium border-b-2 ${
            tab === "projection"
              ? "border-blue-600 text-blue-700"
              : "border-transparent text-gray-500 hover:text-gray-700"
          }`}
          onClick={() => setTab("projection")}
        >
          user_auth / external / instance_settings
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === "admin_user"}
          class={`px-3 py-2 text-sm font-medium border-b-2 ${
            tab === "admin_user"
              ? "border-blue-600 text-blue-700"
              : "border-transparent text-gray-500 hover:text-gray-700"
          }`}
          onClick={() => setTab("admin_user")}
        >
          admin_user（管理ユーザー）
        </button>
      </div>

      <div role="tabpanel" hidden={tab !== "projection"}>
        <ProjectionShell />
      </div>
      <div role="tabpanel" hidden={tab !== "admin_user"}>
        {tab === "admin_user" && <AdminUsersRoster />}
      </div>
    </div>
  );
}
