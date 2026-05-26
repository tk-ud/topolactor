import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type UndoTimelineItem = {
  id: string;
  label: string;
  timestamp?: string;
};

export type UndoTimelineProps = ComponentIdentityProps & {
  items?: UndoTimelineItem[];
  selectedId?: string;
  onSelect?: (id: string) => void;
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function UndoTimeline({
  items,
  selectedId,
  onSelect,
  title,
  className,
  design,
}: UndoTimelineProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:0;font-family:monospace;background:#fff;border:1px solid #e0e0e0;border-radius:6px;overflow:hidden",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="padding:10px 14px;background:#f8f9fa;border-bottom:1px solid #e0e0e0;font-size:0.85rem;font-weight:600;color:#444">
          {title}
        </div>
      )}
      {(!items || items.length === 0)
        ? (
          <div style="padding:12px 14px;color:#aaa;font-size:0.88rem">No history entries.</div>
        )
        : (
          <div style="display:flex;flex-direction:column">
            {items.map((item) => {
              const isSelected = item.id === selectedId;
              return (
                <div
                  key={item.id}
                  onClick={() => onSelect && onSelect(item.id)}
                  style={`display:flex;align-items:center;justify-content:space-between;padding:9px 14px;border-bottom:1px solid #f0f0f0;font-size:0.88rem;cursor:${onSelect ? "pointer" : "default"};background:${isSelected ? "#e8f0fe" : "transparent"};border-left:3px solid ${isSelected ? "#1a73e8" : "transparent"}`}
                  onMouseEnter={(e) => {
                    if (onSelect && !isSelected) {
                      (e.currentTarget as HTMLDivElement).style.background = "#f8f9fa";
                    }
                  }}
                  onMouseLeave={(e) => {
                    if (onSelect && !isSelected) {
                      (e.currentTarget as HTMLDivElement).style.background = "transparent";
                    }
                  }}
                >
                  <span style={`color:${isSelected ? "#1a73e8" : "#222"};font-weight:${isSelected ? "600" : "400"}`}>
                    {item.label}
                  </span>
                  {item.timestamp && (
                    <span style="font-size:0.78rem;color:#aaa;margin-left:12px;flex-shrink:0">
                      {item.timestamp}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        )}
    </div>
  );
}
