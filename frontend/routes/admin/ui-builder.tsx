import { useEffect, useState } from "preact/hooks";
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

function LayoutBuilderSection(): JSX.Element {
  return (
    <section style={{ marginBottom: "24px" }}>
      <h2>Visual Layout Builder (Issue #89 — skeleton)</h2>
      <p style={{ color: "#555", fontSize: "0.9rem" }}>
        Layout builder connects component packages to route-specific layout definitions.
        <strong> Planned (not yet implemented):</strong> mouse-driven layout editor and
        style token / responsive rule management on top of <code>ui_layout_registry</code>.
        Frontend adapter is a stable projection surface; spec changes via registry tensor data.
      </p>
      <p style={{ color: "#888" }}>
        Status: <strong>partial</strong> — layout registration wired through package generator
        (layoutId issued per PromoteBucketItemAsync). Full mouse-driven layout editor pending.
      </p>
      <section style={{ background: "#f5f5f5", padding: "12px", borderRadius: "4px" }}>
        <h3 style={{ marginTop: 0 }}>Planned capabilities</h3>
        <ul>
          <li>Layout structure composition with explicit layoutId bindings</li>
          <li>Mouse-driven layout editing UI (drag/drop and placement controls)</li>
          <li>Style token and responsive rule authoring/management wired to <code>ui_layout_registry</code></li>
          <li>Component bucket → package generator → UI topology DB pipeline (wired above)</li>
          <li>Frontend adapter as fixed projection surface; spec changes via registry tensor</li>
        </ul>
      </section>
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
