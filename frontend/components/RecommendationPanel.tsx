import { JSX } from "preact";

export type RecommendationCandidate = {
  value: string;
  score: number;
  evidence: string[];
};

export type RecommendationPanelProps = {
  status: "ok" | "insufficient_history" | "explicit_error";
  statusDetail?: string;
  nextOperations: RecommendationCandidate[];
  nextTokens: RecommendationCandidate[];
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
          <h4>Next Operations</h4>
          <CandidateList candidates={props.nextOperations} />
          <h4>Next Token Candidates</h4>
          <CandidateList candidates={props.nextTokens} />
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
          {c.evidence.length > 0 && (
            <small style={{ color: "#666" }}> ({c.evidence.join(", ")})</small>
          )}
        </li>
      ))}
    </ol>
  );
}
