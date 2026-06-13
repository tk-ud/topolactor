import type { Emission } from "../api/dispatch.ts";
import { validationErrorText } from "../api/dispatch.ts";

export type EmissionSummary = {
  ok: boolean;
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  layoutId?: string;
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
    layoutId: emission.layoutId,
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
  layoutId?: string;
};

export function toUserFacingResult(summary: EmissionSummary): UserFacingResult {
  if (!summary.ok) {
    const firstMsg = summary.errorMessages[0] ?? "";
    const isAuthError = firstMsg.includes("AUTH_TOKEN_MISSING");
    const headline = isAuthError ? "ログインが必要です" : "エラーが発生しました";
    const detail = isAuthError
      ? "ログイン後に再試行してください。"
      : "処理に失敗しました。詳細はデバッグ画面で確認してください。";
    return { status: "error", headline, detail, itemCount: 0, hasRecommendation: false };
  }

  // explicit_error recommendation is not surfaced on the /demo preview face.
  const hasRec = summary.recommendationStatus === "ok";
  const count = summary.componentCount;
  const headline = count > 0 ? `${count} 件のデータが取得できました` : "データを取得しました";

  return {
    status: "success",
    headline,
    itemCount: count,
    hasRecommendation: hasRec,
    recommendationSummary: hasRec ? "レコメンドが見つかりました" : undefined,
    layoutId: summary.layoutId,
  };
}
