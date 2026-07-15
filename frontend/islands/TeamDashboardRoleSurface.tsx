/**
 * TeamDashboardRoleSurface — role-split composition for the authenticated /dashboard/team route.
 * Normal and admin sessions both get TeamMarkdownViewer; only admin sessions additionally get
 * TeamMarkdownAuthoring. `role` here is a client-side JWT decode used for display only (see
 * hooks/useCurrentSession.ts) — mutation authority is independently re-checked server-side on
 * every team_markdown action regardless of what this component renders.
 */
import { useCurrentSession } from "../hooks/useCurrentSession.ts";
import { TeamMarkdownAuthoring, TeamMarkdownViewer } from "./TeamMarkdownDashboard.tsx";

export default function TeamDashboardRoleSurface() {
  const session = useCurrentSession();

  if (session.status !== "present") {
    return (
      <main class="page-main-wide">
        <p class="text-muted">ログイン状態を確認しています...</p>
      </main>
    );
  }

  const isAdmin = session.role === "admin";

  return (
    <div class="page-main-wide">
      <TeamMarkdownViewer />
      {isAdmin && <TeamMarkdownAuthoring />}
    </div>
  );
}
