import { FreshContext } from "$fresh/server.ts";
import {
  buildAuthPageRedirectResponse,
  getSessionTokenFromRequest,
  isDemoSessionPresent,
} from "./demoSession.ts";
import { probeAuthenticatedSessionOnBackend } from "./demoSessionValidate.ts";

/**
 * Shared SSR gate for authenticated (not admin-only) route subtrees — Normal dashboard, Team
 * Dashboard, Personal Page. Any valid, unexpired JWT passes regardless of role; role-based
 * component visibility is decided client-side (display only) and mutation authority is always
 * re-checked backend-side per operation, never inferred from this gate.
 *
 * - Missing/empty cookie -> redirect to /auth?redirect=...
 * - Non-empty cookie -> probe backend GET /auth/session (no ?expected=); invalid -> clear cookie + redirect
 * - Valid JWT -> pass through to route render
 */
export async function authenticatedGateHandler(req: Request, ctx: FreshContext) {
  if (ctx.destination !== "route") {
    return await ctx.next();
  }

  const token = getSessionTokenFromRequest(req);
  if (!isDemoSessionPresent(token)) {
    return buildAuthPageRedirectResponse(req);
  }

  const valid = await probeAuthenticatedSessionOnBackend(token!);
  if (!valid) {
    return buildAuthPageRedirectResponse(req, { clearSession: true });
  }

  return await ctx.next();
}
