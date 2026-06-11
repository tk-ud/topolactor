import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

// candidate_source_boundary: debounce_backend_readonly_search
// Typing triggers onChange and onSearch. Caller is responsible for debouncing onSearch before
// calling a backend read-only search provider. No mutation / DB write / apply during typing.
// suggestions prop is updated by parent after backend search completes.
// Differentiated from autocomplete by multi-row product / item input assist use cases.
// SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml (suggest_input.primitive)
export type SuggestInputProps = ComponentIdentityProps & {
  value: string;
  onChange: (value: string) => void;
  onSelect?: (value: string) => void;
  /** Read-only backend search hook. Caller must debounce. No mutation/write during typing. */
  onSearch?: (query: string) => void;
  suggestions?: string[];
  placeholder?: string;
  disabled?: boolean;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function SuggestInput({
  value,
  onChange,
  onSelect,
  onSearch,
  suggestions,
  placeholder,
  disabled,
  label,
  className,
  design,
}: SuggestInputProps): JSX.Element {
  const isDisabled = disabled || design?.state === "loading";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  const listId = "suggest-input-list";
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && (
        <label style="font-size:0.85rem;color:#555">{label}</label>
      )}
      <input
        type="text"
        value={value}
        list={suggestions && suggestions.length > 0 ? listId : undefined}
        placeholder={placeholder}
        disabled={isDisabled}
        onInput={(e) => {
          const v = (e.target as HTMLInputElement).value;
          onChange(v);
          onSearch?.(v);
        }}
        onChange={(e) => {
          const v = (e.target as HTMLInputElement).value;
          if (onSelect && suggestions && suggestions.includes(v)) {
            onSelect(v);
          }
        }}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
      {suggestions && suggestions.length > 0 && (
        <datalist id={listId}>
          {suggestions.map((s) => (
            <option key={s} value={s} />
          ))}
        </datalist>
      )}
    </div>
  );
}
