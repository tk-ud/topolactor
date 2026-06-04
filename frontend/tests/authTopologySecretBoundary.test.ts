import { assert } from "https://deno.land/std@0.208.0/assert/mod.ts";

const FORBIDDEN = [
  /password_hash/i,
  /jwt_secret/i,
  /"refresh_token"\s*:/i,
  /DEMO_JWT_SECRET/i,
];

const SCAN_FILES = [
  new URL("../../db/seed_empty.sql", import.meta.url),
  new URL("../../db/demo_seed.sql", import.meta.url),
];

function lineAllowed(fileLabel: string, line: string): boolean {
  if (fileLabel.includes("demo_seed.sql") && line.includes("demo_auth")) {
    return !/password_hash/i.test(line);
  }
  return false;
}

for (const fileUrl of SCAN_FILES) {
  const label = fileUrl.pathname;
  Deno.test(`auth boundary: no secrets in ${label}`, async () => {
    const text = await Deno.readTextFile(fileUrl);
    for (const [i, line] of text.split("\n").entries()) {
      for (const pattern of FORBIDDEN) {
        if (!pattern.test(line)) continue;
        if (lineAllowed(label, line)) continue;
        throw new Error(
          `Forbidden pattern ${pattern} at ${label}:${i + 1}: ${line.trim()}`,
        );
      }
    }
  });
}

Deno.test("auth boundary: demo_auth marker has no password_hash in demo_seed", async () => {
  const text = await Deno.readTextFile(
    new URL("../../db/demo_seed.sql", import.meta.url),
  );
  const block = text.slice(text.indexOf("demo_auth"));
  assert(!/password_hash/.test(block), "demo_auth block must not contain password_hash");
});
