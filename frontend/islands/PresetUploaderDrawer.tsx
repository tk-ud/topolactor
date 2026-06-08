import { useState } from "preact/hooks";
import {
  computeSourceHash,
  flattenVisualTree,
  parseVisualMockSource,
  type VisualObjectNode,
  type VisualSourceKind,
} from "../runtime/visualMockParser.ts";
import {
  createMockPreset,
  type MockPresetListItem,
} from "../api/mockPresetApi.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  STRUCTURAL_HTML_TAG_ALLOWLIST,
} from "../runtime/visualLayoutUtils.ts";

/**
 * PresetUploaderDrawer — UIBuilder modal/drawer for visual mock intake.
 * SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml §ui_builder_integration.preset_uploader_surface
 *
 * Responsibilities:
 *   - Upload or paste SVG/XML visual mock
 *   - Parse visual object tree WITHOUT AI inference
 *   - Show object tree preview and unassigned objects
 *   - Allow user to map objects to component catalog / structural_html / ignored
 *   - Save mapped preset to topology.mock_preset_registry
 *
 * Prohibited (per SSOT):
 *   - direct_apply_to_active_topology
 *   - automatic_component_identity_decision
 *   - automatic_wiring_decision
 *   - silent_drop_of_unmapped_objects
 */

export type ObjectMappingKind = "catalog_component" | "structural_html" | "ignored" | "unassigned";

export type ObjectMapping = {
  sourceObjectId: string;
  nodeId: string;
  mappingKind: ObjectMappingKind;
  componentKey?: string;
  htmlTag?: string;
};

type Props = {
  open: boolean;
  onClose: () => void;
  onPresetSaved: (preset: MockPresetListItem) => void;
};

export function PresetUploaderDrawer({ open, onClose, onPresetSaved }: Props) {
  const [rawSource, setRawSource] = useState("");
  const [parsedNodes, setParsedNodes] = useState<VisualObjectNode[]>([]);
  const [flatNodes, setFlatNodes] = useState<VisualObjectNode[]>([]);
  const [parseError, setParseError] = useState<string | null>(null);
  const [detectedKind, setDetectedKind] = useState<VisualSourceKind | null>(null);
  const [mappings, setMappings] = useState<Map<string, ObjectMapping>>(new Map());
  const [presetKey, setPresetKey] = useState("");
  const [presetLabel, setPresetLabel] = useState("");
  const [saveStatus, setSaveStatus] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [saveError, setSaveError] = useState<string | null>(null);

  if (!open) return null;

  const catalogOptions = COMPONENT_CATALOG_ENTRIES.map((e) => ({
    key: e.componentKey,
    label: `${e.componentKey} (${e.componentKind})`,
  }));

  function handleParse() {
    setParseError(null);
    setParsedNodes([]);
    setFlatNodes([]);
    setMappings(new Map());
    const result = parseVisualMockSource(rawSource);
    if (!result.ok) {
      setParseError(result.message);
      return;
    }
    setDetectedKind(result.sourceKind);
    setParsedNodes(result.nodes);
    const flat = flattenVisualTree(result.nodes);
    setFlatNodes(flat);
    const initial = new Map<string, ObjectMapping>();
    for (const node of flat) {
      initial.set(node.source_object_id, {
        sourceObjectId: node.source_object_id,
        nodeId: `preset_node_${node.source_object_id.replace(/[^a-zA-Z0-9_]/g, "_")}`,
        mappingKind: "unassigned",
      });
    }
    setMappings(initial);
  }

  function updateMapping(sourceObjectId: string, update: Partial<ObjectMapping>) {
    setMappings((prev) => {
      const next = new Map(prev);
      const existing = next.get(sourceObjectId);
      if (existing) next.set(sourceObjectId, { ...existing, ...update });
      return next;
    });
  }

  const unmappedCount = Array.from(mappings.values()).filter(
    (m) => m.mappingKind === "unassigned",
  ).length;

  async function handleSave() {
    if (!presetKey.trim()) {
      setSaveError("プリセットキーを入力してください");
      return;
    }
    if (!presetLabel.trim()) {
      setSaveError("プリセット名を入力してください");
      return;
    }
    if (flatNodes.length === 0) {
      setSaveError("モックをパースしてください");
      return;
    }

    setSaveStatus("saving");
    setSaveError(null);

    try {
      const visualTreeJson = {
        nodes: flatNodes.map((n) => ({
          source_object_id: n.source_object_id,
          object_type: n.object_type,
          parent_source_object_id: n.parent_source_object_id,
          z_index: n.z_index,
          bbox: n.bbox,
          transform: n.transform,
          text_content: n.text_content,
          style_attributes: n.style_attributes,
        })),
      };

      const mappingArray = Array.from(mappings.values());

      const result = await createMockPreset({
        presetKey: presetKey.trim(),
        presetLabel: presetLabel.trim(),
        sourceKind: detectedKind ?? "generic_xml",
        sourceHash: computeSourceHash(rawSource),
        sourceSnapshotJson: { raw: rawSource.slice(0, 4096) },
        visualTreeJson,
      });

      if (!result.ok || !result.presetId) {
        setSaveStatus("error");
        setSaveError(result.message ?? result.errorCode ?? "保存に失敗しました");
        return;
      }

      setSaveStatus("saved");
      onPresetSaved({
        presetId: result.presetId,
        presetKey: result.presetKey ?? presetKey.trim(),
        presetLabel: presetLabel.trim(),
        sourceKind: detectedKind ?? "generic_xml",
        status: "active",
        createdAt: new Date().toISOString(),
      });

      // Reset after save
      setTimeout(() => {
        setRawSource("");
        setParsedNodes([]);
        setFlatNodes([]);
        setMappings(new Map());
        setPresetKey("");
        setPresetLabel("");
        setSaveStatus("idle");
        onClose();
      }, 800);
    } catch (err) {
      setSaveStatus("error");
      setSaveError(err instanceof Error ? err.message : "保存に失敗しました");
    }
  }

  return (
    <div
      class="fixed inset-0 z-50 flex items-start justify-end"
      role="dialog"
      aria-modal="true"
      aria-label="ビジュアルモック プリセットアップロード"
    >
      {/* Overlay */}
      <button
        type="button"
        class="absolute inset-0 bg-black/40"
        aria-label="閉じる"
        onClick={onClose}
      />

      {/* Drawer panel */}
      <div class="relative z-10 flex h-full w-full max-w-xl flex-col overflow-y-auto bg-white shadow-xl">
        <div class="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 class="text-sm font-semibold text-gray-800">
            ビジュアルモック インテーク
          </h2>
          <button
            type="button"
            class="text-gray-500 hover:text-gray-700"
            aria-label="閉じる"
            onClick={onClose}
          >
            ✕
          </button>
        </div>

        <div class="flex-1 space-y-4 p-4 text-sm">
          {/* Source input */}
          <section>
            <label class="mb-1 block font-medium text-gray-700">
              SVG / XML ソースを貼り付け
            </label>
            <textarea
              class="h-32 w-full rounded border border-gray-300 px-2 py-1 font-mono text-xs"
              placeholder="<svg>...</svg> または XML を貼り付けてください"
              value={rawSource}
              onInput={(e) => setRawSource((e.target as HTMLTextAreaElement).value)}
              aria-label="SVG/XML ソース入力"
            />
            <button
              type="button"
              class="mt-1 rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700 disabled:opacity-50"
              disabled={!rawSource.trim()}
              onClick={handleParse}
            >
              パース
            </button>
            {parseError && (
              <p class="mt-1 text-xs text-red-600" role="alert">
                {parseError}
              </p>
            )}
            {detectedKind && (
              <p class="mt-1 text-xs text-gray-500">
                検出: {detectedKind} / {flatNodes.length} オブジェクト
              </p>
            )}
          </section>

          {/* Object mapping section */}
          {flatNodes.length > 0 && (
            <section>
              <div class="mb-2 flex items-center justify-between">
                <h3 class="font-medium text-gray-700">オブジェクトマッピング</h3>
                {unmappedCount > 0 && (
                  <span class="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-800">
                    未マッピング: {unmappedCount}
                  </span>
                )}
              </div>
              <p class="mb-2 text-xs text-gray-500">
                各オブジェクトを「コンポーネント / structural HTML / 無視」に割り当ててください。
                AI による自動推論はありません。
              </p>

              <div class="max-h-60 overflow-y-auto space-y-2 rounded border border-gray-200 p-2">
                {flatNodes.map((node) => {
                  const mapping = mappings.get(node.source_object_id);
                  if (!mapping) return null;
                  const isUnmapped = mapping.mappingKind === "unassigned";

                  return (
                    <div
                      key={node.source_object_id}
                      class={`rounded border p-2 text-xs ${isUnmapped ? "border-amber-200 bg-amber-50" : "border-gray-200 bg-white"}`}
                    >
                      <div class="mb-1 font-mono text-gray-600">
                        &lt;{node.object_type}&gt; {node.source_object_id}
                        {node.text_content ? ` — "${node.text_content.slice(0, 30)}"` : ""}
                      </div>

                      <div class="flex flex-wrap items-center gap-1">
                        <select
                          class="rounded border border-gray-300 px-1 py-0.5 text-xs"
                          value={mapping.mappingKind}
                          onChange={(e) => {
                            const kind = (e.target as HTMLSelectElement).value as ObjectMappingKind;
                            updateMapping(node.source_object_id, {
                              mappingKind: kind,
                              componentKey: undefined,
                              htmlTag: undefined,
                            });
                          }}
                          aria-label={`${node.source_object_id} のマッピング種別`}
                        >
                          <option value="unassigned">未割り当て</option>
                          <option value="catalog_component">コンポーネント</option>
                          <option value="structural_html">structural HTML</option>
                          <option value="ignored">無視</option>
                        </select>

                        {mapping.mappingKind === "catalog_component" && (
                          <select
                            class="rounded border border-gray-300 px-1 py-0.5 text-xs"
                            value={mapping.componentKey ?? ""}
                            onChange={(e) =>
                              updateMapping(node.source_object_id, {
                                componentKey: (e.target as HTMLSelectElement).value || undefined,
                              })}
                            aria-label={`${node.source_object_id} のコンポーネント`}
                          >
                            <option value="">コンポーネントを選択</option>
                            {catalogOptions.map((opt) => (
                              <option key={opt.key} value={opt.key}>
                                {opt.label}
                              </option>
                            ))}
                          </select>
                        )}

                        {mapping.mappingKind === "structural_html" && (
                          <select
                            class="rounded border border-gray-300 px-1 py-0.5 text-xs"
                            value={mapping.htmlTag ?? ""}
                            onChange={(e) =>
                              updateMapping(node.source_object_id, {
                                htmlTag: (e.target as HTMLSelectElement).value || undefined,
                              })}
                            aria-label={`${node.source_object_id} のHTMLタグ`}
                          >
                            <option value="">HTMLタグを選択</option>
                            {STRUCTURAL_HTML_TAG_ALLOWLIST.map((tag) => (
                              <option key={tag} value={tag}>
                                {tag}
                              </option>
                            ))}
                          </select>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Unresolved objects visibility (SSOT §unresolved_object_visibility) */}
              {unmappedCount > 0 && (
                <div class="mt-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                  <strong>未マッピング: {unmappedCount} オブジェクト</strong>
                  <br />
                  未割り当てオブジェクトはプリセット保存時にコンパイルできません。
                  「コンポーネント」「structural HTML」「無視」のいずれかを選択してください。
                </div>
              )}
            </section>
          )}

          {/* Preset metadata */}
          {flatNodes.length > 0 && (
            <section class="space-y-2">
              <h3 class="font-medium text-gray-700">プリセット情報</h3>
              <div>
                <label class="block text-xs text-gray-600">
                  プリセットキー (一意識別子)
                </label>
                <input
                  type="text"
                  class="mt-0.5 w-full rounded border border-gray-300 px-2 py-1 text-xs"
                  placeholder="例: my_landing_preset"
                  value={presetKey}
                  onInput={(e) => setPresetKey((e.target as HTMLInputElement).value)}
                  aria-label="プリセットキー"
                />
              </div>
              <div>
                <label class="block text-xs text-gray-600">
                  プリセット名
                </label>
                <input
                  type="text"
                  class="mt-0.5 w-full rounded border border-gray-300 px-2 py-1 text-xs"
                  placeholder="例: ランディングページ モック"
                  value={presetLabel}
                  onInput={(e) => setPresetLabel((e.target as HTMLInputElement).value)}
                  aria-label="プリセット名"
                />
              </div>

              <p class="text-xs text-gray-500">
                ※ 保存しても active topology には反映されません。
                UIBuilder でロードし、preview → validate → apply を経て反映します。
              </p>

              {saveError && (
                <p class="text-xs text-red-600" role="alert">
                  {saveError}
                </p>
              )}
              {saveStatus === "saved" && (
                <p class="text-xs text-green-600" role="status">
                  プリセットを保存しました
                </p>
              )}

              <button
                type="button"
                class="w-full rounded bg-green-600 px-3 py-1.5 text-sm text-white hover:bg-green-700 disabled:opacity-50"
                disabled={saveStatus === "saving" || !presetKey.trim() || !presetLabel.trim()}
                onClick={handleSave}
              >
                {saveStatus === "saving" ? "保存中..." : "プリセットとして保存"}
              </button>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
