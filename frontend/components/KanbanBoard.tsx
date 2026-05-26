import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type KanbanBoardItem = {
  id: string;
  label: string;
  description?: string;
};

export type KanbanBoardColumn = {
  key: string;
  label: string;
  items?: KanbanBoardItem[];
};

export type KanbanBoardProps = ComponentIdentityProps & {
  title?: string;
  columns?: KanbanBoardColumn[];
  onSelect?: (id: string) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function KanbanBoard({
  title,
  columns = [],
  onSelect,
  className,
  design,
}: KanbanBoardProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "font-family:sans-serif;font-size:0.9rem",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;color:#333;margin-bottom:12px;font-size:0.95rem">
          {title}
        </div>
      )}
      {columns.length === 0
        ? (
          <div style="color:#aaa">No columns.</div>
        )
        : (
          <div style="display:flex;gap:12px;overflow-x:auto;align-items:flex-start">
            {columns.map((col) => (
              <div
                key={col.key}
                style="min-width:200px;flex:1;background:#f5f5f5;border-radius:6px;padding:12px;border:1px solid #e0e0e0"
              >
                <div style="font-weight:600;color:#555;font-size:0.82rem;text-transform:uppercase;letter-spacing:0.05em;margin-bottom:8px">
                  {col.label}
                </div>
                {(!col.items || col.items.length === 0)
                  ? (
                    <div style="color:#bbb;font-size:0.82rem">Empty</div>
                  )
                  : (
                    <div style="display:flex;flex-direction:column;gap:6px">
                      {col.items.map((item) => (
                        <div
                          key={item.id}
                          onClick={onSelect ? () => onSelect(item.id) : undefined}
                          style={`background:#fff;border:1px solid #e0e0e0;border-radius:4px;padding:8px 10px;${
                            onSelect
                              ? "cursor:pointer;"
                              : ""
                          }`}
                        >
                          <div style="font-weight:500;color:#222;font-size:0.88rem">
                            {item.label}
                          </div>
                          {item.description && (
                            <div style="color:#888;font-size:0.8rem;margin-top:3px">
                              {item.description}
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
              </div>
            ))}
          </div>
        )}
    </div>
  );
}
