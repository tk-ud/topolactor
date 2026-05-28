import { JSX } from "preact";
import type { ComponentDesignParams } from "./Button.tsx";
import { mergeDesignClassName, mergeDesignStyle } from "./types.ts";

export type DocumentCanvasTemplateEditorField = {
  key: string;
  label?: string;
  x?: number;
  y?: number;
  value?: string;
};

export type DocumentCanvasTemplateEditorProps = {
  backgroundImageUrl?: string;
  fields?: DocumentCanvasTemplateEditorField[];
  className?: string;
  design?: ComponentDesignParams;
};

// Projection scaffold only. Background image is an alignment reference surface.
// Field binding is preview display only — no mutation, no PDF generation.
// Out of scope: production report layout engine, advanced pagination, printer calibration.
export function DocumentCanvasTemplateEditor({
  backgroundImageUrl,
  fields = [],
  className,
  design,
}: DocumentCanvasTemplateEditorProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const baseStyle =
    "position:relative;width:100%;min-height:400px;border:1px solid #ccc;background:#fafafa;overflow:hidden;font-family:monospace;";
  const containerStyle = mergeDesignStyle(baseStyle, design);

  return (
    <div
      className={mergedClassName}
      style={containerStyle}
      data-component="document_canvas_template_editor"
      data-lifecycle="draft_candidate"
    >
      {backgroundImageUrl && (
        <img
          src={backgroundImageUrl}
          alt="background reference"
          style="position:absolute;top:0;left:0;width:100%;height:100%;object-fit:contain;opacity:0.4;pointer-events:none;"
          aria-hidden="true"
        />
      )}
      {fields.map((field) => (
        <div
          key={field.key}
          data-field-key={field.key}
          style={`position:absolute;left:${field.x ?? 0}px;top:${field.y ?? 0}px;background:rgba(255,255,255,0.85);border:1px dashed #0070f3;padding:2px 6px;font-size:12px;cursor:default;`}
          title={field.label ?? field.key}
        >
          <span style="color:#888;font-size:10px;">
            {field.label ?? field.key}
          </span>
          {field.value !== undefined && (
            <span style="margin-left:4px;color:#333;">{field.value}</span>
          )}
        </div>
      ))}
      {fields.length === 0 && !backgroundImageUrl && (
        <div
          style="display:flex;align-items:center;justify-content:center;height:400px;color:#aaa;font-size:13px;"
        >
          [Document Canvas — projection scaffold only]
        </div>
      )}
    </div>
  );
}
