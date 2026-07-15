import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import { TeamMarkdownAuthoring } from "../../../islands/TeamMarkdownDashboard.tsx";

/**
 * /admin/team-dashboard — canonical Team Markdown Dashboard placement, admin-only.
 * 2026-07-15 gate0 audit: reverted from the PR589 /dashboard/team role-composition detour (which
 * had no Gate 0 grounding — see .agent/tasks/todo.md) back to this canonical route. Mounts
 * TeamMarkdownAuthoring (not the default-exported TeamMarkdownDashboard, whose Refresh/Clone/Rebind
 * handlers are the pre-round-3 stub notices) so the real confirmed-write atomicity fix from round 3
 * is preserved for admin use.
 */
export default function AdminTeamDashboardRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <main class="page-main-wide">
        <p class="mb-1">
          <a href="/admin" class="link">← 管理インデックスへ戻る</a>
        </p>
        <TeamMarkdownAuthoring />
      </main>
    </AdminAuthGate>
  );
}
