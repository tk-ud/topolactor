/**
 * /admin/contents — Content / topology data management skeleton.
 *
 * Skeleton route per SSOT frontend_routes.admin.
 * Full content management UI is out of scope for this skeleton.
 */
export default function ContentsAdmin() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / contents</h1>
      <p>
        <a href="/admin">&larr; admin index</a>
      </p>
      <p>
        Content and topology data management UI skeleton. This route is a
        placeholder for topology content editing defined in{" "}
        <code>docs/registrar-admin-ui-specification.md</code>.
      </p>
      <p style={{ color: "#888" }}>
        Status: <strong>not_started</strong> — content management UI pending
        implementation (Issue #86).
      </p>
      <section>
        <h2>Planned capabilities</h2>
        <ul>
          <li>Browse topology entities, hubs, and relations</li>
          <li>Draft registration and ref validation before promotion</li>
          <li>Promote to active registry state</li>
          <li>Broken refs surfaced as explicit validation errors</li>
        </ul>
      </section>
    </main>
  );
}
