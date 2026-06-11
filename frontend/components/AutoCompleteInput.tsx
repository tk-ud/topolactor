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
// SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml (autocomplete_input.primitive)
export type AutoCompleteInputProps = ComponentIdentityProps & {
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

export function AutoCompleteInput({
  value,
  onChange,
  onSelect,
  onSearch,
  suggestions = [],
  placeholder,
  disabled = false,
  label,
  className,
  design,
}: AutoCompleteInputProps): JSX.Element {
  const listId = "autocomplete-suggestions";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && <label style="font-size:0.85rem;color:#555">{label}</label>}
      <input
        type="text"
        value={value}
        list={suggestions.length > 0 ? listId : undefined}
        onInput={(e) => {
          const v = (e.target as HTMLInputElement).value;
          onChange(v);
          onSearch?.(v);
        }}
        onChange={onSelect
          ? (e) => onSelect((e.target as HTMLInputElement).value)
          : undefined}
        placeholder={placeholder}
        disabled={disabled || design?.state === "loading"}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
      {suggestions.length > 0 && (
        <datalist id={listId}>
          {suggestions.map((s) => <option key={s} value={s} />)}
        </datalist>
      )}
    </div>
  );
}
