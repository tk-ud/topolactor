import type { Emission } from "../api/dispatch.ts";
import { validationErrorText } from "../api/dispatch.ts";

export type EmissionSummary = {
  ok: boolean;
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentCount: number;
  recommendationStatus?: string;
  recommendationDetail?: string;
  errorMessages: string[];
};

export function summarizeEmission(emission: Emission): EmissionSummary {
  const errorMessages = (emission.errors ?? []).map(validationErrorText);
  const rec = emission.contextRouteRecommendation;

  return {
    ok: errorMessages.length === 0,
    structureMapId: emission.structureMapId,
    packageId: emission.packageId,
    schemaId: emission.schemaId,
    componentCount: emission.componentIds?.length ?? 0,
    recommendationStatus: rec?.status,
    recommendationDetail: rec?.statusDetail,
    errorMessages,
  };
}

export type UserFacingResult = {
  status: "success" | "error";
  headline: string;
  detail?: string;
  itemCount: number;
  hasRecommendation: boolean;
  recommendationSummary?: string;
};

export function toUserFacingResult(summary: EmissionSummary): UserFacingResult {
  if (!summary.ok) {
    const firstMsg = summary.errorMessages[0] ?? "";
    const headline = firstMsg.includes("AUTH_TOKEN_MISSING")
      ? "ログインが必要です"
      : "エラーが発生しました";
    const detail = summary.errorMessages.length > 0
      ? summary.errorMessages.join(" / ")
      : undefined;
    return { status: "error", headline, detail, itemCount: 0, hasRecommendation: false };
  }

  const hasRec = summary.recommendationStatus === "ok";
  const count = summary.componentCount;
  const headline = count > 0 ? `${count} 件のデータが取得できました` : "データを取得しました";

  return {
    status: "success",
    headline,
    itemCount: count,
    hasRecommendation: hasRec,
    recommendationSummary: hasRec ? "レコメンドが見つかりました" : undefined,
  };
}
