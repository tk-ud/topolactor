import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";

Deno.test("UiBuilderAdmin exposes package wiring editor and dispatch axes", async () => {
  const source = await Deno.readTextFile(
    new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
  );
  assertEquals(source.includes("パッケージ配線（編集）"), true);
  assertEquals(source.includes("get_package_wiring"), true);
  assertEquals(source.includes("update_package_wiring"), true);
  assertEquals(source.includes("パッケージ配線（参照）"), false);
});
