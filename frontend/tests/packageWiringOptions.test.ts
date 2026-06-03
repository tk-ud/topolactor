import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  mergeWiringKindSuggestions,
  PACKAGE_WIRING_TARGET_SURFACES,
} from "../lib/packageWiringOptions.ts";

Deno.test("PACKAGE_WIRING_TARGET_SURFACES includes route and ui", () => {
  assertEquals(PACKAGE_WIRING_TARGET_SURFACES.includes("route"), true);
  assertEquals(PACKAGE_WIRING_TARGET_SURFACES.includes("ui"), true);
});

Deno.test("mergeWiringKindSuggestions dedupes component kinds", () => {
  const kinds = mergeWiringKindSuggestions(["button", "button", "card"]);
  assertEquals(kinds.includes("button"), true);
  assertEquals(kinds.includes("card"), true);
  assertEquals(kinds.filter((k) => k === "button").length, 1);
});
