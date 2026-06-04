import { FreshContext } from "$fresh/server.ts";
import {
  buildAuthRedirectResponse,
  getSessionTokenFromRequest,
  isDemoSessionPresent,
} from "../../lib/demoSession.ts";
import { probeDemoSessionOnBackend } from "../../lib/demoSessionValidate.ts";

/**
 * /admin/* SSR demo session gate (Registrar admin UI boundary).
 *
 * - Missing/empty cookie → redirect to /super_auth?redirect=...
 * - Non-empty cookie → probe backend GET /auth/session; invalid/unverifiable → clear cookie + redirect
 * - Valid JWT (signature, exp, sub, role) → pass through to route render
 */
export async function handler(req: Request, ctx: FreshContext) {
  if (ctx.destination !== "route") {
    return await ctx.next();
  }

  const token = getSessionTokenFromRequest(req);
  if (!isDemoSessionPresent(token)) {
    return buildAuthRedirectResponse(req);
  }

  const valid = await probeDemoSessionOnBackend(token!);
  if (!valid) {
    return buildAuthRedirectResponse(req, { clearSession: true });
  }

  return await ctx.next();
}
