export type BucketItem = {
  bucketItemId: string;
  componentKey: string;
  sourcePath: string;
  componentKind: string;
  status: string;
};

export type BucketStatusResult = {
  label: string;
  variant: "ok" | "warn" | "error" | "info";
  status: "promoted" | "packaging" | "bucketed" | "unregistered";
  bucketItemId?: string;
};

const BUCKET_STATUS_LABEL: Record<string, string> = {
  promoted: "配置可能（登録完了）",
  packaging: "パッケージ化中",
  bucketed: "部品登録済み（準備中）",
};

const BUCKET_STATUS_VARIANT: Record<string, "ok" | "warn" | "error" | "info"> = {
  promoted: "ok",
  packaging: "info",
  bucketed: "warn",
};

export function resolveBucketStatus(
  componentKey: string,
  bucketItems: BucketItem[],
  promotedKeys: Set<string>,
): BucketStatusResult {
  if (promotedKeys.has(componentKey)) {
    return { label: "配置可能（登録完了）", variant: "ok", status: "promoted" };
  }
  const item = bucketItems.find((b) => b.componentKey === componentKey);
  if (!item) {
    return { label: "未登録（使用不可）", variant: "info", status: "unregistered" };
  }
  const canonicalStatus = item.status as "promoted" | "packaging" | "bucketed";
  const label = BUCKET_STATUS_LABEL[canonicalStatus] ?? item.status;
  const variant = BUCKET_STATUS_VARIANT[canonicalStatus] ?? "info";
  return { label, variant, status: canonicalStatus, bucketItemId: item.bucketItemId };
}
