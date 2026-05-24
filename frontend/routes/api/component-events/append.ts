import { Handlers } from "$fresh/server.ts";

export const handler: Handlers = {
  async POST(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
    if (!backendUrl) {
      return Response.json({ success: false, accepted: 0, errors: [{ code: "COMPONENT_EVENT_BACKEND_NOT_CONFIGURED", message: "DEMO_BACKEND_URL is not set" }] }, { status: 501 });
    }
    try {
      const body = await req.text();
      const response = await fetch(`${backendUrl}/component-events/append`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body,
      });
      const json: unknown = await response.json();
      return Response.json(json, { status: response.status });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json({ success: false, accepted: 0, errors: [{ code: "COMPONENT_EVENT_BACKEND_UNREACHABLE", message }] }, { status: 502 });
    }
  },
};
