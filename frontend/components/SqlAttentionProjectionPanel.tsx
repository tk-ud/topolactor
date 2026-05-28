import { JSX } from "preact";

// ---------------------------------------------------------------------------
// SQL Attention Topology Projection Panel
// Child projection surface for SQLA-6 (topology projection recommendation basis).
//
// SSOT boundary:
//   - This component is projection-only: it receives structured backend result and displays it.
//   - No cosine computation, no topology judgment, no MLP feature crossing, no feedback weight update.
//   - Candidates are read-only recommendation guidance; users may navigate or act externally.
//   - Evidence layers (statistics / attention / phase_attention) are carried through from the backend.
// ---------------------------------------------------------------------------

export type TopologyProjectionStatus =
  | "ok"
  | "no_evidence"
  | "missing_policy"
  | "malformed_policy"
  | "db_unavailable";

export type TopologyProjectionCandidate = {
  hubId: string | null;
  attractorKey: string;
  relationRegistryId: string | null;
  attentionScore: number;
  scoreBand: string;
  /** statistics layer: convergence confidence / stability / continuity */
  statisticsJson: string;
  /** attention layer: current excitation / neighbor hit */
  vectorJson: string;
  /** phase_attention layer: exploratory variance / shifted candidate direction */
  phaseVectorJson: string;
  /** scoring provenance */
  evidenceJson: string;
  hitRank: number;
};

export type SqlAttentionProjectionPanelProps = {
  status: TopologyProjectionStatus;
  statusDetail?: string;
  candidates: TopologyProjectionCandidate[];
};

export function SqlAttentionProjectionPanel(
  props: SqlAttentionProjectionPanelProps,
): JSX.Element {
  const statusColor = props.status === "ok"
    ? "#060"
    : props.status === "no_evidence"
    ? "#880"
    : "#c00";

  return (
    <section style={{ border: "1px solid #ddd", padding: "16px", marginBottom: "16px" }}>
      <h3 style={{ marginTop: 0 }}>SQL Attention — Topology Projection</h3>
      <p>
        <strong>status:</strong>{" "}
        <code style={{ color: statusColor }}>{props.status}</code>
        {props.statusDetail && (
          <span style={{ color: "#666" }}> — {props.statusDetail}</span>
        )}
      </p>
      {props.status === "ok" && props.candidates.length > 0 && (
        <>
          <h4>Projection Candidates</h4>
          <CandidateList candidates={props.candidates} />
        </>
      )}
      {props.status === "ok" && props.candidates.length === 0 && (
        <p style={{ color: "#888" }}>No candidates after filtering.</p>
      )}
    </section>
  );
}

function CandidateList(
  { candidates }: { candidates: TopologyProjectionCandidate[] },
): JSX.Element {
  return (
    <ol>
      {candidates.map((c, i) => (
        <li key={i} style={{ marginBottom: "12px" }}>
          <div>
            <code>{c.attractorKey}</code>
            {" "}
            <span
              style={{
                fontSize: "0.75em",
                color: "#555",
                background: "#f0f0f0",
                padding: "1px 4px",
                borderRadius: "3px",
              }}
            >
              {c.scoreBand}
            </span>
            {" "}
            <span style={{ color: "#666" }}>score: {c.attentionScore.toFixed(4)}</span>
          </div>
          <EvidenceBreakdown candidate={c} />
        </li>
      ))}
    </ol>
  );
}

function EvidenceBreakdown(
  { candidate }: { candidate: TopologyProjectionCandidate },
): JSX.Element {
  return (
    <details style={{ marginTop: "4px" }}>
      <summary style={{ fontSize: "0.8em", color: "#555", cursor: "pointer" }}>
        evidence breakdown
      </summary>
      <table style={{ fontSize: "0.78em", borderCollapse: "collapse", marginTop: "4px" }}>
        <tbody>
          <EvidenceRow label="statistics" value={candidate.statisticsJson} />
          <EvidenceRow label="attention" value={candidate.vectorJson} />
          <EvidenceRow label="phase_attention" value={candidate.phaseVectorJson} />
          <EvidenceRow label="provenance" value={candidate.evidenceJson} />
        </tbody>
      </table>
    </details>
  );
}

function EvidenceRow(
  { label, value }: { label: string; value: string },
): JSX.Element {
  return (
    <tr>
      <td
        style={{
          paddingRight: "8px",
          color: "#666",
          verticalAlign: "top",
          whiteSpace: "nowrap",
        }}
      >
        {label}:
      </td>
      <td>
        <code style={{ wordBreak: "break-all" }}>{value}</code>
      </td>
    </tr>
  );
}
