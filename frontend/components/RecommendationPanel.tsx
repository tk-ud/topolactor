import { JSX } from "preact";

export type RecommendationCandidate = {
  value: string;
  score: number;
  evidence: string[];
  lane: "ui_pressure" | "state_pressure";
};

export type RecommendationPanelProps = {
  status: "ok" | "insufficient_history" | "explicit_error";
  statusDetail?: string;
  nextOperations: RecommendationCandidate[];
  nextTokens: RecommendationCandidate[];
  nextEnumItems?: RecommendationCandidate[];
};

export function RecommendationPanel(props: RecommendationPanelProps): JSX.Element {
  return (
    <section style={{ border: "1px solid #ddd", padding: "16px", marginBottom: "16px" }}>
      <h3 style={{ marginTop: 0 }}>Context Route Recommendation</h3>
      <p>
        <strong>status:</strong>{" "}
        <code style={{ color: props.status === "ok" ? "#060" : props.status === "explicit_error" ? "#c00" : "#880" }}>
          {props.status}
        </code>
        {props.statusDetail && <span style={{ color: "#666" }}> — {props.statusDetail}</span>}
      </p>
      {props.status === "ok" && (
        <>
          <h4>UI / operation pressure</h4>
          <h5>next_operation</h5>
          <CandidateList candidates={props.nextOperations.filter((candidate) => candidate.lane === "ui_pressure")} />
          <h5>next_component / next_route_action candidates</h5>
          <CandidateList candidates={props.nextTokens.filter((candidate) => candidate.lane === "ui_pressure")} />
          <h4>State / enum pressure</h4>
          <h5>next_enum_item / likely_status / state_shift_candidate</h5>
          <CandidateList candidates={(props.nextEnumItems ?? []).filter((candidate) => candidate.lane === "state_pressure")} />
        </>
      )}
    </section>
  );
}

function CandidateList({ candidates }: { candidates: RecommendationCandidate[] }): JSX.Element {
  if (candidates.length === 0) {
    return <p style={{ color: "#888" }}>none</p>;
  }
  return (
    <ol>
      {candidates.map((c, i) => (
        <li key={i}>
          <code>{c.value}</code> — score: {c.score.toFixed(3)}
          {c.lane && <small style={{ color: "#666" }}> [{c.lane}]</small>}
          {c.evidence.length > 0 && (
            <small style={{ color: "#666" }}> ({c.evidence.join(", ")})</small>
          )}
        </li>
      ))}
    </ol>
  );
}
