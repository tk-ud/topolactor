import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";
import type { ScreenDataShapeSummary } from "./manifestTopologyExtensions.ts";

export type ManifestScreenColumnDraft = {
  name: string;
  dataType: string;
  nullable: boolean;
};

/** Local draft cache only — not canonical. Backend topology extensions are SSOT after save. */
export type ManifestScreenDesignDraft = {
  screenLabel: string;
  operationKind: ScreenOperationKind;
  tableRef: string;
  importSchemaName: string;
  searchTargets: string;
  aggregationSpec: string;
  columns: ManifestScreenColumnDraft[];
};

const STORAGE_KEY = "topolactor_manifest_screen_design_v2";

export const MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE =
  "ブラウザ内の下書きキャッシュです。保存反映後は backend の manifest topology が正本です。";

export const MANIFEST_SCREEN_DB_SHAPE_TODO_NOTE =
  "relation/join・aggregation viewing key・aggregation display columns の構造化入力は未実装（SSOT gap）。現状は tableRef / columns / searchTargets / aggregationSpec 文字列のみ。";

export const emptyManifestScreenDesign = (): ManifestScreenDesignDraft => ({
  screenLabel: "",
  operationKind: "list",
  tableRef: "",
  importSchemaName: "",
  searchTargets: "",
  aggregationSpec: "",
  columns: [{ name: "", dataType: "text", nullable: true }],
});

function readAll(): Record<string, ManifestScreenDesignDraft> {
  if (typeof globalThis.localStorage === "undefined") return {};
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, ManifestScreenDesignDraft>;
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

export function loadManifestScreenDesignLocal(manifestId: string): ManifestScreenDesignDraft | null {
  const entry = readAll()[manifestId];
  if (!entry) return null;
  return {
    ...emptyManifestScreenDesign(),
    ...entry,
    tableRef: entry.tableRef ?? (entry as { dbTableName?: string }).dbTableName ?? "",
  };
}

export function saveManifestScreenDesignLocal(manifestId: string, draft: ManifestScreenDesignDraft): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const all = readAll();
  all[manifestId] = draft;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
}

export function clearManifestScreenDesignLocal(manifestId: string): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const all = readAll();
  delete all[manifestId];
  localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
}

export function screenDesignFromBackendShape(
  shape: ScreenDataShapeSummary,
  operationKind: ScreenOperationKind,
): ManifestScreenDesignDraft {
  return {
    screenLabel: "",
    operationKind: (shape.screenOperationKind as ScreenOperationKind) ?? operationKind,
    tableRef: shape.tableRef ?? "",
    importSchemaName: shape.importSchemaName ?? "",
    searchTargets: shape.searchTargets.join(", "),
    aggregationSpec: shape.aggregationSpec ?? "",
    columns: [{ name: "", dataType: "text", nullable: true }],
  };
}

export function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
