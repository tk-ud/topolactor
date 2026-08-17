import { JSX } from "preact";
import { useEffect, useState } from "preact/hooks";
import { renderMarkdownToVNodes } from "../lib/markdownRenderer.ts";
import { probeSessionToken } from "../api/authApi.ts";
import { ensureValidClientSession } from "../lib/demoSession.ts";

type TeamDashboardNote = {
  noteId: string;
  title: string;
  bodyMarkdown: string;
  updatedAt: string;
};

type LoadState =
  | { kind: "loading" }
  | { kind: "unauthenticated" }
  | { kind: "error"; message: string }
  | { kind: "ready"; note: TeamDashboardNote };

/**
 * Normal (read-only) team-dashboard viewer.
 *
 * SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.normal.surfaces.
 * dashboard.team_dashboard_canonical_shared_contract. Reads GET /team-dashboard/note (backend/
 * Program.cs — any authenticated JWT, not admin-gated), the SAME plain-REST-bypass-of-admin_runtime
 * pattern the pre-existing /team-markdown/* viewer endpoints already established (see that route's
 * own comment in backend/Program.cs for why: ManifestDispatcher.ResolveRequiredRole infers
 * required_role=admin for ANY manifest whose runtime_mapping.runtime_destination=admin_runtime,
 * with no per-action or explicit-open override, so a genuinely role-open dispatch through
 * ManifestDispatcher is not available for this — or any — seed-driven screen today).
 *
 * Renders through the SAME shared safe Markdown renderer (frontend/lib/markdownRenderer.ts,
 * physical_details_inline_editor_md_generator_preset / MdViewer.tsx's own renderer, PR #604) — no
 * bespoke Markdown handling. No edit/save control exists in this component at all (not merely
 * hidden) — the admin edit surface lives entirely at /admin/team-dashboard
 * (surface_axes.admin.surfaces.team_dashboard), a completely separate component tree.
 */
export default function TeamDashboardViewer(): JSX.Element {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    void (async () => {
      const token = await ensureValidClientSession((t) => probeSessionToken(t));
      if (!token) {
        setState({ kind: "unauthenticated" });
        return;
      }
      try {
        const response = await fetch("/team-dashboard/note", {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!response.ok) {
          setState({ kind: "error", message: `team-dashboard note request failed (${response.status})` });
          return;
        }
        const json = await response.json() as { success: boolean; note?: TeamDashboardNote };
        if (!json.success || !json.note) {
          setState({ kind: "error", message: "team-dashboard note response missing note" });
          return;
        }
        setState({ kind: "ready", note: json.note });
      } catch {
        setState({ kind: "error", message: "team-dashboard note request failed" });
      }
    })();
  }, []);

  if (state.kind === "loading") {
    return <p class="text-muted">読み込み中...</p>;
  }
  if (state.kind === "unauthenticated") {
    return (
      <p class="text-muted">
        閲覧にはログインが必要です。<a href="/auth" class="link">ログインページへ</a>
      </p>
    );
  }
  if (state.kind === "error") {
    return <p class="text-error">{state.message}</p>;
  }

  return (
    <article aria-label="Team dashboard" class="team-dashboard-viewer">
      <h1 class="page-title">{state.note.title}</h1>
      <div class="team-dashboard-viewer-body">{renderMarkdownToVNodes(state.note.bodyMarkdown)}</div>
    </article>
  );
}
