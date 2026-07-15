import { useEffect, useState } from "preact/hooks";
import { getTokenClaims, probeSessionToken } from "../api/authApi.ts";
import { ensureValidClientSession } from "../lib/demoSession.ts";

export type CurrentSessionState =
  | { status: "loading" }
  | { status: "absent" }
  | { status: "present"; token: string; subject: string | null; role: string | null };

/**
 * Client-side session probe + role/subject read for component VISIBILITY only.
 *
 * `role` here comes from an unverified client-side JWT payload decode (getTokenClaims) — it is
 * used exclusively to show/hide authoring UI. It must never be treated as mutation authority:
 * every backend mutation operation re-derives and re-checks the caller's role from the
 * server-verified JWT independently (see backend/schema/Contracts.cs OperationVector.AuthenticatedRole),
 * so a tampered client-side token cannot grant write access even if it changes what renders here.
 */
export function useCurrentSession(): CurrentSessionState {
  const [state, setState] = useState<CurrentSessionState>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const token = await ensureValidClientSession((t) => probeSessionToken(t));
      if (cancelled) return;
      if (!token) {
        setState({ status: "absent" });
        return;
      }
      const claims = getTokenClaims(token);
      setState({
        status: "present",
        token,
        subject: typeof claims?.sub === "string" ? claims.sub : null,
        role: typeof claims?.role === "string" ? claims.role : null,
      });
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
