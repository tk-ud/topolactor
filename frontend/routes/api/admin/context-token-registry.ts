import { Handlers } from "$fresh/server.ts";
import pg from "npm:pg";

const { Pool } = pg;
const pool = new Pool({ connectionString: Deno.env.get("DATABASE_URL") });

export const handler: Handlers = {
  async GET() {
    const client = await pool.connect();
    try {
      const result = await client.query(`SELECT token_id, label, "group", value, status FROM context_token_registry ORDER BY created_at DESC`);
      return Response.json(result.rows.map((r) => ({ tokenId: r.token_id, label: r.label, group: r.group, value: Number(r.value), status: r.status })));
    } finally { client.release(); }
  },
  async POST(req) {
    const body = await req.json();
    const label = String(body?.label ?? "").trim();
    const value = Number(body?.value);
    const group = body?.group == null || body?.group === "" ? null : String(body.group);
    if (!label || !Number.isFinite(value) || value < -1 || value > 1) {
      return Response.json({ ok: false, message: "label と value（-1.0〜1.0）は必須です。" }, { status: 400 });
    }
    const client = await pool.connect();
    try {
      const result = await client.query(
        `INSERT INTO context_token_registry(label, "group", value, status) VALUES ($1,$2,$3,'active') RETURNING token_id`,
        [label, group, value],
      );
      return Response.json({ ok: true, message: "created", tokenId: result.rows[0].token_id });
    } catch {
      return Response.json({ ok: false, message: "create failed" }, { status: 400 });
    } finally { client.release(); }
  },
};
