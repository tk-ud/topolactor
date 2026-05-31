import { FreshContext } from "$fresh/server.ts";
import {
  buildAuthRedirectUrl,
  getSessionTokenFromRequest,
} from "../../lib/demoSession.ts";

/**
 * /admin/* — unauthenticated requests redirect to /auth before route handlers run.
 * Token is read from the demo_jwt_token cookie (set on login alongside sessionStorage).
 */
export async function handler(req: Request, ctx: FreshContext) {
  if (ctx.destination !== "route") {
    return await ctx.next();
  }

  if (getSessionTokenFromRequest(req)) {
    return await ctx.next();
  }

  return Response.redirect(buildAuthRedirectUrl(req), 302);
}
