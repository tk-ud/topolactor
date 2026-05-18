import { JSX } from "preact";

export type HubOverviewProps = {
  hubId: string;
  relationName: string;
  stateName: string;
  entityCount: number;
  note?: string;
};

export function HubOverviewCard(props: HubOverviewProps): JSX.Element {
  return (
    <section style={{ border: "1px solid #ccc", padding: "16px", marginBottom: "16px" }}>
      <h3 style={{ marginTop: 0 }}>Hub: {props.relationName}</h3>
      <dl style={{ display: "grid", gridTemplateColumns: "140px 1fr", gap: "4px 8px" }}>
        <dt>hub_id</dt><dd><code>{props.hubId}</code></dd>
        <dt>state</dt><dd>{props.stateName}</dd>
        <dt>entity_count</dt><dd>{props.entityCount}</dd>
        {props.note && <><dt>note</dt><dd>{props.note}</dd></>}
      </dl>
    </section>
  );
}
