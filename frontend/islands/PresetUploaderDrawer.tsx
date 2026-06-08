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
  type MockPresetObjectMapping,
  type MockPresetWiringCandidate,
  saveMockPresetMappings,
} from "../api/mockPresetApi.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import type { ComponentCapabilityTag } from "../components/types.ts";
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
 *   - capabilityTags gate: show wiring/binding UI gated by component capability properties
 *   - Save mapped preset + object mappings + wiring candidates to DB
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
  componentKind?: string;
  htmlTag?: string;
};

// Wiring selection state per source object (for emits_event / controlled_value etc.)
type WiringSelection = {
  wiringKind: string;     // "route_navigation" | "runtime_dispatch" | "value_binding" | "db_jsonb_field_binding" | ""
  targetSurface: string;
  targetRef: string;
  status: string;         // "pending" | "confirmed"
};

type Props = {
  open: boolean;
  onClose: () => void;
  onPresetSaved: (preset: MockPresetListItem) => void;
  /** When provided, the drawer is seeded with canvas data for save-as-preset flow. */
  canvasPreset?: CanvasPresetSeed | null;
};

export type CanvasPresetSeed = {
  sourceKind: "ui_builder_canvas";
  layoutPatchJson: unknown;
  componentMappings: ObjectMapping[];
  visualTreeJson: unknown;
  sourceHash: string;
};

// Minimal display record for the mapping section (works for both parsed and canvas modes).
type MappingDisplayNode = {
  source_object_id: string;
  object_type: string;
  text_content: string;
};

// capabilityTags that open wiring/binding controls
const WIRING_CAPABILITY_TAGS: ComponentCapabilityTag[] = [
  "emits_event",
  "requires_event_binding",
  "controlled_value",
  "field_binding",
];

function getCapabilityTagsForComponent(componentKey: string): ComponentCapabilityTag[] {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === componentKey);
  return (entry?.capabilityTags ?? []) as ComponentCapabilityTag[];
}

function hasWiringCapability(componentKey: string): boolean {
  const tags = getCapabilityTagsForComponent(componentKey);
  return tags.some((t) => WIRING_CAPABILITY_TAGS.includes(t));
}

function wiringKindOptionsForTags(tags: ComponentCapabilityTag[]): { value: string; label: string }[] {
  const opts: { value: string; label: string }[] = [{ value: "", label: "配線なし (未設定)" }];
  if (tags.includes("emits_event")) {
    opts.push({ value: "route_navigation", label: "ルートナビゲーション (route_navigation)" });
    opts.push({ value: "runtime_dispatch", label: "ランタイムディスパッチ (runtime_dispatch)" });
  }
  if (tags.includes("controlled_value")) {
    opts.push({ value: "value_binding", label: "値バインディング (value_binding)" });
  }
  if (tags.includes("field_binding")) {
    opts.push({ value: "db_jsonb_field_binding", label: "DBフィールドバインディング (db_jsonb_field_binding)" });
  }
  return opts;
}

export function PresetUploaderDrawer({ open, onClose, onPresetSaved, canvasPreset }: Props) {
  const [rawSource, setRawSource] = useState("");
  const [parsedNodes, setParsedNodes] = useState<VisualObjectNode[]>([]);
  const [flatNodes, setFlatNodes] = useState<VisualObjectNode[]>([]);
  const [parseError, setParseError] = useState<string | null>(null);
  const [detectedKind, setDetectedKind] = useState<VisualSourceKind | null>(null);
  const [mappings, setMappings] = useState<Map<string, ObjectMapping>>(new Map());
  const [wiringSelections, setWiringSelections] = useState<Map<string, WiringSelection>>(new Map());
  const [presetKey, setPresetKey] = useState("");
  const [presetLabel, setPresetLabel] = useState("");
  const [saveStatus, setSaveStatus] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveMappingsError, setSaveMappingsError] = useState<string | null>(null);

  if (!open) return null;

  // Canvas preset seed: pre-populate state from canvas data
  const isCanvasMode = !!canvasPreset;
  const effectiveDisplayNodes: MappingDisplayNode[] = isCanvasMode
    ? canvasPreset!.componentMappings.map((m) => ({
        source_object_id: m.sourceObjectId,
        object_type: m.mappingKind === "structural_html" ? (m.htmlTag ?? "div") : (m.componentKey ?? "unknown"),
        text_content: "",
      }))
    : flatNodes.map((n) => ({
        source_object_id: n.source_object_id,
        object_type: n.object_type,
        text_content: n.text_content,
      }));
  const effectiveMappings = isCanvasMode
    ? new Map(canvasPreset!.componentMappings.map((m) => [m.sourceObjectId, m]))
    : mappings;

  const catalogOptions = COMPONENT_CATALOG_ENTRIES.map((e) => ({
    key: e.componentKey,
    label: `${e.componentKey} (${e.componentKind})`,
    kind: e.componentKind,
  }));

  function handleParse() {
    setParseError(null);
    setParsedNodes([]);
    setFlatNodes([]);
    setMappings(new Map());
    setWiringSelections(new Map());
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
    // Reset wiring when mapping kind changes
    if ("mappingKind" in update || "componentKey" in update) {
      setWiringSelections((prev) => {
        const next = new Map(prev);
        next.delete(sourceObjectId);
        return next;
      });
    }
  }

  function updateWiring(sourceObjectId: string, update: Partial<WiringSelection>) {
    setWiringSelections((prev) => {
      const next = new Map(prev);
      const existing = next.get(sourceObjectId) ?? {
        wiringKind: "",
        targetSurface: "",
        targetRef: "",
        status: "pending",
      };
      next.set(sourceObjectId, { ...existing, ...update });
      return next;
    });
  }

  const activeMappings = isCanvasMode ? effectiveMappings : mappings;
  const unmappedCount = Array.from(activeMappings.values()).filter(
    (m) => m.mappingKind === "unassigned",
  ).length;

  // Nodes that need wiring confirmation (requires_event_binding with no wiring set)
  const requiresWiringCount = Array.from(activeMappings.values()).filter((m) => {
    if (m.mappingKind !== "catalog_component" || !m.componentKey) return false;
    const tags = getCapabilityTagsForComponent(m.componentKey);
    if (!tags.includes("requires_event_binding")) return false;
    const sel = wiringSelections.get(m.sourceObjectId);
    return !sel?.wiringKind;
  }).length;

  async function handleSave() {
    if (!presetKey.trim()) {
      setSaveError("プリセットキーを入力してください");
      return;
    }
    if (!presetLabel.trim()) {
      setSaveError("プリセット名を入力してください");
      return;
    }
    const activeFlat = isCanvasMode ? effectiveDisplayNodes : flatNodes;
    if (activeFlat.length === 0 && !isCanvasMode) {
      setSaveError("モックをパースしてください");
      return;
    }

    setSaveStatus("saving");
    setSaveError(null);
    setSaveMappingsError(null);

    try {
      let visualTreeJson: unknown;
      let sourceKind: string;
      let sourceHash: string;
      let sourceSnapshotJson: unknown;

      if (isCanvasMode && canvasPreset) {
        visualTreeJson = canvasPreset.visualTreeJson;
        sourceKind = "ui_builder_canvas";
        sourceHash = canvasPreset.sourceHash;
        sourceSnapshotJson = { source_kind: "ui_builder_canvas", node_count: effectiveDisplayNodes.length };
      } else {
        visualTreeJson = {
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
        sourceKind = detectedKind ?? "generic_xml";
        sourceHash = computeSourceHash(rawSource);
        sourceSnapshotJson = { raw: rawSource.slice(0, 4096) };
      }

      const result = await createMockPreset({
        presetKey: presetKey.trim(),
        presetLabel: presetLabel.trim(),
        sourceKind,
        sourceHash,
        sourceSnapshotJson,
        visualTreeJson,
      });

      if (!result.ok || !result.presetId) {
        setSaveStatus("error");
        setSaveError(result.message ?? result.errorCode ?? "保存に失敗しました");
        return;
      }

      const presetId = result.presetId;

      // Persist object mappings and wiring candidates.
      const mappingArray: MockPresetObjectMapping[] = Array.from(activeMappings.values()).map(
        (m, idx) => ({
          sourceObjectId: m.sourceObjectId,
          nodeId: m.nodeId,
          nodeKind: m.mappingKind === "unassigned" ? "unassigned" : m.mappingKind,
          componentKey: m.componentKey,
          componentKind: m.componentKind,
          htmlTag: m.htmlTag,
          parentSourceObjectId: undefined,
          slotKey: undefined,
          orderIndex: idx,
          bboxJson: {},
          textJson: {},
          styleCandidateJson: [],
          mappingStatus: m.mappingKind === "unassigned" ? "unassigned" : m.mappingKind === "ignored" ? "ignored" : "mapped",
        }),
      );

      const wiringCandidates: MockPresetWiringCandidate[] = [];
      for (const [sourceObjectId, sel] of wiringSelections.entries()) {
        if (!sel.wiringKind) continue;
        const mapping = activeMappings.get(sourceObjectId);
        if (!mapping?.componentKey) continue;
        const tags = getCapabilityTagsForComponent(mapping.componentKey);
        for (const tag of tags) {
          if (!WIRING_CAPABILITY_TAGS.includes(tag)) continue;
          wiringCandidates.push({
            sourceObjectId,
            nodeId: mapping.nodeId,
            capabilityTag: tag,
            wiringKind: sel.wiringKind || undefined,
            targetSurface: sel.targetSurface || undefined,
            targetRef: sel.targetRef || undefined,
            bindingJson: {},
            status: sel.status || "pending",
          });
        }
      }

      const saveResult = await saveMockPresetMappings({
        presetId,
        mappings: mappingArray,
        wiringCandidates,
      });

      if (!saveResult.ok && saveResult.errors.length > 0) {
        // Non-fatal: preset saved, but mapping persistence had errors. Show warning.
        setSaveMappingsError(
          `オブジェクトマッピングの保存に一部失敗しました: ${saveResult.errors[0]}`,
        );
      }

      setSaveStatus("saved");
      onPresetSaved({
        presetId,
        presetKey: result.presetKey ?? presetKey.trim(),
        presetLabel: presetLabel.trim(),
        sourceKind: (sourceKind as MockPresetListItem["sourceKind"]),
        status: "active",
        createdAt: new Date().toISOString(),
      });

      setTimeout(() => {
        setRawSource("");
        setParsedNodes([]);
        setFlatNodes([]);
        setMappings(new Map());
        setWiringSelections(new Map());
        setPresetKey("");
        setPresetLabel("");
        setSaveStatus("idle");
        setSaveMappingsError(null);
        onClose();
      }, 800);
    } catch (err) {
      setSaveStatus("error");
      setSaveError(err instanceof Error ? err.message : "保存に失敗しました");
    }
  }

  const displayNodes = effectiveDisplayNodes;

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
            {isCanvasMode ? "キャンバスをプリセットとして保存" : "ビジュアルモック インテーク"}
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
          {/* Source input — only shown in non-canvas mode */}
          {!isCanvasMode && (
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
          )}

          {isCanvasMode && (
            <div class="rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs text-blue-800">
              現在のキャンバス ({effectiveDisplayNodes.length} ノード) を
              source_kind=ui_builder_canvas としてプリセット保存します。
              active topology には反映されません。
            </div>
          )}

          {/* Object mapping section */}
          {displayNodes.length > 0 && (
            <section>
              <div class="mb-2 flex items-center justify-between">
                <h3 class="font-medium text-gray-700">オブジェクトマッピング</h3>
                <div class="flex items-center gap-1">
                  {unmappedCount > 0 && (
                    <span class="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-800">
                      未マッピング: {unmappedCount}
                    </span>
                  )}
                  {requiresWiringCount > 0 && (
                    <span class="rounded bg-orange-100 px-2 py-0.5 text-xs text-orange-800">
                      配線要: {requiresWiringCount}
                    </span>
                  )}
                </div>
              </div>
              {!isCanvasMode && (
                <p class="mb-2 text-xs text-gray-500">
                  各オブジェクトを「コンポーネント / structural HTML / 無視」に割り当ててください。
                  AI による自動推論はありません。
                </p>
              )}

              <div class="max-h-80 overflow-y-auto space-y-2 rounded border border-gray-200 p-2">
                {displayNodes.map((node, idx) => {
                  const mapping = activeMappings.get(node.source_object_id);
                  if (!mapping) return null;
                  const isUnmapped = mapping.mappingKind === "unassigned";
                  const componentKey = mapping.componentKey ?? "";
                  const capTags = componentKey ? getCapabilityTagsForComponent(componentKey) : [];
                  const needsWiring = hasWiringCapability(componentKey) && mapping.mappingKind === "catalog_component";
                  const wiringOpts = wiringKindOptionsForTags(capTags);
                  const wiring = wiringSelections.get(node.source_object_id);
                  const requiresBinding = capTags.includes("requires_event_binding");
                  const wiringMissing = requiresBinding && !wiring?.wiringKind;

                  return (
                    <div
                      key={node.source_object_id}
                      data-testid={`mapping-row-${idx}`}
                      class={`rounded border p-2 text-xs ${
                        wiringMissing
                          ? "border-orange-300 bg-orange-50"
                          : isUnmapped
                          ? "border-amber-200 bg-amber-50"
                          : "border-gray-200 bg-white"
                      }`}
                    >
                      <div class="mb-1 font-mono text-gray-600">
                        &lt;{node.object_type}&gt; {node.source_object_id}
                        {node.text_content ? ` — "${node.text_content.slice(0, 30)}"` : ""}
                      </div>

                      {/* Mapping kind select — only in non-canvas mode */}
                      {!isCanvasMode && (
                        <div class="flex flex-wrap items-center gap-1">
                          <select
                            class="rounded border border-gray-300 px-1 py-0.5 text-xs"
                            value={mapping.mappingKind}
                            onChange={(e) => {
                              const kind = (e.target as HTMLSelectElement).value as ObjectMappingKind;
                              updateMapping(node.source_object_id, {
                                mappingKind: kind,
                                componentKey: undefined,
                                componentKind: undefined,
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
                              onChange={(e) => {
                                const key = (e.target as HTMLSelectElement).value || undefined;
                                const entry = key ? COMPONENT_CATALOG_ENTRIES.find((c) => c.componentKey === key) : undefined;
                                updateMapping(node.source_object_id, {
                                  componentKey: key,
                                  componentKind: entry?.componentKind,
                                });
                              }}
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
                      )}

                      {/* Canvas mode: show read-only mapping info */}
                      {isCanvasMode && (
                        <div class="text-xs text-gray-500">
                          {mapping.mappingKind === "catalog_component" && mapping.componentKey
                            ? `コンポーネント: ${mapping.componentKey}`
                            : mapping.mappingKind === "structural_html" && mapping.htmlTag
                            ? `structural HTML: ${mapping.htmlTag}`
                            : mapping.mappingKind === "ignored"
                            ? "無視"
                            : "未割り当て"}
                        </div>
                      )}

                      {/* capabilityTags gate: wiring/binding UI panel */}
                      {needsWiring && (
                        <div
                          class="mt-2 rounded border border-indigo-200 bg-indigo-50 p-2 text-xs"
                          data-testid={`capability-gate-${node.source_object_id}`}
                        >
                          <div class="mb-1 font-medium text-indigo-800">
                            配線・バインディング設定
                            {capTags.map((t) => (
                              <span
                                key={t}
                                class="ml-1 rounded bg-indigo-100 px-1 py-0.5 text-[0.6rem] text-indigo-700"
                                data-testid={`capability-tag-${t}`}
                              >
                                {t}
                              </span>
                            ))}
                          </div>

                          {wiringMissing && (
                            <p class="mb-1 text-orange-700" role="alert">
                              このコンポーネントはイベントバインディングが必要です（requires_event_binding）。
                              配線種別を選択してください。
                            </p>
                          )}

                          <div class="flex flex-col gap-1">
                            <div class="flex items-center gap-1">
                              <label class="text-gray-600" style="min-width:4rem">
                                配線種別:
                              </label>
                              <select
                                class="flex-1 rounded border border-gray-300 px-1 py-0.5 text-xs"
                                value={wiring?.wiringKind ?? ""}
                                onChange={(e) =>
                                  updateWiring(node.source_object_id, {
                                    wiringKind: (e.target as HTMLSelectElement).value,
                                    targetRef: "",
                                    targetSurface: "",
                                  })}
                                aria-label={`${node.source_object_id} の配線種別`}
                                data-testid={`wiring-kind-select-${node.source_object_id}`}
                              >
                                {wiringOpts.map((opt) => (
                                  <option key={opt.value} value={opt.value}>
                                    {opt.label}
                                  </option>
                                ))}
                              </select>
                            </div>

                            {wiring?.wiringKind && (
                              <div class="flex items-center gap-1">
                                <label class="text-gray-600" style="min-width:4rem">
                                  ターゲット:
                                </label>
                                <input
                                  type="text"
                                  class="flex-1 rounded border border-gray-300 px-1 py-0.5 text-xs"
                                  placeholder={
                                    wiring.wiringKind === "route_navigation"
                                      ? "例: route:my_route_key"
                                      : wiring.wiringKind === "runtime_dispatch"
                                      ? "例: action_key"
                                      : "例: field_path"
                                  }
                                  value={wiring.targetRef ?? ""}
                                  onInput={(e) =>
                                    updateWiring(node.source_object_id, {
                                      targetRef: (e.target as HTMLInputElement).value,
                                    })}
                                  aria-label={`${node.source_object_id} のターゲット参照`}
                                  data-testid={`wiring-target-ref-${node.source_object_id}`}
                                />
                              </div>
                            )}

                            {wiring?.wiringKind && (
                              <div class="flex items-center gap-1">
                                <label class="flex items-center gap-1 text-gray-600">
                                  <input
                                    type="checkbox"
                                    checked={wiring.status === "confirmed"}
                                    onChange={(e) =>
                                      updateWiring(node.source_object_id, {
                                        status: (e.target as HTMLInputElement).checked
                                          ? "confirmed"
                                          : "pending",
                                      })}
                                    aria-label={`${node.source_object_id} の配線を確定`}
                                    data-testid={`wiring-confirm-${node.source_object_id}`}
                                  />
                                  配線を確定
                                </label>
                              </div>
                            )}
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              {/* Unresolved objects visibility (SSOT §unresolved_object_visibility) */}
              {unmappedCount > 0 && (
                <div
                  class="mt-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
                  role="alert"
                  data-testid="unresolved-objects-banner"
                >
                  <strong>未マッピング: {unmappedCount} オブジェクト</strong>
                  <br />
                  未割り当てオブジェクトはコンパイル時に unresolved として扱われます。
                  「コンポーネント」「structural HTML」「無視」のいずれかを選択してください。
                </div>
              )}
            </section>
          )}

          {/* Preset metadata */}
          {(displayNodes.length > 0 || isCanvasMode) && (
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
              {saveMappingsError && (
                <p class="text-xs text-orange-600" role="alert" data-testid="mapping-save-warning">
                  {saveMappingsError}
                </p>
              )}
              {saveStatus === "saved" && (
                <p class="text-xs text-green-600" role="status" data-testid="save-success-msg">
                  プリセットを保存しました
                </p>
              )}

              <button
                type="button"
                class="w-full rounded bg-green-600 px-3 py-1.5 text-sm text-white hover:bg-green-700 disabled:opacity-50"
                disabled={saveStatus === "saving" || !presetKey.trim() || !presetLabel.trim()}
                onClick={handleSave}
                data-testid="save-preset-button"
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
