// frontend/tests/canonicalSurfaceTechnicalDisclosureStructure.test.ts
//
// Structural (not literal-string) source proof that specific dynamic runtime values are
// STRUCTURALLY contained inside a <details>技術情報 disclosure element, never rendered as
// always-visible primary text. Unlike a NORMAL_VIEW_BANNED_TERMS-style literal-string scan (which
// can only catch a STATIC banned word and is blind to a dynamic {expression} whose real runtime
// value is unknown at scan time), a structural check on "is this {expression} lexically nested
// inside a <details> tag in the source" is sound for ANY runtime value that expression evaluates
// to, because Preact/JSX renders exactly the DOM structure the source describes — an expression
// written inside a <details>...</details> block can never render outside it.
//
// This complements layoutPatchApplyModalLabelBoundary.test.tsx (full DOM-mount proof for the
// highest-risk rewrite) for the remaining, simpler mechanical <details> wraps in this round.

import { assert, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";

async function read(relPath: string): Promise<string> {
  return await Deno.readTextFile(new URL(`../${relPath}`, import.meta.url));
}

/** True when `needle` appears strictly between a <details ...> open tag and its matching </details>. */
function isInsideDetails(source: string, needle: string): boolean {
  const detailsBlocks: string[] = [];
  const re = /<details[\s\S]*?<\/details>/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(source)) !== null) {
    detailsBlocks.push(m[0]);
  }
  return detailsBlocks.some((block) => block.includes(needle));
}

/** True when `needle` appears in the source OUTSIDE of any <details>...</details> block. */
function appearsOutsideDetails(source: string, needle: string): boolean {
  let stripped = source;
  stripped = stripped.replace(/<details[\s\S]*?<\/details>/g, "");
  return stripped.includes(needle);
}

Deno.test("LoginManifestPanel: AUTH_USER_NOT_APPROVED error code is structurally inside a 技術情報 <details>, not the primary registration description", async () => {
  const src = await read("islands/LoginManifestPanel.tsx");
  assert(isInsideDetails(src, "AUTH_USER_NOT_APPROVED"), "the raw error code must be nested inside a <details> block");
  assertFalse(
    appearsOutsideDetails(src, "AUTH_USER_NOT_APPROVED"),
    "the raw error code must not also appear outside any <details> block",
  );
});

Deno.test("LoginManifestPanel: registration success message no longer prints raw realm/approve/status inline with the primary text", async () => {
  const src = await read("islands/LoginManifestPanel.tsx");
  assert(
    isInsideDetails(src, "realm=user"),
    "raw realm/approve/status must be nested inside a <details> block",
  );
});

Deno.test("routes/auth.tsx: 'DB seed manifest' / 'DB 正本' technical wording is structurally inside a 技術情報 <details>", async () => {
  const src = await read("routes/auth.tsx");
  assert(isInsideDetails(src, "DB seed manifest"), "must be nested inside <details>");
  assert(isInsideDetails(src, "DB 正本"), "must be nested inside <details>");
  assertFalse(appearsOutsideDetails(src, "DB seed manifest"), "must not also appear outside <details>");
});

Deno.test("ContentsScreenDesignPanel: clone source evidence's raw topologySystemName/status/dispatcher fields are structurally inside a 技術情報 <details>", async () => {
  const src = await read("islands/ContentsScreenDesignPanel.tsx");
  assert(
    isInsideDetails(src, "cloneSourceEvidence.topologySystemName"),
    "the raw topologySystemName expression must be nested inside <details>",
  );
  assert(
    isInsideDetails(src, "cloneSourceEvidence.dispatcherAxes.role"),
    "the raw dispatcherAxes expression must be nested inside <details>",
  );
  assertFalse(
    appearsOutsideDetails(src, "cloneSourceEvidence.dispatcherAxes"),
    "dispatcherAxes must not also be rendered outside <details>",
  );
  // status must resolve through the shared UX_STATUS_LABELS friendly mapping, never the raw enum
  // value directly, even inside the disclosure.
  assert(
    src.includes("UX_STATUS_LABELS[cloneSourceEvidence.status] ?? cloneSourceEvidence.status"),
    "status must be resolved through UX_STATUS_LABELS before display",
  );
});

Deno.test("UiBuilderAdmin: the post-apply handoff banner's raw routeKey line is structurally inside a 技術情報 <details>", async () => {
  const src = await read("islands/UiBuilderAdmin.tsx");
  assert(
    isInsideDetails(src, "route: <code"),
    "the raw routeKey line must be nested inside a <details> block",
  );
});

Deno.test("UiBuilderAdmin / LayoutPatchApplyModal / adminUxTerms: no remaining raw 'layout_patch' / 'DB ' jargon in always-visible primary strings", async () => {
  const uiBuilderSrc = await read("islands/UiBuilderAdmin.tsx");
  const termsSrc = await read("content/adminUxTerms.ts");
  // Both known primary-text sites this round fixed: the lifecycle phase step banner
  // ("配置（layout_patch）は DB に保存済みです") and the shared handoff hint constant.
  assertFalse(
    appearsOutsideDetails(uiBuilderSrc, "配置（layout_patch）は DB"),
    "the lifecycle phase banner must no longer show raw layout_patch/DB jargon as primary text",
  );
  assertFalse(
    termsSrc.includes("配置（layout_patch）は DB に反映済みです"),
    "UX_LAYOUT_APPLIED_HANDOFF_HINT must no longer contain raw layout_patch/DB jargon",
  );
});
