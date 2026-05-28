import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  listImportManifests,
  listImportSchemas,
  uploadImportPreview,
  applyImport,
  type AdminImportManifestItem,
  type AdminImportSchemaItem,
  type AdminImportRecordPreview,
  type AdminImportPreviewResult,
  type AdminImportApplyResult,
} from "../../api/adminApi.ts";

/**
 * /admin/import — Admin CSV/JSON Import (M6 validate-preview-apply)
 *
 * Flow:
 *   1. Select manifest and schema
 *   2. Upload CSV or JSON file
 *   3. Preview: backend parses + validates; preview table shows valid/invalid rows
 *   4. Apply: explicit user action; backend writes apply_log (staged at MVP)
 *
 * Canonical runtime authority: topolactor DB (backend runtime boundary).
 * Frontend does NOT write directly to DB.
 * preview: canonical state is NOT mutated.
 * apply: apply_log written with status='staged' (MVP partial boundary).
 */

export default function AdminImport(): JSX.Element {
  const [manifests, setManifests] = useState<AdminImportManifestItem[]>([]);
  const [schemas, setSchemas] = useState<AdminImportSchemaItem[]>([]);
  const [selectedManifestId, setSelectedManifestId] = useState<string>("");
  const [selectedSchemaId, setSelectedSchemaId] = useState<string>("");
  const [sourceType, setSourceType] = useState<"csv" | "json">("csv");
  const [fileName, setFileName] = useState<string>("");
  const [fileContent, setFileContent] = useState<string>("");
  const [preview, setPreview] = useState<AdminImportPreviewResult | null>(null);
  const [applyResult, setApplyResult] = useState<AdminImportApplyResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingSelectors, setLoadingSelectors] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [m, s] = await Promise.all([listImportManifests(), listImportSchemas()]);
        setManifests(m ?? []);
        setSchemas(s ?? []);
      } catch {
        // selectors remain empty; user sees empty dropdowns
      } finally {
        setLoadingSelectors(false);
      }
    })();
  }, []);

  const handleFileChange = (e: Event) => {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    setFileName(file.name);
    const detectedType = file.name.toLowerCase().endsWith(".json") ? "json" : "csv";
    setSourceType(detectedType);
    const reader = new FileReader();
    reader.onload = (ev) => {
      setFileContent((ev.target?.result as string) ?? "");
    };
    reader.readAsText(file);
  };

  const handlePreview = async () => {
    setError(null);
    setPreview(null);
    setApplyResult(null);

    if (!selectedManifestId) { setError("Select a manifest before uploading."); return; }
    if (!selectedSchemaId) { setError("Select a schema before uploading."); return; }
    if (!fileContent) { setError("No file content. Select a file first."); return; }

    setLoading(true);
    try {
      const result = await uploadImportPreview(
        sourceType, fileName || "upload", selectedManifestId, selectedSchemaId, fileContent,
      );
      setPreview(result);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  const handleApply = async () => {
    if (!preview?.snapshotId) return;
    setError(null);
    setApplyResult(null);
    setLoading(true);
    try {
      const result = await applyImport(preview.snapshotId);
      setApplyResult(result);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / import</h1>
      <p><a href="/admin">&larr; admin index</a></p>
      <p style={{ color: "#555" }}>
        CSV/JSON import (M6): select manifest + schema, upload file, preview validate results,
        then explicitly apply.<br />
        Preview does NOT mutate canonical state. Apply writes apply_log (staged at MVP).
      </p>

      <hr style={{ margin: "16px 0" }} />

      <section style={{ marginBottom: "24px" }}>
        <h2>1. Select manifest and schema</h2>
        {loadingSelectors ? (
          <p style={{ color: "#888" }}>Loading selectors…</p>
        ) : (
          <div style={{ display: "flex", gap: "16px", flexWrap: "wrap", alignItems: "flex-end" }}>
            <label>
              Manifest
              <br />
              <select
                value={selectedManifestId}
                onChange={(e) => setSelectedManifestId((e.target as HTMLSelectElement).value)}
                style={{ minWidth: "260px", fontFamily: "monospace", padding: "4px" }}
              >
                <option value="">— select manifest —</option>
                {manifests.map((m) => (
                  <option key={m.manifestId} value={m.manifestId}>
                    {m.manifestId.slice(0, 8)}… [{m.status}]
                  </option>
                ))}
              </select>
            </label>
            <label>
              Schema
              <br />
              <select
                value={selectedSchemaId}
                onChange={(e) => setSelectedSchemaId((e.target as HTMLSelectElement).value)}
                style={{ minWidth: "220px", fontFamily: "monospace", padding: "4px" }}
              >
                <option value="">— select schema —</option>
                {schemas.map((s) => (
                  <option key={s.schemaId} value={s.schemaId}>
                    {s.name} ({s.schemaId.slice(0, 8)}…)
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
      </section>

      <section style={{ marginBottom: "24px" }}>
        <h2>2. Upload CSV or JSON file</h2>
        <div style={{ display: "flex", gap: "12px", alignItems: "center", flexWrap: "wrap" }}>
          <input type="file" accept=".csv,.json" onChange={handleFileChange} />
          {fileName && (
            <span style={{ color: "#555" }}>
              {fileName} ({sourceType.toUpperCase()})
            </span>
          )}
        </div>
        {fileContent && (
          <details style={{ marginTop: "8px" }}>
            <summary style={{ cursor: "pointer", color: "#666" }}>Preview raw content</summary>
            <pre style={{ background: "#f5f5f5", padding: "8px", fontSize: "0.8rem", maxHeight: "200px", overflow: "auto" }}>
              {fileContent.slice(0, 2000)}{fileContent.length > 2000 ? "\n…(truncated)" : ""}
            </pre>
          </details>
        )}
      </section>

      <section style={{ marginBottom: "24px" }}>
        <h2>3. Validate &amp; Preview</h2>
        <button
          onClick={handlePreview}
          disabled={loading}
          style={{ padding: "6px 16px", marginRight: "8px" }}
        >
          Preview (validate)
        </button>
        {loading && <span style={{ color: "#888" }}>Processing…</span>}
      </section>

      {error && (
        <p style={{ color: "#c00" }}>
          <strong>Error:</strong> {error}
        </p>
      )}

      {preview && (
        <section style={{ marginBottom: "24px" }}>
          <h2>Preview results</h2>
          <p>
            Snapshot ID: <code>{preview.snapshotId}</code>
            &nbsp;|&nbsp;
            <span style={{ color: "#090" }}>valid: {preview.validCount}</span>
            &nbsp;|&nbsp;
            <span style={{ color: preview.invalidCount > 0 ? "#c00" : "#090" }}>
              invalid: {preview.invalidCount}
            </span>
            &nbsp;|&nbsp; total: {preview.records.length}
          </p>
          <p style={{ color: "#888", fontSize: "0.85rem" }}>
            status = manifest/schema conformity — not business state or hub lifecycle state
          </p>

          <div style={{ overflowX: "auto", maxHeight: "400px", overflowY: "auto" }}>
            <table cellPadding="4" style={{ borderCollapse: "collapse", width: "100%", fontSize: "0.85rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #ccc" }}>
                  <th style={{ textAlign: "left", padding: "4px 8px" }}>#</th>
                  <th style={{ textAlign: "left", padding: "4px 8px" }}>status</th>
                  <th style={{ textAlign: "left", padding: "4px 8px" }}>record</th>
                  <th style={{ textAlign: "left", padding: "4px 8px" }}>errors</th>
                </tr>
              </thead>
              <tbody>
                {preview.records.map((r) => (
                  <tr
                    key={r.rowIndex}
                    style={{ borderBottom: "1px solid #eee", background: r.status === "invalid" ? "#fff5f5" : undefined }}
                  >
                    <td style={{ padding: "4px 8px" }}>{r.rowIndex + 1}</td>
                    <td style={{ padding: "4px 8px", color: r.status === "valid" ? "#090" : "#c00" }}>
                      {r.status}
                    </td>
                    <td style={{ padding: "4px 8px", maxWidth: "400px", overflow: "hidden", whiteSpace: "nowrap", textOverflow: "ellipsis" }}>
                      <code>{JSON.stringify(r.records)}</code>
                    </td>
                    <td style={{ padding: "4px 8px", color: "#c00", fontSize: "0.8rem" }}>
                      {r.validationErrors.join("; ")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div style={{ marginTop: "16px" }}>
            <h2>4. Apply</h2>
            <p style={{ color: "#888", fontSize: "0.85rem" }}>
              Apply targets valid records only ({preview.validCount} row{preview.validCount !== 1 ? "s" : ""}).
              At MVP, apply writes apply_log with status=staged. Canonical entity mutation is not yet implemented.
            </p>
            <button
              onClick={handleApply}
              disabled={loading || preview.validCount === 0 || applyResult !== null}
              style={{ padding: "6px 16px", background: "#0070f3", color: "#fff", border: "none", cursor: "pointer" }}
            >
              Apply ({preview.validCount} valid)
            </button>
          </div>
        </section>
      )}

      {applyResult && (
        <section style={{ marginBottom: "24px" }}>
          <h2>Apply result</h2>
          <p style={{ color: "#060" }}>
            <strong>Apply log written.</strong> applyLogId: <code>{applyResult.applyLogId}</code>
          </p>
          <p>
            applied record count: {applyResult.appliedRecordCount}
            &nbsp;|&nbsp; status: <code>{applyResult.status}</code>
          </p>
          <p style={{ color: "#888", fontSize: "0.85rem" }}>{applyResult.note}</p>
        </section>
      )}

      <hr style={{ margin: "24px 0" }} />
      <section>
        <h2>File format reference</h2>
        <h3>CSV</h3>
        <pre style={{ background: "#f5f5f5", padding: "8px", fontSize: "0.85rem" }}>
          {`name,email,age\nAlice,alice@example.com,30\nBob,bob@example.com,25`}
        </pre>
        <h3>JSON</h3>
        <pre style={{ background: "#f5f5f5", padding: "8px", fontSize: "0.85rem" }}>
          {`[\n  { "name": "Alice", "email": "alice@example.com", "age": 30 },\n  { "name": "Bob", "email": "bob@example.com", "age": 25 }\n]`}
        </pre>
      </section>
    </main>
  );
}
