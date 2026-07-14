import { Handlers } from "$fresh/server.ts";

/**
 * /admin/team-dashboard — legacy compat redirect to the canonical authenticated route
 * /dashboard/team (see routes/dashboard/team.tsx). Kept as a redirect rather than deleted per
 * the repo's route-retirement rule (render/action proof and seed conversion must land before a
 * route is actually retired); retire this file only after that proof lands in a follow-up PR.
 */
export const handler: Handlers = {
  GET(req) {
    const url = new URL(req.url);
    const target = new URL("/dashboard/team", url.origin);
    target.search = url.search;
    return new Response(null, { status: 302, headers: { Location: target.toString() } });
  },
};
