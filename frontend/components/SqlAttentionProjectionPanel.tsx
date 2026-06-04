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
  lane?: "sql_attention_projection";
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

const STATUS_CLASS: Record<TopologyProjectionStatus, string> = {
  ok: "text-green-700",
  no_evidence: "text-yellow-700",
  missing_policy: "text-red-700",
  malformed_policy: "text-red-700",
  db_unavailable: "text-red-700",
};

const STATUS_LABEL: Record<TopologyProjectionStatus, string> = {
  ok: "正常",
  no_evidence: "証拠なし",
  missing_policy: "ポリシー欠落",
  malformed_policy: "ポリシー不正",
  db_unavailable: "DB 利用不可",
};

export function SqlAttentionProjectionPanel(
  props: SqlAttentionProjectionPanelProps,
): JSX.Element {
  const statusClass = STATUS_CLASS[props.status];
  const statusLabel = STATUS_LABEL[props.status];

  return (
    <section class="card mb-4">
      <h3 class="mb-3 mt-0 text-base font-semibold">SQL Attention — トポロジー Projection</h3>
      <p class="text-sm">
        <strong>ステータス:</strong>{" "}
        <code class={statusClass}>{props.status}</code>
        <span class="ml-1 text-gray-500">({statusLabel})</span>
        {props.statusDetail && (
          <span class="text-gray-500"> — {props.statusDetail}</span>
        )}
      </p>
      {props.status === "ok" && props.candidates.length > 0 && (
        <>
          <h4 class="mb-2 mt-4 font-semibold">Projection 候補</h4>
          <CandidateList candidates={props.candidates} />
        </>
      )}
      {props.status === "ok" && props.candidates.length === 0 && (
        <p class="text-muted">フィルタ後の候補はありません。</p>
      )}
    </section>
  );
}

function CandidateList(
  { candidates }: { candidates: TopologyProjectionCandidate[] },
): JSX.Element {
  return (
    <ol class="list-decimal space-y-3 pl-5 text-sm">
      {candidates.map((c, i) => (
        <li key={i}>
          <div>
            <code>{c.attractorKey}</code>
            {" "}
            <span class="rounded bg-gray-100 px-1 py-0.5 text-xs text-gray-600">
              {c.scoreBand}
            </span>
            {" "}
            <span class="text-gray-500">score: {c.attentionScore.toFixed(4)}</span>
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
    <details class="mt-1">
      <summary class="cursor-pointer text-xs text-gray-500">
        エビデンス内訳
      </summary>
      <table class="mt-1 text-xs">
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
      <td class="whitespace-nowrap pr-2 align-top text-gray-500">
        {label}:
      </td>
      <td>
        <code class="break-all">{value}</code>
      </td>
    </tr>
  );
}
