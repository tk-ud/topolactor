import { authenticatedGateHandler } from "../../lib/authenticatedGate.ts";

/** /account/* — authenticated (Normal or admin) SSR gate. See lib/authenticatedGate.ts. */
export const handler = authenticatedGateHandler;
