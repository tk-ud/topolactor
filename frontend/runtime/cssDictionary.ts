// Generated-from-contract artifact source: docs/design/css-dictionary-ssot.yaml
// This is a projection artifact for UI selector binding, not a secondary SSOT.

export type CssDictionaryToken = {
  tokenKey: string;
  category: string;
  property: string;
  componentScope: string[];
  semanticRole: string;
};

// Resolved CSS values projected from value_scales in docs/design/css-dictionary-ssot.yaml.
// Keys match SSOT value_refs (category.scale_key) or "direct" for tokens with literal values.
const CSS_TOKEN_RESOLVED_VALUES: Record<string, string> = {
  "color.action.primary.background": "#0070f3",
  "color.action.primary.text": "#fff",
  "color.action.secondary.background": "#eee",
  "color.action.secondary.text": "#333",
  "color.action.danger.background": "#e00",
  "color.action.danger.text": "#fff",
  "border.control.default": "1px solid #ccc",
  "radius.control.sm": "4px",
  "spacing.control.padding_md": "6px 14px",
  "spacing.field.padding_sm": "4px 8px",
  "typography.control.monospace": "monospace",
  "interaction.control.pointer": "pointer",
  "interaction.control.disabled_opacity": "0.6",
  "layout.stack.flex_column": "column",
  "layout.table.full_width": "100%",
};

export function resolveCssTokenValue(tokenKey: string): string | undefined {
  return CSS_TOKEN_RESOLVED_VALUES[tokenKey];
}

export const CSS_DICTIONARY_TOKENS: CssDictionaryToken[] = [
  { tokenKey: "color.action.primary.background", category: "color", property: "background", componentScope: ["Button"], semanticRole: "primary_action" },
  { tokenKey: "color.action.primary.text", category: "color", property: "color", componentScope: ["Button"], semanticRole: "primary_action" },
  { tokenKey: "color.action.secondary.background", category: "color", property: "background", componentScope: ["Button"], semanticRole: "secondary_action" },
  { tokenKey: "color.action.secondary.text", category: "color", property: "color", componentScope: ["Button"], semanticRole: "secondary_action" },
  { tokenKey: "color.action.danger.background", category: "color", property: "background", componentScope: ["Button"], semanticRole: "danger_action" },
  { tokenKey: "color.action.danger.text", category: "color", property: "color", componentScope: ["Button"], semanticRole: "danger_action" },
  { tokenKey: "border.control.default", category: "border", property: "border", componentScope: ["Button", "Input"], semanticRole: "control_boundary" },
  { tokenKey: "radius.control.sm", category: "radius", property: "border-radius", componentScope: ["Button", "Input", "Card"], semanticRole: "control_shape" },
  { tokenKey: "spacing.control.padding_md", category: "spacing", property: "padding", componentScope: ["Button"], semanticRole: "control_spacing" },
  { tokenKey: "spacing.field.padding_sm", category: "spacing", property: "padding", componentScope: ["Input"], semanticRole: "field_spacing" },
  { tokenKey: "typography.control.monospace", category: "typography", property: "font-family", componentScope: ["Button", "Input", "Table", "Card"], semanticRole: "admin_ui_text" },
  { tokenKey: "interaction.control.pointer", category: "interaction", property: "cursor", componentScope: ["Button"], semanticRole: "clickable_control" },
  { tokenKey: "interaction.control.disabled_opacity", category: "interaction", property: "opacity", componentScope: ["Button", "Input"], semanticRole: "disabled_state" },
  { tokenKey: "layout.stack.flex_column", category: "layout", property: "flex-direction", componentScope: ["Card"], semanticRole: "stacked_layout" },
  { tokenKey: "layout.table.full_width", category: "layout", property: "width", componentScope: ["Table"], semanticRole: "data_table_width" },
];
