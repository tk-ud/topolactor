import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type AutoCompleteInputProps = ComponentIdentityProps & {
  value: string;
  onChange: (value: string) => void;
  onSelect?: (value: string) => void;
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
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
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
