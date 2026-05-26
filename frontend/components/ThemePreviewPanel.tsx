import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type ThemeTokenPreview = {
  key: string;
  value: string;
  description?: string;
};

export type ThemePreviewPanelProps = ComponentIdentityProps & {
  tokens?: ThemeTokenPreview[];
  title?: string;
  className?: string;
  design?: ComponentDesignParams;
};

export function ThemePreviewPanel({
  tokens = [],
  title,
  className,
  design,
}: ThemePreviewPanelProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle(
    "border:1px solid #ddd;border-radius:6px;padding:12px;font-family:monospace",
    design,
  );
  return (
    <div className={mergedClassName} style={mergedStyle}>
      {title && (
        <div style="font-weight:600;margin-bottom:10px;color:#333;font-size:0.95rem">
          {title}
        </div>
      )}
      {tokens.length === 0
        ? <div style="color:#aaa;font-size:0.85rem">No tokens.</div>
        : (
          <div style="display:flex;flex-direction:column;gap:6px">
            {tokens.map((t) => (
              <div
                key={t.key}
                style="display:flex;align-items:center;gap:8px;font-size:0.85rem"
              >
                <div
                  style={`width:24px;height:24px;border-radius:4px;border:1px solid #eee;background:${t.value};flex-shrink:0`}
                />
                <span style="color:#555;font-family:monospace">{t.key}</span>
                <span style="color:#888">{t.value}</span>
                {t.description && (
                  <span style="color:#aaa;font-size:0.78rem">{t.description}</span>
                )}
              </div>
            ))}
          </div>
        )}
    </div>
  );
}
