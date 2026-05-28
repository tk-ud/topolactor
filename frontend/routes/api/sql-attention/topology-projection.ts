import { Handlers } from "$fresh/server.ts";

export const handler: Handlers = {
  async GET(req) {
    const backendUrl = Deno.env.get("DEMO_BACKEND_URL");
    if (!backendUrl) {
      return Response.json({ success: false, errors: [{ code: "SQL_ATTENTION_BACKEND_NOT_CONFIGURED", message: "DEMO_BACKEND_URL is not set." }] }, { status: 501 });
    }
    try {
      const url = new URL(req.url);
      const sourceSetId = url.searchParams.get("sourceSetId") ?? "";
      const authHeader = req.headers.get("Authorization");
      const headers: Record<string, string> = {};
      if (authHeader) headers["Authorization"] = authHeader;
      const response = await fetch(`${backendUrl}/sql-attention/topology-projection?sourceSetId=${encodeURIComponent(sourceSetId)}`, { headers });
      const json: unknown = await response.json();
      return Response.json(json, { status: response.status });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      return Response.json({ success: false, errors: [{ code: "SQL_ATTENTION_BACKEND_UNREACHABLE", message }] }, { status: 502 });
    }
  },
};
