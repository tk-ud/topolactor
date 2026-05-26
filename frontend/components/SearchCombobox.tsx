import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type SearchComboboxOption = { label: string; value: string };

export type SearchComboboxProps = ComponentIdentityProps & {
  value: string;
  onChange: (value: string) => void;
  onSelect?: (value: string) => void;
  options?: SearchComboboxOption[];
  placeholder?: string;
  disabled?: boolean;
  label?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function SearchCombobox({
  value,
  onChange,
  onSelect,
  options = [],
  placeholder,
  disabled = false,
  label,
  className,
  design,
}: SearchComboboxProps): JSX.Element {
  const listId = "search-combobox-options";
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "display:flex;flex-direction:column;gap:4px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {label && <label style="font-size:0.85rem;color:#555">{label}</label>}
      <input
        role="combobox"
        aria-autocomplete="list"
        type="text"
        value={value}
        list={options.length > 0 ? listId : undefined}
        onInput={(e) => onChange((e.target as HTMLInputElement).value)}
        onChange={onSelect
          ? (e) => onSelect((e.target as HTMLInputElement).value)
          : undefined}
        placeholder={placeholder}
        disabled={disabled || design?.state === "loading"}
        style="padding:6px 8px;border:1px solid #ccc;border-radius:4px;font-family:monospace;font-size:0.9rem"
      />
      {options.length > 0 && (
        <datalist id={listId}>
          {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </datalist>
      )}
    </div>
  );
}
