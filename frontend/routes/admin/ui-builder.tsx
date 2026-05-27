import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../../components/catalog.ts";
import { CSS_DICTIONARY_TOKENS } from "../../runtime/cssDictionary.ts";

/**
 * /admin/ui-builder — UI component system and layout builder.
 *
 * Per Issue #86: primitive component system with UI topology DB registration.
 * Per Issue #89: admin visual layout builder (skeleton).
 *
 * SSOT: docs/registrar-admin-ui-specification.md §2.5 Components Bucket → Package Generator → DB Save
 *   component definition → bucket → package generator
 *   → componentId/packageId issued → UI topology DB save
 *   → frontend projection reads DB definitions
 *
 * Code-only components are drift/GAP. DB-registered components are the SSOT.
 * Frontend adapter is a stable projection surface — spec changes via registry tensor.
 */

const SESSION_TOKEN_KEY = "demo_jwt_token";

function getAuthHeaders(): Record<string, string> {
  const token =
    typeof globalThis.sessionStorage !== "undefined"
      ? sessionStorage.getItem(SESSION_TOKEN_KEY)
      : null;
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return headers;
}

async function dispatchAdminOp(layer: string, action: string, payload?: unknown) {
  const res = await fetch("/api/dispatch", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({
      operationType: "admin",
      target: "admin",
      layer,
      action,
      payload: payload ?? null,
    }),
  });
  return await res.json();
}

type BucketItem = {
  bucketItemId: string;
  componentKey: string;
  sourcePath: string;
  componentKind: string;
  status: string;
};

type ValidationError = { code: string; message: string };

// Draft state for the visual layout editor.
// Frontend holds draft projection only — topology authority is DB-side.
// isDraftOnly: true means the component has no DB-registered identity (code_only_drift /
// registrationRequired). Draft-only nodes are blocked from layout_patch:apply.
type DraftNode = {
  nodeId: string;
  componentKey: string;
  isDraftOnly: boolean;
  slotKey: string;
  orderIndex: number;
  parentNodeId: string | null;
  gridCol: number;
  gridRow: number;
};

type DragSrc =
  | { kind: "palette"; componentKey: string; isDraftOnly: boolean }
  | { kind: "canvas"; nodeId: string };

function buildLayoutPatchJson(nodes: DraftNode[]): string {
  return JSON.stringify(
    {
      grid: { cols: 12 },
      nodes: nodes.map((n) => ({
        nodeId: n.nodeId,
        componentKey: n.componentKey,
        // _draftOnly marks nodes with no DB-registered identity — backend must not persist these.
        ...(n.isDraftOnly ? { _draftOnly: true } : {}),
        slotKey: n.slotKey || null,
        orderIndex: n.orderIndex,
        parentNodeId: n.parentNodeId || null,
        gridCol: n.gridCol,
        gridRow: n.gridRow,
      })),
    },
    null,
    2
  );
}

function makeNodeId(): string {
  return `node_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
}

function PrimitiveCatalog(): JSX.Element {
  return (
    <section style={{ marginBottom: "24px" }}>
      <h2>Component Catalog Classification (Issue #86 bundle)</h2>
      <p style={{ color: "#555", fontSize: "0.9rem" }}>
        Primitive components are defined in <code>frontend/components/</code>.
        Components must be registered in the UI topology DB (via the package generator) to become
        topology tensor entities. Code-only components are treated as drift/GAP.
      </p>
      <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%", fontFamily: "monospace" }}>
        <thead>
          <tr>
            {["component_key", "kind", "source_path", "family", "semantic_role", "visual_role", "lifecycle_status", "runtime_connected", "registration_required", "capability_tags"].map((h) => (
              <th key={h} style={{ textAlign: "left", borderBottom: "2px solid #ccc", padding: "4px 8px", background: "#f5f5f5" }}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {COMPONENT_CATALOG_ENTRIES.map((c) => (
            <tr key={c.componentKey} style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "4px 8px" }}><code>{c.componentKey}</code></td>
              <td style={{ padding: "4px 8px" }}>{c.componentKind}</td>
              <td style={{ padding: "4px 8px" }}><code>{c.sourcePath}</code></td>
              <td style={{ padding: "4px 8px" }}>{c.componentFamily}</td>
              <td style={{ padding: "4px 8px" }}>{c.semanticRole}</td>
              <td style={{ padding: "4px 8px" }}>{c.visualRole}</td>
              <td style={{ padding: "4px 8px", color: c.lifecycleStatus === "code_only_drift" ? "#c80" : "#067" }}>{c.lifecycleStatus}</td>
              <td style={{ padding: "4px 8px" }}>{String(c.runtimeConnected)}</td>
              <td style={{ padding: "4px 8px" }}>{String(c.registrationRequired)}</td>
              <td style={{ padding: "4px 8px" }}><code>{c.capabilityTags.join(",")}</code></td>
            </tr>
          ))}
        </tbody>
      </table>
      <p style={{ color: "#888", fontSize: "0.85rem", marginTop: "8px" }}>
        To promote from drift to topology entity: register in ui_component_bucket and run the
        package generator (admin:ui_component_bucket:list → admin:package_generator:generate).
      </p>
    </section>
  );
}

function BucketSection(): JSX.Element {
  const [items, setItems] = useState<BucketItem[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationError[]>([]);
  const [loading, setLoading] = useState(false);
  const [routeKey, setRouteKey] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [componentKey, setComponentKey] = useState("");
  const [sourcePath, setSourcePath] = useState("");
  const [componentKind, setComponentKind] = useState("primitive");

  const loadBucket = async () => {
    setLoading(true);
    setStatus(null);
    try {
      const body = await dispatchAdminOp("ui_component_bucket", "list");
      const data = body?.emission?.data;
      if (Array.isArray(data)) {
        setItems(data);
        setStatus(`Loaded ${data.length} bucket item(s).`);
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Failed to load bucket.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleGenerate = async () => {
    if (!selectedId || !routeKey) {
      setStatus("Select a bucket item and enter a route key.");
      return;
    }
    setLoading(true);
    setStatus(null);
    try {
      const body = await dispatchAdminOp("package_generator", "generate", {
        bucketItemId: selectedId,
        routeKey,
      });
      if (body?.success || body?.emission?.data?.ok) {
        const data = body?.emission?.data;
        setStatus(
          `Generated/staged: bucketItemId=${data?.bucketItemId}, routeKey=${data?.routeKey}, status=${data?.status}`
        );
        await loadBucket();
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Package generation failed.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    if (!componentKey || !sourcePath || !componentKind) {
      setStatus("componentKey/sourcePath/componentKind are required.");
      return;
    }
    setLoading(true);
    setStatus(null);
    try {
      const body = await dispatchAdminOp("ui_component_bucket", "create", {
        componentKey,
        sourcePath,
        componentKind,
        metadataJson: "{}",
      });
      if (body?.emission?.data?.bucketItemId) {
        setStatus(`Bucket item created: ${body.emission.data.bucketItemId}`);
        await loadBucket();
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Bucket create failed.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <section style={{ marginBottom: "24px" }}>
      <h2>Component Bucket → Generate → Promote → DB registration</h2>
      <p style={{ color: "#555", fontSize: "0.9rem" }}>
        Bucket items are unpackaged component candidates. The package generator promotes them
        to UI topology tensor entities (componentId / packageId / layoutId / wiringId issued).
      </p>
      <div style={{ display: "flex", gap: "8px", marginBottom: "8px" }}>
        <input value={componentKey} onInput={(e) => setComponentKey((e.target as HTMLInputElement).value)} placeholder="componentKey" style={{ padding: "6px 8px", fontFamily: "monospace" }} />
        <input value={sourcePath} onInput={(e) => setSourcePath((e.target as HTMLInputElement).value)} placeholder="sourcePath" style={{ padding: "6px 8px", fontFamily: "monospace", flex: 1 }} />
        <input value={componentKind} onInput={(e) => setComponentKind((e.target as HTMLInputElement).value)} placeholder="componentKind" style={{ padding: "6px 8px", fontFamily: "monospace" }} />
        <button onClick={handleCreate} disabled={loading} style={{ padding: "6px 14px" }}>
          Register to bucket
        </button>
        <button onClick={loadBucket} disabled={loading} style={{ padding: "6px 14px" }}>
          Load bucket (status: bucketed)
        </button>
      </div>

      {items.length > 0 && (
        <>
          <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%", fontFamily: "monospace", marginBottom: "12px" }}>
            <thead>
              <tr>
                {["select", "component_key", "kind", "status"].map((h) => (
                  <th key={h} style={{ textAlign: "left", borderBottom: "2px solid #ccc", padding: "4px 8px", background: "#f5f5f5" }}>
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.bucketItemId} style={{ borderBottom: "1px solid #eee" }}>
                  <td style={{ padding: "4px 8px" }}>
                    <input
                      type="radio"
                      name="bucketItem"
                      value={item.bucketItemId}
                      checked={selectedId === item.bucketItemId}
                      onChange={() => setSelectedId(item.bucketItemId)}
                    />
                  </td>
                  <td style={{ padding: "4px 8px" }}><code>{item.componentKey}</code></td>
                  <td style={{ padding: "4px 8px" }}>{item.componentKind}</td>
                  <td style={{ padding: "4px 8px" }}>{item.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div style={{ display: "flex", gap: "8px", alignItems: "center", marginBottom: "8px" }}>
            <label style={{ fontFamily: "monospace" }}>Route key:</label>
            <input
              type="text"
              value={routeKey}
              onInput={(e) => setRouteKey((e.target as HTMLInputElement).value)}
              placeholder="e.g. /admin/ui-builder"
              style={{ padding: "4px 8px", fontFamily: "monospace", flex: 1 }}
            />
            <button
              onClick={handleGenerate}
              disabled={loading || !selectedId || !routeKey}
              style={{ padding: "6px 14px", background: "#0070f3", color: "#fff", border: "none" }}
            >
              Generate (bucketed → packaging)
            </button>
            <button
              onClick={async () => {
                if (!selectedId || !routeKey) return;
                setLoading(true);
                setStatus(null);
                try {
                  const body = await dispatchAdminOp("package_generator", "promote", { bucketItemId: selectedId, routeKey });
                  if (body?.success || body?.emission?.data?.ok) {
                    const data = body?.emission?.data;
                    setStatus(`Package promoted: tensorId=${data?.tensorId}, componentId=${data?.componentId}, packageId=${data?.packageId}, layoutId=${data?.layoutId}, wiringId=${data?.wiringId}`);
                    await loadBucket();
                  } else {
                    setErrors(body?.errors ?? []);
                    setStatus("Package promote failed.");
                  }
                } finally {
                  setLoading(false);
                }
              }}
              disabled={loading || !selectedId || !routeKey}
              style={{ padding: "6px 14px", background: "#0a7a33", color: "#fff", border: "none" }}
            >
              Promote (packaging → promoted)
            </button>
          </div>
        </>
      )}

      {loading && <p style={{ color: "#888" }}>Processing...</p>}
      {status && (
        <p style={{ color: errors.length > 0 ? "#c00" : "#090" }}>
          <strong>{status}</strong>
        </p>
      )}
      {errors.length > 0 && (
        <ul style={{ color: "#c00" }}>
          {errors.map((e, i) => (
            <li key={i}><code>{e.code}</code>: {e.message}</li>
          ))}
        </ul>
      )}
    </section>
  );
}


function CssTokenSelectorSection(): JSX.Element {
  const [componentScope, setComponentScope] = useState("Button");
  const candidates = CSS_DICTIONARY_TOKENS.filter((t) => t.componentScope.includes(componentScope));
  return (
    <section style={{ marginBottom: "24px" }}>
      <h2>CSS Dictionary Selector (Issue #89 wiring)</h2>
      <p style={{ color: "#555", fontSize: "0.9rem" }}>
        Selector candidates are projected from <code>docs/design/css-dictionary-ssot.yaml</code> derived artifact.
        Drafts should hold <code>cssTokenRefs</code> / <code>responsiveTokenRefs</code>; raw CSS is legacy-only.
      </p>
      <div style={{ display: "flex", gap: "8px", marginBottom: "8px" }}>
        <label>component_scope</label>
        <select value={componentScope} onChange={(e) => setComponentScope((e.target as HTMLSelectElement).value)}>
          {["Button","Input","Table","Card"].map((k) => <option value={k}>{k}</option>)}
        </select>
      </div>
      <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%", fontFamily: "monospace" }}>
        <thead><tr>{["token_key","category","component_scope","semantic_role","property"].map((h)=><th style={{textAlign:"left",borderBottom:"2px solid #ccc",background:"#f5f5f5"}}>{h}</th>)}</tr></thead>
        <tbody>{candidates.map((t)=><tr><td><code>{t.tokenKey}</code></td><td>{t.category}</td><td>{t.componentScope.join(",")}</td><td>{t.semanticRole}</td><td>{t.property}</td></tr>)}</tbody>
      </table>
    </section>
  );
}

// A palette entry is draft-only when it has no DB-registered identity.
// registrationRequired:true = must go through bucket → package generator before apply.
function isDraftOnlyEntry(c: { registrationRequired: boolean }): boolean {
  return c.registrationRequired;
}

function LayoutPalette({
  onDragStart,
}: {
  onDragStart: (componentKey: string, isDraftOnly: boolean) => void;
}): JSX.Element {
  return (
    <div
      style={{
        width: "175px",
        flexShrink: 0,
        border: "1px solid #ccc",
        borderRadius: "4px",
        padding: "8px",
        background: "#fafafa",
        overflowY: "auto",
        maxHeight: "340px",
      }}
    >
      <h4 style={{ margin: "0 0 4px", fontSize: "0.9rem" }}>Palette</h4>
      <p style={{ fontSize: "0.68rem", color: "#888", margin: "0 0 4px" }}>
        Drag to canvas. <strong style={{ color: "#a06000" }}>(draft)</strong> = unregistered;
        preview only, apply blocked until promoted via Component Bucket.
      </p>
      {COMPONENT_CATALOG_ENTRIES.map((c) => {
        const draftOnly = isDraftOnlyEntry(c);
        return (
          <div
            key={c.componentKey}
            draggable={true}
            onDragStart={() => onDragStart(c.componentKey, draftOnly)}
            style={{
              padding: "5px 7px",
              marginBottom: "3px",
              border: `1px solid ${draftOnly ? "#e6c97a" : "#bde"}`,
              borderRadius: "3px",
              background: draftOnly ? "#fffbe6" : "#f0f7ff",
              cursor: "grab",
              fontFamily: "monospace",
              fontSize: "0.75rem",
            }}
          >
            <div style={{ fontWeight: "bold" }}>
              {c.componentKey}
              {draftOnly && (
                <span style={{ color: "#a06000", fontWeight: "normal", marginLeft: "4px" }}>
                  (draft)
                </span>
              )}
            </div>
            <div style={{ color: "#777", fontSize: "0.65rem" }}>{c.componentKind}</div>
          </div>
        );
      })}
    </div>
  );
}

function LayoutNodeInspector({
  node,
  onUpdate,
  onClose,
}: {
  node: DraftNode;
  onUpdate: (updates: Partial<DraftNode>) => void;
  onClose: () => void;
}): JSX.Element {
  return (
    <div
      style={{
        width: "190px",
        flexShrink: 0,
        border: "1px solid #0070f3",
        borderRadius: "4px",
        padding: "10px",
        background: "#f0f7ff",
        fontFamily: "monospace",
        fontSize: "0.8rem",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "8px" }}>
        <strong style={{ fontSize: "0.85rem" }}>Inspector</strong>
        <button onClick={onClose} style={{ padding: "0 5px", fontSize: "0.75rem" }}>x</button>
      </div>
      <div style={{ color: "#555", marginBottom: "8px" }}>
        <code style={{ fontSize: "0.75rem" }}>{node.componentKey}</code>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
        <label style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
          slotKey
          <input
            value={node.slotKey}
            onInput={(e) =>
              onUpdate({ slotKey: (e.target as HTMLInputElement).value })
            }
            placeholder="e.g. header_slot"
            style={{ padding: "3px 5px", fontFamily: "monospace", fontSize: "0.8rem" }}
          />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
          parentNodeId
          <input
            value={node.parentNodeId ?? ""}
            onInput={(e) => {
              const v = (e.target as HTMLInputElement).value;
              onUpdate({ parentNodeId: v || null });
            }}
            placeholder="(none — top-level)"
            style={{ padding: "3px 5px", fontFamily: "monospace", fontSize: "0.8rem" }}
          />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
          gridCol (1–12)
          <input
            type="number"
            min={1}
            max={12}
            value={node.gridCol}
            onInput={(e) => {
              const v = parseInt((e.target as HTMLInputElement).value);
              onUpdate({ gridCol: isNaN(v) ? 1 : v });
            }}
            style={{ padding: "3px 5px" }}
          />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
          gridRow
          <input
            type="number"
            min={1}
            value={node.gridRow}
            onInput={(e) => {
              const v = parseInt((e.target as HTMLInputElement).value);
              onUpdate({ gridRow: isNaN(v) ? 1 : v });
            }}
            style={{ padding: "3px 5px" }}
          />
        </label>
      </div>
    </div>
  );
}

function LayoutBuilderSection(): JSX.Element {
  const [layoutId, setLayoutId] = useState("");
  const [routeKey, setRouteKey] = useState("/admin/ui-builder");
  const [draftNodes, setDraftNodes] = useState<DraftNode[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedTokenRefs, setSelectedTokenRefs] = useState<string[]>([]);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // useRef to avoid stale closure issues during drag-and-drop event sequence
  const dragSrc = useRef<DragSrc | null>(null);

  const tensorPatchJson = buildLayoutPatchJson(draftNodes);

  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setError(null);
    setResult(null);

    // Apply is blocked if any canvas node has no DB-registered identity.
    // Draft-only nodes (code_only_drift / registrationRequired) must be promoted via
    // Component Bucket → Package Generator before they can be persisted to the layout tensor.
    if (action === "apply") {
      const draftOnlyNodes = draftNodes.filter((n) => n.isDraftOnly);
      if (draftOnlyNodes.length > 0) {
        setError(
          `APPLY_BLOCKED: ${draftOnlyNodes.length} node(s) have no DB-registered identity ` +
          `(registrationRequired / code_only_drift). ` +
          `Register via Component Bucket → Package Generator first: ` +
          draftOnlyNodes.map((n) => n.componentKey).join(", ")
        );
        return;
      }
    }

    setLoading(true);
    try {
      const body = await dispatchAdminOp("layout_patch", action, {
        layoutId,
        routeKey,
        tensorPatchJson,
        cssTokenRefs: selectedTokenRefs,
        responsiveTokenRefs: { md: selectedTokenRefs },
      });
      if (body?.errors?.length) {
        setError(`${body.errors[0].code}: ${body.errors[0].message}`);
      } else {
        setResult(JSON.stringify(body?.emission?.data ?? body, null, 2));
      }
    } catch (e) {
      setError(`${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleDragStartPalette = (componentKey: string, isDraftOnly: boolean) => {
    dragSrc.current = { kind: "palette", componentKey, isDraftOnly };
  };

  const handleDragStartCanvas = (nodeId: string) => {
    dragSrc.current = { kind: "canvas", nodeId };
  };

  const handleDragOverCanvas = (e: Event) => {
    e.preventDefault();
  };

  const handleDropOnCanvas = (e: Event) => {
    e.preventDefault();
    const src = dragSrc.current;
    if (!src) return;
    if (src.kind === "palette") {
      const newNode: DraftNode = {
        nodeId: makeNodeId(),
        componentKey: src.componentKey,
        isDraftOnly: src.isDraftOnly,
        slotKey: "",
        orderIndex: draftNodes.length,
        parentNodeId: null,
        gridCol: 1,
        gridRow: draftNodes.length + 1,
      };
      setDraftNodes((prev) => [...prev, newNode]);
    } else if (src.kind === "canvas") {
      // Reorder: move dragged canvas node to end of list
      const nodeId = src.nodeId;
      setDraftNodes((prev) => {
        const node = prev.find((n) => n.nodeId === nodeId);
        if (!node) return prev;
        const rest = prev.filter((n) => n.nodeId !== nodeId);
        return [...rest, node].map((n, i) => ({ ...n, orderIndex: i }));
      });
    }
    dragSrc.current = null;
  };

  const moveLayoutNode = (nodeId: string, dir: "up" | "down") => {
    setDraftNodes((prev) => {
      const idx = prev.findIndex((n) => n.nodeId === nodeId);
      if (idx < 0) return prev;
      const swapIdx = dir === "up" ? idx - 1 : idx + 1;
      if (swapIdx < 0 || swapIdx >= prev.length) return prev;
      const next = [...prev];
      [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
      return next.map((n, i) => ({ ...n, orderIndex: i }));
    });
  };

  const assignNodeToSlot = (nodeId: string, slotKey: string) => {
    setDraftNodes((prev) =>
      prev.map((n) => (n.nodeId === nodeId ? { ...n, slotKey } : n))
    );
  };

  const removeNode = (nodeId: string) => {
    setDraftNodes((prev) => prev.filter((n) => n.nodeId !== nodeId));
    if (selectedNodeId === nodeId) setSelectedNodeId(null);
  };

  const updateNode = (nodeId: string, updates: Partial<DraftNode>) => {
    setDraftNodes((prev) =>
      prev.map((n) => (n.nodeId === nodeId ? { ...n, ...updates } : n))
    );
  };

  const toggleTokenRef = (tokenKey: string) => {
    setSelectedTokenRefs((prev) =>
      prev.includes(tokenKey) ? prev.filter((k) => k !== tokenKey) : [...prev, tokenKey]
    );
  };

  const selectedNode = draftNodes.find((n) => n.nodeId === selectedNodeId) ?? null;

  return (
    <section style={{ marginBottom: "24px" }}>
      <h2>Visual Layout Builder (Issue #89)</h2>

      {/* Boundary declaration: frontend is projection surface only */}
      <p
        style={{
          background: "#fffbe6",
          border: "1px solid #e6c97a",
          borderRadius: "4px",
          padding: "8px 12px",
          fontSize: "0.85rem",
          color: "#665500",
          marginBottom: "12px",
        }}
      >
        <strong>Projection surface boundary:</strong> Frontend holds draft layout state only.
        Topology persistence authority is DB-side (<code>ui_layout_registry</code> /
        <code>ui_topology_tensor</code>). Apply always routes through{" "}
        <code>layout_patch:preview &rarr; validate &rarr; apply</code> — no direct DB write.
      </p>

      {/* Layout identity */}
      <div style={{ display: "flex", gap: "8px", marginBottom: "12px", flexWrap: "wrap" }}>
        <input
          value={layoutId}
          onInput={(e) => setLayoutId((e.target as HTMLInputElement).value)}
          placeholder="layoutId (UUID — from package generator promote)"
          style={{ padding: "6px 8px", fontFamily: "monospace", flex: 2 }}
        />
        <input
          value={routeKey}
          onInput={(e) => setRouteKey((e.target as HTMLInputElement).value)}
          placeholder="routeKey (e.g. /admin/ui-builder)"
          style={{ padding: "6px 8px", fontFamily: "monospace", flex: 1 }}
        />
      </div>

      {/* Main editor: palette + canvas + inspector */}
      <div style={{ display: "flex", gap: "10px", minHeight: "280px", marginBottom: "12px" }}>

        {/* LayoutPalette — drag component kinds from catalog */}
        <LayoutPalette onDragStart={handleDragStartPalette} />

        {/* LayoutCanvas — drop zone with placed nodes */}
        <div
          onDragOver={handleDragOverCanvas}
          onDrop={handleDropOnCanvas}
          style={{
            flex: 1,
            border: "2px dashed #aaa",
            borderRadius: "4px",
            padding: "10px",
            background: "#fdfdfd",
            minHeight: "200px",
            position: "relative",
          }}
        >
          <div style={{ color: "#999", fontSize: "0.75rem", marginBottom: "8px" }}>
            Canvas — drop components here; drag nodes to reorder; click to select
          </div>
          {draftNodes.length === 0 && (
            <div
              style={{
                color: "#ccc",
                textAlign: "center",
                padding: "60px 0",
                fontFamily: "monospace",
                fontSize: "0.85rem",
              }}
            >
              Empty. Drag a component from the palette to start.
            </div>
          )}
          {draftNodes.map((node, idx) => (
            <div
              key={node.nodeId}
              draggable={true}
              onDragStart={() => handleDragStartCanvas(node.nodeId)}
              onClick={() =>
                setSelectedNodeId(
                  node.nodeId === selectedNodeId ? null : node.nodeId
                )
              }
              style={{
                padding: "7px 10px",
                marginBottom: "4px",
                border: `2px solid ${
                  node.nodeId === selectedNodeId
                    ? "#0070f3"
                    : node.isDraftOnly
                    ? "#e6c97a"
                    : "#bde"
                }`,
                borderRadius: "3px",
                background:
                  node.nodeId === selectedNodeId
                    ? "#f0f7ff"
                    : node.isDraftOnly
                    ? "#fffbe6"
                    : "#fff",
                cursor: "grab",
                fontFamily: "monospace",
                fontSize: "0.82rem",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                userSelect: "none",
              }}
            >
              <div>
                <span style={{ fontWeight: "bold" }}>{node.componentKey}</span>
                {node.isDraftOnly && (
                  <span
                    style={{
                      marginLeft: "6px",
                      padding: "1px 5px",
                      background: "#fffbe6",
                      border: "1px solid #e6c97a",
                      borderRadius: "2px",
                      color: "#a06000",
                      fontSize: "0.68rem",
                    }}
                  >
                    draft-only — apply blocked
                  </span>
                )}
                {node.slotKey && (
                  <span style={{ color: "#666", marginLeft: "8px" }}>
                    slot:<code>{node.slotKey}</code>
                  </span>
                )}
                <span style={{ color: "#bbb", marginLeft: "8px", fontSize: "0.72rem" }}>
                  order:{node.orderIndex} col:{node.gridCol} row:{node.gridRow}
                  {node.parentNodeId ? ` parent:${node.parentNodeId.slice(0, 8)}` : ""}
                </span>
              </div>
              <div style={{ display: "flex", gap: "3px", flexShrink: 0 }}>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    moveLayoutNode(node.nodeId, "up");
                  }}
                  disabled={idx === 0}
                  style={{ padding: "1px 5px", fontSize: "0.72rem" }}
                  title="Move up"
                >
                  up
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    moveLayoutNode(node.nodeId, "down");
                  }}
                  disabled={idx === draftNodes.length - 1}
                  style={{ padding: "1px 5px", fontSize: "0.72rem" }}
                  title="Move down"
                >
                  dn
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    removeNode(node.nodeId);
                  }}
                  style={{ padding: "1px 5px", fontSize: "0.72rem", color: "#c00" }}
                  title="Remove node"
                >
                  rm
                </button>
              </div>
            </div>
          ))}
        </div>

        {/* LayoutNodeInspector — slot, parent, grid position assignment */}
        {selectedNode && (
          <LayoutNodeInspector
            node={selectedNode}
            onUpdate={(updates) => updateNode(selectedNode.nodeId, updates)}
            onClose={() => setSelectedNodeId(null)}
          />
        )}
      </div>

      {/* CSS token refs — projected from css-dictionary-ssot.yaml */}
      <div style={{ marginBottom: "12px" }}>
        <h4 style={{ margin: "0 0 6px", fontFamily: "monospace" }}>
          cssTokenRefs{" "}
          <span style={{ color: "#888", fontWeight: "normal", fontSize: "0.8rem" }}>
            (from <code>docs/design/css-dictionary-ssot.yaml</code>)
          </span>
        </h4>
        <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
          {CSS_DICTIONARY_TOKENS.map((t) => (
            <label
              key={t.tokenKey}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "4px",
                padding: "3px 7px",
                border: `1px solid ${selectedTokenRefs.includes(t.tokenKey) ? "#0070f3" : "#ddd"}`,
                borderRadius: "3px",
                cursor: "pointer",
                fontSize: "0.75rem",
                fontFamily: "monospace",
                background: selectedTokenRefs.includes(t.tokenKey) ? "#f0f7ff" : "#fff",
              }}
            >
              <input
                type="checkbox"
                checked={selectedTokenRefs.includes(t.tokenKey)}
                onChange={() => toggleTokenRef(t.tokenKey)}
                style={{ margin: 0 }}
              />
              {t.tokenKey}
            </label>
          ))}
        </div>
        {selectedTokenRefs.length > 0 && (
          <p style={{ color: "#555", fontSize: "0.78rem", marginTop: "4px" }}>
            Selected: {selectedTokenRefs.join(", ")}
          </p>
        )}
      </div>

      {/* Draft tensorPatchJson preview */}
      <div style={{ marginBottom: "12px" }}>
        <h4 style={{ margin: "0 0 4px", fontFamily: "monospace" }}>Draft tensorPatchJson</h4>
        <pre
          style={{
            background: "#f0f0f0",
            padding: "8px",
            borderRadius: "3px",
            fontSize: "0.78rem",
            maxHeight: "140px",
            overflowY: "auto",
            margin: 0,
          }}
        >
          {tensorPatchJson}
        </pre>
      </div>

      {/* Apply readiness: warn if any draft-only nodes are present */}
      {draftNodes.some((n) => n.isDraftOnly) && (
        <p
          style={{
            background: "#fffbe6",
            border: "1px solid #e6c97a",
            borderRadius: "3px",
            padding: "6px 10px",
            fontSize: "0.8rem",
            color: "#a06000",
            marginBottom: "8px",
          }}
        >
          <strong>Apply blocked:</strong>{" "}
          {draftNodes.filter((n) => n.isDraftOnly).length} node(s) marked (draft-only) have no
          DB-registered identity. Preview and Validate are allowed. To unlock Apply, promote
          these components via Component Bucket &rarr; Package Generator above.
        </p>
      )}

      {/* layout_patch actions — preview/validate route to backend; apply is explicit only */}
      <div style={{ display: "flex", gap: "8px", marginBottom: "10px" }}>
        <button
          onClick={() => callLayoutPatch("preview")}
          disabled={loading}
          style={{ padding: "6px 14px" }}
        >
          Preview
        </button>
        <button
          onClick={() => callLayoutPatch("validate")}
          disabled={loading}
          style={{ padding: "6px 14px" }}
        >
          Validate
        </button>
        <button
          onClick={() => callLayoutPatch("apply")}
          disabled={loading}
          style={{ padding: "6px 14px", background: "#0a7a33", color: "#fff", border: "none" }}
        >
          Apply (explicit — routes through layout_patch:apply)
        </button>
        {draftNodes.length > 0 && (
          <button
            onClick={() => {
              setDraftNodes([]);
              setSelectedNodeId(null);
            }}
            style={{ padding: "6px 14px", color: "#c00" }}
          >
            Clear canvas
          </button>
        )}
      </div>

      {loading && <p style={{ color: "#888", fontFamily: "monospace" }}>Processing...</p>}
      {error && (
        <p style={{ color: "#c00", fontFamily: "monospace" }}>
          <strong>{error}</strong>
        </p>
      )}
      {result && (
        <pre
          style={{
            background: "#f8f8f8",
            padding: "8px",
            fontSize: "0.8rem",
            maxHeight: "200px",
            overflowY: "auto",
          }}
        >
          {result}
        </pre>
      )}
    </section>
  );
}

export default function UiBuilderAdmin(): JSX.Element {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / ui-builder</h1>
      <p>
        <a href="/admin">&larr; admin index</a>
      </p>
      <p style={{ color: "#555" }}>
        UI component system (Issue #86) and visual layout builder (Issue #89).
        See <code>docs/registrar-admin-ui-specification.md</code> §2.5 for component-to-DB flow.
      </p>

      <hr style={{ margin: "16px 0" }} />

      <PrimitiveCatalog />
      <BucketSection />
      <CssTokenSelectorSection />
      <LayoutBuilderSection />
    </main>
  );
}
