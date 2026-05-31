import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { resolveBucketStatus, type BucketItem } from "../runtime/bucketUtils.ts";

const base: BucketItem = {
  bucketItemId: "bid-1",
  componentKey: "display/card",
  sourcePath: "frontend/components/display/Card.tsx",
  componentKind: "primitive",
  status: "bucketed",
};

// ─── promotedKeys fast-path ───────────────────────────────────────────────────

Deno.test("resolveBucketStatus: promotedKeys hit → status promoted, label 配置可能", () => {
  const promoted = new Set(["display/card"]);
  const r = resolveBucketStatus("display/card", [], promoted);
  assertEquals(r.status, "promoted");
  assertEquals(r.label, "配置可能（登録完了）");
  assertEquals(r.variant, "ok");
});

Deno.test("resolveBucketStatus: promotedKeys miss with no bucket item → status unregistered", () => {
  const r = resolveBucketStatus("display/card", [], new Set());
  assertEquals(r.status, "unregistered");
  assertEquals(r.label, "未登録（使用不可）");
  assertEquals(r.variant, "info");
});

// ─── canonical status from bucket item ───────────────────────────────────────

Deno.test("resolveBucketStatus: bucketed item → status bucketed, label 部品登録済み", () => {
  const r = resolveBucketStatus("display/card", [{ ...base, status: "bucketed" }], new Set());
  assertEquals(r.status, "bucketed");
  assertEquals(r.label, "部品登録済み（準備中）");
  assertEquals(r.variant, "warn");
  assertEquals(r.bucketItemId, "bid-1");
});

Deno.test("resolveBucketStatus: packaging item → status packaging, label パッケージ化中", () => {
  const r = resolveBucketStatus("display/card", [{ ...base, status: "packaging" }], new Set());
  assertEquals(r.status, "packaging");
  assertEquals(r.label, "パッケージ化中");
  assertEquals(r.variant, "info");
});

Deno.test("resolveBucketStatus: promoted item → status promoted, label 配置可能", () => {
  const r = resolveBucketStatus("display/card", [{ ...base, status: "promoted" }], new Set());
  assertEquals(r.status, "promoted");
  assertEquals(r.label, "配置可能（登録完了）");
  assertEquals(r.variant, "ok");
});

// ─── status isolation: label is display-only, status is canonical ─────────────

Deno.test("resolveBucketStatus: status is never a Japanese label string", () => {
  const cases: Array<[BucketItem[], Set<string>]> = [
    [[{ ...base, status: "bucketed" }], new Set()],
    [[{ ...base, status: "packaging" }], new Set()],
    [[{ ...base, status: "promoted" }], new Set()],
    [[], new Set(["display/card"])],
    [[], new Set()],
  ];
  for (const [items, promoted] of cases) {
    const r = resolveBucketStatus("display/card", items, promoted);
    // status must never be a Japanese string (canonical only)
    assertEquals(
      /[ぁ-ん]|[ァ-ヶ]|[一-龠]/.test(r.status),
      false,
      `status "${r.status}" must not contain Japanese characters`,
    );
  }
});

Deno.test("resolveBucketStatus: duplicate-create guard uses status, not label", () => {
  // Guard: if status is promoted/bucketed/packaging, block duplicate creation
  const blockingStatuses = ["promoted", "bucketed", "packaging"];
  for (const s of blockingStatuses) {
    const r = resolveBucketStatus("display/card", [{ ...base, status: s }], new Set());
    assertEquals(
      blockingStatuses.includes(r.status),
      true,
      `status "${s}" should block duplicate create`,
    );
  }
  // unregistered status (no bucket item) should NOT block
  const r = resolveBucketStatus("display/card", [], new Set());
  assertEquals(r.status, "unregistered");
  assertEquals(blockingStatuses.includes(r.status), false);
});

Deno.test("resolveBucketStatus: disabled-button guard uses status unregistered, not Japanese label", () => {
  // Button to create bucket item is disabled unless status === "unregistered"
  const notUnregistered = resolveBucketStatus("display/card", [{ ...base, status: "bucketed" }], new Set());
  assertEquals(notUnregistered.status !== "unregistered", true);

  const isUnregistered = resolveBucketStatus("display/card", [], new Set());
  assertEquals(isUnregistered.status === "unregistered", true);
});
