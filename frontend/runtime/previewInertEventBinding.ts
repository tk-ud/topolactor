/**
 * Inert event bindings for read-only layout projection (UI Builder canvas, /demo draft preview).
 * SSOT: preview surfaces do not require wiring-backed runtime dispatch.
 */

export function buildPreviewInertEventBinding(): Record<string, unknown> {
  const inert = (eventType: string) => ({ eventType, payload: {} });
  return {
    click: inert("click"),
    change: inert("change"),
    select: inert("select"),
    submit: inert("submit"),
    focus: inert("focus"),
    blur: inert("blur"),
    toggle: inert("toggle"),
  };
}
