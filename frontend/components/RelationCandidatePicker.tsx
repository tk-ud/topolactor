import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type RelationCandidate = {
  id: string;
  label: string;
  score?: number;
};

export type RelationCandidatePickerProps = ComponentIdentityProps & {
  candidates?: RelationCandidate[];
  selectedId?: string;
  onSelect: (id: string) => void;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function RelationCandidatePicker({
  candidates,
  selectedId,
  onSelect,
  label,
  className,
  design,
}: RelationCandidatePickerProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:6px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <div style="font-size:0.85rem;color:#555;font-weight:600">{label}</div>
      )}
      {(!candidates || candidates.length === 0)
        ? (
          <div style="color:#aaa;font-size:0.88rem;padding:8px 0">No candidates.</div>
        )
        : (
          <div style="display:flex;flex-direction:column;gap:2px">
            {candidates.map((c) => {
              const isSelected = c.id === selectedId;
              return (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => onSelect(c.id)}
                  style={`display:flex;align-items:center;justify-content:space-between;padding:7px 10px;border-radius:4px;border:1px solid ${isSelected ? "#1a73e8" : "#e0e0e0"};background:${isSelected ? "#e8f0fe" : "#fff"};cursor:pointer;font-family:monospace;font-size:0.9rem;text-align:left;width:100%`}
                >
                  <span style={`color:${isSelected ? "#1a73e8" : "#222"};font-weight:${isSelected ? "600" : "400"}`}>
                    {c.label}
                  </span>
                  {c.score !== undefined && (
                    <span style="background:#f1f3f4;border:1px solid #ddd;border-radius:10px;padding:1px 7px;font-size:0.78rem;color:#666">
                      {Math.round(c.score * 100)}%
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        )}
    </div>
  );
}
