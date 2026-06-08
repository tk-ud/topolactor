import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import TeamMarkdownDashboard from "../../../islands/TeamMarkdownDashboard.tsx";

/**
 * /admin/team-dashboard — routable Team Markdown Dashboard placement.
 * Projection-only surface for saved Markdown views; all data access goes through
 * TeamMarkdownDashboard -> teamMarkdownApi -> AdminRuntime team_markdown actions.
 */
export default function AdminTeamDashboardRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <main class="page-main-wide">
        <p class="mb-1">
          <a href="/admin" class="link">← 管理インデックスへ戻る</a>
        </p>
        <TeamMarkdownDashboard placement="admin_route" />
      </main>
    </AdminAuthGate>
  );
}
