/**
 * /admin/manifests — Manifest management skeleton.
 *
 * Skeleton route per SSOT frontend_routes.admin.
 * Full manifest CRUD (draft -> active -> deprecated lifecycle) is out of scope
 * for this skeleton; see docs/promotion-manifest-editor-specification.md.
 *
 * When manifest_dispatcher (Gap-1) and the manifest admin UI (Issue #86, #89)
 * are implemented, this route will display active manifests and allow editing.
 */
export default function ManifestsAdmin() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / manifests</h1>
      <p>
        <a href="/admin">&larr; admin index</a>
      </p>
      <p>
        Manifest management UI skeleton. This route is a placeholder for the
        manifest editor defined in{" "}
        <code>docs/promotion-manifest-editor-specification.md</code>.
      </p>
      <p style={{ color: "#888" }}>
        Status: <strong>not_started</strong> — manifest CRUD and promotion UI
        pending implementation (Issue #86, #89).
      </p>
      <section>
        <h2>Planned capabilities</h2>
        <ul>
          <li>List active / draft / deprecated manifests from DB</li>
          <li>Edit manifest topology vectors (dispatcher_mapping, runtime_mapping, ui_projection)</li>
          <li>Promote draft manifest to active</li>
          <li>Deprecate active manifest</li>
          <li>Validate refs before promotion (broken refs are explicit errors)</li>
        </ul>
      </section>
    </main>
  );
}
