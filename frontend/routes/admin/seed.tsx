import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";

/**
 * /admin/seed — Seed Import/Export Runtime management.
 *
 * Per Issue #84 SSOT position:
 *   Seed Runtime is a controlled registration boundary.
 *   /storage/seed.json is a UI-managed topology payload candidate.
 *   The canonical runtime authority is topolactor DB.
 *
 * Seed format:
 *   { "version": "1", "runtimes": [{ "name", "target", "layer", "action" }] }
 *
 * Operations: save, load, validate, preview, import.
 * import is a skeleton — full canonical import requires Gap-1 resolution.
 * Import failure is explicit (fail-close). No silent fallback.
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

async function dispatchSeedOp(layer: string, action: string, payload?: unknown) {
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
  const body = await res.json();
  return body;
}

type SeedValidationError = { code: string; message: string };
type SeedRuntime = { name: string; target: string; layer: string; action: string };

export default function SeedAdmin(): JSX.Element {
  const [seedContent, setSeedContent] = useState<string>("");
  const [loadedContent, setLoadedContent] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [errors, setErrors] = useState<SeedValidationError[]>([]);
  const [previewRuntimes, setPreviewRuntimes] = useState<SeedRuntime[]>([]);
  const [importResult, setImportResult] = useState<{ importedCount: number; note?: string } | null>(null);
  const [loading, setLoading] = useState(false);

  const clearState = () => {
    setStatus(null);
    setErrors([]);
    setPreviewRuntimes([]);
    setImportResult(null);
  };

  const handleLoad = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "load");
      if (body?.emission?.resolvedData?.content) {
        setLoadedContent(body.emission.resolvedData.content);
        setStatus("Loaded seed.json from /storage.");
      } else if (body?.errors?.length) {
        setErrors(body.errors);
        setStatus("Load failed.");
      } else {
        setStatus("seed.json not found or backend not configured.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    clearState();
    if (!seedContent.trim()) {
      setStatus("Content is empty. Enter seed JSON before saving.");
      return;
    }
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "save", { content: seedContent });
      if (body?.success) {
        setStatus("Saved seed.json to /storage.");
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Save failed.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "validate");
      if (body?.emission?.resolvedData?.valid) {
        setStatus("Validation passed. seed.json is structurally valid.");
      } else {
        setErrors(body?.emission?.resolvedData?.errors ?? body?.errors ?? []);
        setStatus("Validation failed.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handlePreview = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "preview");
      const data = body?.emission?.resolvedData;
      if (data?.runtimes) {
        setPreviewRuntimes(data.runtimes);
        setStatus(`Preview: ${data.runtimeCount} runtime(s) declared.`);
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Preview failed.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleImport = async () => {
    clearState();
    setLoading(true);
    try {
      const body = await dispatchSeedOp("seed_runtime", "import");
      if (body?.success || body?.emission?.resolvedData?.ok) {
        const data = body?.emission?.resolvedData;
        setImportResult({ importedCount: data?.importedCount ?? 0, note: data?.note });
        setStatus("Import completed (skeleton).");
      } else {
        setErrors(body?.errors ?? []);
        setStatus("Import failed.");
      }
    } catch (e) {
      setStatus(`Error: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / seed</h1>
      <p>
        <a href="/admin">&larr; admin index</a>
      </p>
      <p style={{ color: "#555" }}>
        Seed Runtime (Issue #84): save / load / validate / preview / import pipeline for{" "}
        <code>/storage/seed.json</code>.<br />
        Seed file is a UI-managed topology payload candidate. The canonical runtime authority is
        topolactor DB.<br />
        Import is a skeleton — full canonical import requires Gap-1 (manifest-driven routing)
        resolution.
      </p>

      <hr style={{ margin: "16px 0" }} />

      <section style={{ marginBottom: "24px" }}>
        <h2>Seed JSON editor</h2>
        <textarea
          value={seedContent}
          onInput={(e) => setSeedContent((e.target as HTMLTextAreaElement).value)}
          rows={12}
          placeholder={`{\n  "version": "1",\n  "runtimes": [\n    { "name": "example", "target": "default", "layer": "entity", "action": "search" }\n  ]\n}`}
          style={{ width: "100%", fontFamily: "monospace", fontSize: "0.9rem", padding: "8px" }}
        />
        <div style={{ display: "flex", gap: "8px", marginTop: "8px", flexWrap: "wrap" }}>
          <button onClick={handleSave} disabled={loading} style={{ padding: "6px 14px" }}>
            Save to /storage
          </button>
          <button onClick={handleLoad} disabled={loading} style={{ padding: "6px 14px" }}>
            Load from /storage
          </button>
          <button onClick={handleValidate} disabled={loading} style={{ padding: "6px 14px" }}>
            Validate
          </button>
          <button onClick={handlePreview} disabled={loading} style={{ padding: "6px 14px" }}>
            Preview
          </button>
          <button
            onClick={handleImport}
            disabled={loading}
            style={{ padding: "6px 14px", background: "#0070f3", color: "#fff", border: "none" }}
          >
            Import (skeleton)
          </button>
        </div>
      </section>

      {loading && <p style={{ color: "#888" }}>Processing...</p>}

      {status && (
        <p style={{ color: errors.length > 0 ? "#c00" : "#090" }}>
          <strong>{status}</strong>
        </p>
      )}

      {errors.length > 0 && (
        <section style={{ marginBottom: "16px" }}>
          <h3 style={{ color: "#c00" }}>Errors</h3>
          <ul>
            {errors.map((e, i) => (
              <li key={i}>
                <code>{e.code}</code>: {e.message}
              </li>
            ))}
          </ul>
        </section>
      )}

      {loadedContent && (
        <section style={{ marginBottom: "16px" }}>
          <h3>Loaded content</h3>
          <pre
            style={{
              background: "#f5f5f5",
              padding: "12px",
              overflowX: "auto",
              fontSize: "0.85rem",
            }}
          >
            {loadedContent}
          </pre>
          <button
            onClick={() => setSeedContent(loadedContent)}
            style={{ padding: "4px 10px", fontSize: "0.85rem" }}
          >
            Copy to editor
          </button>
        </section>
      )}

      {previewRuntimes.length > 0 && (
        <section style={{ marginBottom: "16px" }}>
          <h3>Preview — declared runtimes</h3>
          <table cellPadding="6" style={{ borderCollapse: "collapse", width: "100%" }}>
            <thead>
              <tr>
                {["name", "target", "layer", "action"].map((h) => (
                  <th
                    key={h}
                    style={{ textAlign: "left", borderBottom: "2px solid #ccc", padding: "4px 8px" }}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {previewRuntimes.map((r, i) => (
                <tr key={i} style={{ borderBottom: "1px solid #eee" }}>
                  <td style={{ padding: "4px 8px" }}>{r.name}</td>
                  <td style={{ padding: "4px 8px" }}>{r.target}</td>
                  <td style={{ padding: "4px 8px" }}>{r.layer}</td>
                  <td style={{ padding: "4px 8px" }}>{r.action}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}

      {importResult && (
        <section style={{ marginBottom: "16px" }}>
          <h3 style={{ color: "#090" }}>Import result</h3>
          <p>
            Imported count: <strong>{importResult.importedCount}</strong>
          </p>
          {importResult.note && (
            <p style={{ color: "#888", fontSize: "0.85rem" }}>{importResult.note}</p>
          )}
        </section>
      )}

      <hr style={{ margin: "24px 0" }} />
      <section>
        <h2>Seed file format</h2>
        <pre style={{ background: "#f5f5f5", padding: "12px", fontSize: "0.85rem" }}>
          {`{\n  "version": "1",\n  "runtimes": [\n    {\n      "name": "string (required)",\n      "target": "string (required)",\n      "layer": "string (required)",\n      "action": "string (required)",\n      "payload": {} // optional\n    }\n  ]\n}`}
        </pre>
      </section>
    </main>
  );
}
