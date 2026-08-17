import { JSX } from "preact";
import AdminAuthGate from "../../../islands/AdminAuthGate.tsx";
import { TeamMarkdownAuthoring } from "../../../islands/TeamMarkdownDashboard.tsx";

/**
 * /admin/team-markdown — Team Markdown Dashboard Saved View admin authoring surface.
 * Moved here (was /admin/team-dashboard) by the team-dashboard subBundle design_change
 * (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.
 * dashboard.superseding_authority_note): "team-dashboard" now names the new lightweight shared
 * dashboard surface (surface_axes.admin.surfaces.team_dashboard), not this feature. Team Markdown
 * Dashboard Saved View itself is unchanged and unaffected — same TeamMarkdownAuthoring component
 * (preserving the round-3 confirmed-write atomicity fix), same admin-only gate, only the route
 * file's own path moved.
 */
export default function AdminTeamMarkdownRoute(): JSX.Element {
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
