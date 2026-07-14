import { authenticatedGateHandler } from "../../lib/authenticatedGate.ts";

/** /dashboard/* — authenticated (Normal or admin) SSR gate. See lib/authenticatedGate.ts. */
export const handler = authenticatedGateHandler;
