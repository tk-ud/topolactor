import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";

export type ManifestScreenColumnDraft = {
  name: string;
  dataType: string;
  nullable: boolean;
};

export type ManifestScreenDesignDraft = {
  screenLabel: string;
  operationKind: ScreenOperationKind;
  dbTableName: string;
  importSchemaName: string;
  searchTargets: string;
  aggregationSpec: string;
  columns: ManifestScreenColumnDraft[];
  hubId: string;
  manifestKey: string;
};

const STORAGE_KEY = "topolactor_manifest_screen_design_v1";

export const emptyManifestScreenDesign = (): ManifestScreenDesignDraft => ({
  screenLabel: "",
  operationKind: "list",
  dbTableName: "",
  importSchemaName: "",
  searchTargets: "",
  aggregationSpec: "",
  columns: [{ name: "", dataType: "text", nullable: true }],
  hubId: "",
  manifestKey: "",
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

export function loadManifestScreenDesign(manifestId: string): ManifestScreenDesignDraft | null {
  return readAll()[manifestId] ?? null;
}

export function saveManifestScreenDesign(manifestId: string, draft: ManifestScreenDesignDraft): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const all = readAll();
  all[manifestId] = draft;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
}

export function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
