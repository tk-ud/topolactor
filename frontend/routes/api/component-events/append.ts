import { Handlers } from "$fresh/server.ts";

type AppendRequest = {
  events?: Array<Record<string, unknown>>;
};

export const handler: Handlers = {
  async POST(req) {
    const body = (await req.json()) as AppendRequest;
    if (!Array.isArray(body.events)) {
      return Response.json({ success: false, error: "COMPONENT_EVENT_APPEND_INVALID_EVENTS" }, { status: 400 });
    }
    return Response.json({ success: true, accepted: body.events.length }, { status: 202 });
  },
};
