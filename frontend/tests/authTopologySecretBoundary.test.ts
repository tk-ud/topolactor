import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";

const FORBIDDEN = [
  /password_hash/i,
  /jwt_secret/i,
  /"refresh_token"\s*:/i,
  /DEMO_JWT_SECRET/i,
];

const SCAN_FILES = [
  new URL("../../db/seed_empty.sql", import.meta.url),
];

for (const fileUrl of SCAN_FILES) {
  const label = fileUrl.pathname;
  Deno.test(`auth boundary: no secrets in ${label}`, async () => {
    const text = await Deno.readTextFile(fileUrl);
    for (const [i, line] of text.split("\n").entries()) {
      for (const pattern of FORBIDDEN) {
        if (!pattern.test(line)) continue;
        throw new Error(
          `Forbidden pattern ${pattern} at ${label}:${i + 1}: ${line.trim()}`,
        );
      }
    }
  });
}

Deno.test("auth boundary: seed_empty.sql contains no password_hash or jwt_secret", async () => {
  const text = await Deno.readTextFile(
    new URL("../../db/seed_empty.sql", import.meta.url),
  );
  assert(!/password_hash/.test(text), "seed_empty.sql must not contain password_hash");
  assert(!/jwt_secret/i.test(text), "seed_empty.sql must not contain jwt_secret");
});
