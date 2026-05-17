import { JSX } from "preact";
import { HubOverviewCard } from "../components/HubOverviewCard.tsx";
import { EntityTableProjection, type EntityRow } from "../components/EntityTableProjection.tsx";
import { RecommendationPanel } from "../components/RecommendationPanel.tsx";
import { ContextTokenBadgeList, type ContextToken } from "../components/ContextTokenBadgeList.tsx";

const demoHub = {
  hubId: "00000000-0000-0000-0000-000000000010",
  relationName: "demo_relation",
  stateName: "active",
  entityCount: 3,
  note: "Demo hub — resolved from demo_seed.sql via attractor_resolve",
};

const demoEntities: EntityRow[] = [
  { entityId: "00000000-0000-0000-0000-000000000041", label: "Alpha Entity",   state: "active",    relations: ["demo_relation"] },
  { entityId: "00000000-0000-0000-0000-000000000042", label: "Beta Entity",    state: "operating", relations: ["demo_relation"] },
  { entityId: "00000000-0000-0000-0000-000000000043", label: "Gamma Entity",   state: "active",    relations: ["demo_relation"] },
];

const demoTokens: ContextToken[] = [
  { tokenId: "00000000-0000-0000-0000-000000000021", label: "active",   group: "status", value: 1.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000022", label: "warning",  group: "status", value: 0.0,  status: "active" },
  { tokenId: "00000000-0000-0000-0000-000000000023", label: "critical", group: "status", value: -1.0, status: "active" },
];

export default function Demo(): JSX.Element {
  return (
    <main style={{ fontFamily: "sans-serif", padding: "24px", maxWidth: "900px" }}>
      <h1>topolactor — public scaffold demo</h1>

      <div style={{
        background: "#fffbe6",
        border: "1px solid #e6c700",
        borderRadius: "4px",
        padding: "12px 16px",
        marginBottom: "24px",
      }}>
        <strong>Scaffold notice:</strong> This page displays demo scaffold data from{" "}
        <code>db/demo_seed.sql</code>. It is NOT production UI. No real business data is used.
        The canonical runtime route (<code>operation → vector → attractor → structure_map → package → schema → emission</code>) is maintained.
      </div>

      <section style={{ marginBottom: "24px" }}>
        <h2>What to observe</h2>
        <ol>
          <li>Change a token <code>value</code> in <code>context_token_registry</code> → re-run recommendation → score changes.</li>
          <li>Change <code>context_route_policy_ref</code> in <code>structure_maps.state_policy</code> → different policy loads from <code>function_parameters</code>.</li>
          <li>Change <code>transition_aggregation.aggregation_limit</code> in <code>function_parameters</code> → windowed stats scope changes.</li>
        </ol>
      </section>

      <h2>Hub Projection</h2>
      <HubOverviewCard {...demoHub} />

      <h2>Entity Table Projection</h2>
      <EntityTableProjection entities={demoEntities} schemaName="demo_entity_schema" />

      <h2>Context Token Registry</h2>
      <ContextTokenBadgeList tokens={demoTokens} activeOnly />

      <h2>Context Route Recommendation (scaffold placeholder)</h2>
      <RecommendationPanel
        status="insufficient_history"
        statusDetail="NO_CONTEXT_HISTORY — seed context_event rows to see recommendations"
        nextOperations={[]}
        nextTokens={[]}
      />

      <hr style={{ marginTop: "32px" }} />
      <p style={{ color: "#888", fontSize: "0.85em" }}>
        See <a href="/admin">admin</a> for topology inspection,{" "}
        <a href="/runtime-status">runtime-status</a> for pipeline step validation,{" "}
        <a href="/">index</a> for the dispatch panel.
        Demo walkthrough: <code>docs/demo-walkthrough.md</code>.
      </p>
    </main>
  );
}
