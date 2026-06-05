import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildWiringKindSelectOptions,
  encodeManifestPackageTargetRef,
  manifestIdFromTargetRef,
  manifestWiringKeyFromTargetRef,
  mergeManifestPickerOptions,
  parseManifestPackageTargetRef,
} from "../lib/packageWiringPicker.ts";

const SAMPLE_ID = "11111111-2222-3333-4444-555555555555";

Deno.test("encodeManifestPackageTargetRef: manifest id and wiring key", () => {
  assertEquals(
    encodeManifestPackageTargetRef(SAMPLE_ID, "screen.searchConditions[0]"),
    `manifest:${SAMPLE_ID}:screen.searchConditions[0]`,
  );
});

Deno.test("parseManifestPackageTargetRef: round-trip", () => {
  const encoded = `manifest:${SAMPLE_ID}:screen.displayColumnMode`;
  assertEquals(parseManifestPackageTargetRef(encoded), {
    manifestId: SAMPLE_ID,
    wiringKey: "screen.displayColumnMode",
  });
});

Deno.test("manifestWiringKeyFromTargetRef: legacy plain wiring key", () => {
  assertEquals(
    manifestWiringKeyFromTargetRef("screen.searchConditions[0]", "manifest"),
    "screen.searchConditions[0]",
  );
});

Deno.test("manifestIdFromTargetRef: encoded ref", () => {
  assertEquals(
    manifestIdFromTargetRef(`manifest:${SAMPLE_ID}:screen.list`, "manifest"),
    SAMPLE_ID,
  );
});

Deno.test("buildWiringKindSelectOptions: merges component kinds and candidates", () => {
  const options = buildWiringKindSelectOptions(
    ["action/button"],
    [{ wiringKey: "screen.searchConditions[0]", label: "search", field: "searchConditions", valueSourceKinds: [] }],
  );
  assertEquals(options.includes("action/button"), true);
  assertEquals(options.includes("screen.searchConditions[0]"), true);
});

Deno.test("mergeManifestPickerOptions: dedupes by manifestId", () => {
  const merged = mergeManifestPickerOptions(
    [{ manifestId: SAMPLE_ID, label: "A", status: "active" }],
    [{ manifestId: SAMPLE_ID, label: "B", status: "draft" }],
  );
  assertEquals(merged.length, 1);
  assertEquals(merged[0].label, "A");
});
