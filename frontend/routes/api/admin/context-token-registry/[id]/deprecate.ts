import { Handlers } from "$fresh/server.ts";
import pg from "npm:pg";
const { Pool } = pg;
const pool = new Pool({ connectionString: Deno.env.get("DATABASE_URL") });

export const handler: Handlers = {
  async POST(_req, ctx) {
    const id = ctx.params.id;
    if (!id) return Response.json({ ok: false, message: "token_id is required" }, { status: 400 });
    const client = await pool.connect();
    try {
      const result = await client.query(
        `UPDATE context_token_registry SET status='deprecated', updated_at=now() WHERE token_id=$1 RETURNING token_id`,
        [id],
      );
      if (result.rowCount === 0) return Response.json({ ok: false, message: "not found" }, { status: 404 });
      return Response.json({ ok: true, message: "deprecated" });
    } finally { client.release(); }
  },
};
