/**
 * /admin/ui-builder — Visual layout builder skeleton.
 *
 * Skeleton route per SSOT frontend_routes.admin.
 * Full visual layout builder is out of scope for this skeleton; see Issue #89.
 * Prerequisite: component system (Issue #86).
 */
export default function UiBuilderAdmin() {
  return (
    <main style={{ fontFamily: "monospace", padding: "2rem" }}>
      <h1>topolactor — admin / ui-builder</h1>
      <p>
        <a href="/admin">&larr; admin index</a>
      </p>
      <p>
        Visual layout builder skeleton. This route is a placeholder for the
        admin layout builder defined in{" "}
        <code>docs/registrar-admin-ui-specification.md</code> (Issue #89).
      </p>
      <p style={{ color: "#888" }}>
        Status: <strong>not_started</strong> — visual layout builder pending
        implementation. Prerequisite: component system (Issue #86).
      </p>
      <section>
        <h2>Planned capabilities</h2>
        <ul>
          <li>Mouse-driven layout construction (layoutId / styleTokenId / responsiveRuleId)</li>
          <li>Tailwind layout token and style token DB management</li>
          <li>Component bucket &rarr; package generator &rarr; UI topology DB pipeline</li>
          <li>Frontend adapter as fixed projection surface; spec changes via registry tensor</li>
        </ul>
      </section>
    </main>
  );
}
