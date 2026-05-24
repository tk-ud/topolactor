import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";

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
      <h2>Primitive Component Catalog (Issue #86)</h2>
      <p style={{ color: "#555", fontSize: "0.9rem" }}>
        Primitive components are defined in <code>frontend/components/</code>.
        Components must be registered in the UI topology DB (via the package generator) to become
        topology tensor entities. Code-only components are treated as drift/GAP.
      </p>
      <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%", fontFamily: "monospace" }}>
        <thead>
          <tr>
            {["component_key", "kind", "source_path", "status"].map((h) => (
              <th key={h} style={{ textAlign: "left", borderBottom: "2px solid #ccc", padding: "4px 8px", background: "#f5f5f5" }}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {[
            { key: "button.primitive", kind: "primitive", path: "frontend/components/Button.tsx", status: "code-only (drift)" },
            { key: "input.primitive",  kind: "primitive", path: "frontend/components/Input.tsx",  status: "code-only (drift)" },
            { key: "table.primitive",  kind: "primitive", path: "frontend/components/Table.tsx",  status: "code-only (drift)" },
            { key: "card.primitive",   kind: "primitive", path: "frontend/components/Card.tsx",   status: "code-only (drift)" },
          ].map((c) => (
            <tr key={c.key} style={{ borderBottom: "1px solid #eee" }}>
              <td style={{ padding: "4px 8px" }}><code>{c.key}</code></td>
              <td style={{ padding: "4px 8px" }}>{c.kind}</td>
              <td style={{ padding: "4px 8px" }}><code>{c.path}</code></td>
              <td style={{ padding: "4px 8px", color: "#c80" }}>{c.status}</td>
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
          `Package generated: tensorId=${data?.tensorId}, componentId=${data?.componentId}, packageId=${data?.packageId}`
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
      <h2>Component Bucket → Package Generator</h2>
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
              Generate package
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
      <LayoutBuilderSection />
    </main>
  );
}
