import { Handlers } from "$fresh/server.ts";

/**
 * /dashboard — redirects to the canonical authenticated landing surface /dashboard/team.
 * This surface has no business projection of its own (see docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * design_blocking.normal_dashboard_authoring_runtime_adapter — the former hardcoded landing page
 * (NormalDashboardHome.tsx) was removed per Gate 0 audit; auth gating for this subtree stays in
 * routes/dashboard/_middleware.ts, which also covers /dashboard/team).
 */
export const handler: Handlers = {
  GET(req) {
    const url = new URL(req.url);
    const target = new URL("/dashboard/team", url.origin);
    return new Response(null, { status: 302, headers: { Location: target.toString() } });
  },
};
