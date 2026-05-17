import { Handlers } from "$fresh/server.ts";

/**
 * GET  /api/admin/context-token-registry       — list active tokens (skeleton: returns seed list)
 * POST /api/admin/context-token-registry       — create new token  (skeleton: echoes back)
 *
 * In-memory skeleton.  Production implementation calls the C# backend
 * which reads/writes context_token_registry.
 */

type TokenStatus = "active" | "deprecated";

type ContextToken = {
  tokenId: string;
  label: string;
  group: string | null;
  value: number;
  status: TokenStatus;
};

const SEED_TOKENS: ContextToken[] = [
  { tokenId: "00000000-0000-0000-0001-000000000001", label: "正常",    group: "status", value:  1.0, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000002", label: "注意",    group: "status", value:  0.0, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000003", label: "警戒",    group: "status", value: -0.5, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000004", label: "取引禁止", group: "status", value: -1.0, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000005", label: "完了",    group: "task",   value:  1.0, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000006", label: "進行",    group: "task",   value:  0.5, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000007", label: "保留",    group: "task",   value:  0.0, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000008", label: "中止",    group: "task",   value: -0.5, status: "active" },
  { tokenId: "00000000-0000-0000-0001-000000000009", label: "失敗",    group: "task",   value: -1.0, status: "active" },
];

export const handler: Handlers = {
  GET(_req) {
    return Response.json(SEED_TOKENS);
  },

  async POST(req) {
    let body: unknown;
    try {
      body = await req.json();
    } catch {
      return Response.json({ ok: false, message: "Invalid JSON body." }, { status: 400 });
    }

    if (
      typeof body !== "object" ||
      body === null ||
      typeof (body as Record<string, unknown>).label !== "string" ||
      typeof (body as Record<string, unknown>).value !== "number"
    ) {
      return Response.json(
        { ok: false, message: "label (string) and value (number) are required." },
        { status: 400 },
      );
    }

    const tokenId = crypto.randomUUID();

    // In-memory skeleton: no-op persist.
    // Production: INSERT INTO context_token_registry ...
    return Response.json({
      ok: true,
      message: "Token accepted (skeleton — not persisted to DB).",
      tokenId,
    });
  },
};
