import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { buildInlineStyleFromCssTokenRefs } from "../runtime/cssDictionary.ts";

Deno.test("buildInlineStyleFromCssTokenRefs maps token keys to CSS properties", () => {
  const style = buildInlineStyleFromCssTokenRefs([
    "color.action.primary.background",
    "color.action.primary.text",
    "radius.control.sm",
  ]);
  assertEquals(style.background, "#0070f3");
  assertEquals(style.color, "#fff");
  assertEquals(style["border-radius"], "4px");
});

Deno.test("buildInlineStyleFromCssTokenRefs ignores unknown token keys", () => {
  const style = buildInlineStyleFromCssTokenRefs([
    "color.action.primary.background",
    "unknown.token.key",
  ]);
  assertEquals(style.background, "#0070f3");
  assertEquals(Object.keys(style).length, 1);
});
