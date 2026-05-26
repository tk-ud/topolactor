import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type CommandItem = {
  id: string;
  label: string;
  description?: string;
};

export type CommandPaletteProps = ComponentIdentityProps & {
  open: boolean;
  onClose: () => void;
  onSelect: (id: string) => void;
  commands?: CommandItem[];
  placeholder?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function CommandPalette({
  open,
  onClose,
  onSelect,
  commands = [],
  placeholder,
  className,
  design,
}: CommandPaletteProps): JSX.Element | null {
  if (!open) return null;
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "background:#fff;border-radius:8px;padding:0;max-width:560px;width:100%;box-shadow:0 8px 32px rgba(0,0,0,0.22);font-family:monospace;overflow:hidden",
    design,
  );
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Command palette"
      style="position:fixed;inset:0;display:flex;align-items:flex-start;justify-content:center;padding-top:80px;background:rgba(0,0,0,0.4);z-index:1000"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={mergedClassName} style={mergedStyle}>
        <div style="display:flex;align-items:center;border-bottom:1px solid #e8e8e8;padding:12px 16px;gap:8px">
          <span style="color:#aaa;font-size:1rem;flex-shrink:0">⌘</span>
          <input
            type="text"
            placeholder={placeholder ?? "Type a command…"}
            aria-label="Search commands"
            style="flex:1;border:none;outline:none;font-size:0.95rem;font-family:monospace;color:#222;background:transparent"
          />
          <button
            onClick={onClose}
            aria-label="Close"
            style="background:none;border:none;font-size:1.1rem;cursor:pointer;color:#aaa;padding:0;flex-shrink:0"
          >
            ✕
          </button>
        </div>
        <ul
          role="listbox"
          aria-label="Commands"
          style="margin:0;padding:4px 0;list-style:none;max-height:360px;overflow-y:auto"
        >
          {commands.length === 0
            ? (
              <li style="padding:14px 16px;color:#aaa;font-size:0.88rem">
                No commands available.
              </li>
            )
            : commands.map((cmd) => (
              <li
                key={cmd.id}
                role="option"
                aria-selected="false"
                style="padding:10px 16px;cursor:pointer;display:flex;flex-direction:column;gap:2px;border-bottom:1px solid #f5f5f5"
                onClick={() => onSelect(cmd.id)}
              >
                <span style="font-size:0.9rem;color:#222;font-weight:600">
                  {cmd.label}
                </span>
                {cmd.description && (
                  <span style="font-size:0.8rem;color:#777">
                    {cmd.description}
                  </span>
                )}
              </li>
            ))}
        </ul>
      </div>
    </div>
  );
}
