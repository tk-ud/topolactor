import { FreshContext } from "$fresh/server.ts";
import {
  buildAuthRedirectUrl,
  hasDemoSessionPresenceFromRequest,
} from "../../lib/demoSession.ts";

/**
 * /admin/* SSR demo session **presence gate** (Registrar admin UI boundary).
 *
 * - Passes when `demo_jwt_token` cookie parses to a non-empty string.
 * - Does NOT validate JWT / authz — arbitrary non-empty values are not "pre-authenticated".
 * - Missing/empty cookie → fail-close redirect to /auth?redirect=...
 * - Token validity and operation authz remain backend `Authorization` responsibility.
 */
export async function handler(req: Request, ctx: FreshContext) {
  if (ctx.destination !== "route") {
    return await ctx.next();
  }

  if (hasDemoSessionPresenceFromRequest(req)) {
    return await ctx.next();
  }

  return Response.redirect(buildAuthRedirectUrl(req), 302);
}
