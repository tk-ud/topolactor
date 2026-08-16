/**
 * markdownRenderer.ts — shared safe Markdown-to-Preact-VNode renderer.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml §markdown_template_contract.validation_rules
 *       (raw_html_is_disabled_by_default, markdown_template_must_not_contain_executable_script)
 *
 * Reused by any surface that needs to display Markdown safely (MdViewer.tsx / md_viewer.projection
 * runtime factory / physical_details_inline_editor_md_generator preset preview) — this is the single
 * canonical rendering core, not a per-surface reimplementation.
 *
 * Security model (fail-close, not a denylist of known-bad tags):
 *   - This function NEVER builds an HTML string and NEVER uses dangerouslySetInnerHTML. It parses a
 *     restricted Markdown subset directly into real Preact VNodes (h(...) calls with text children).
 *     Any character sequence that looks like an HTML/script tag in the source (e.g. "<script>",
 *     "<img onerror=...>") is therefore never interpreted as markup — Preact renders text children as
 *     literal text nodes, so it can only ever appear as escaped, inert text on the page. There is no
 *     raw-HTML passthrough mode to disable; none exists.
 *   - Link targets ([text](url)) are allow-listed by URL scheme (http, https, mailto). Any other
 *     scheme (javascript:, data:, vbscript:, or anything unrecognized) — and any relative/scheme-less
 *     URL, which this renderer does not attempt to resolve — renders the link as plain, non-hyperlinked
 *     text instead of emitting an <a href>. This is a fail-close allow-list, not a denylist of specific
 *     known-bad schemes.
 *
 * Allowed rule vocabulary (matches the "allowed" side of the SSOT contract above): ATX headings
 * (# .. ######), paragraphs, unordered/ordered lists, blockquotes, fenced code blocks, inline code,
 * bold, italic, links (allow-listed schemes only), hard line breaks. Anything else (raw HTML tags,
 * scripts, non-allow-listed link schemes, images) is rendered as literal text or dropped to plain text
 * — never executed, never passed through as markup.
 */

import { Fragment, h } from "preact";
import type { VNode } from "preact";
type AnyVNode = VNode<any>;

const ALLOWED_LINK_SCHEMES = ["http:", "https:", "mailto:"];

function isAllowedLinkUrl(url: string): boolean {
  const trimmed = url.trim();
  // Scheme-relative ("//host/...") and relative URLs are not resolved by this renderer — treated as
  // not-allow-listed (fail-close) rather than guessed at, since a resolved-safe base URL is not part
  // of this renderer's input.
  const schemeMatch = /^([a-zA-Z][a-zA-Z0-9+.-]*:)/.exec(trimmed);
  if (!schemeMatch) return false;
  return ALLOWED_LINK_SCHEMES.includes(schemeMatch[1].toLowerCase());
}

/**
 * Parses inline Markdown constructs (bold, italic, inline code, links) within a single line of text
 * into an array of Preact children. Never emits raw HTML; unresolved/disallowed constructs fall back
 * to literal text.
 */
function renderInline(text: string, keyPrefix: string): (AnyVNode | string)[] {
  const out: (AnyVNode | string)[] = [];
  // Single combined pass: inline code, links, bold, italic — in that priority order per match position.
  const INLINE_RE =
    /`([^`]+)`|\[([^\]]*)\]\(([^)\s]+)\)|\*\*([^*]+)\*\*|\*([^*]+)\*/g;
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  let idx = 0;
  while ((match = INLINE_RE.exec(text)) !== null) {
    if (match.index > lastIndex) {
      out.push(text.slice(lastIndex, match.index));
    }
    const key = `${keyPrefix}-inline-${idx++}`;
    if (match[1] !== undefined) {
      out.push(h("code", { key }, match[1]));
    } else if (match[2] !== undefined) {
      const linkText = match[2];
      const url = match[3];
      if (isAllowedLinkUrl(url)) {
        out.push(
          h("a", { key, href: url, rel: "noopener noreferrer" }, linkText),
        );
      } else {
        // Disallowed/unrecognized scheme: render as plain text, never as a navigable link.
        out.push(`${linkText} (${url})`);
      }
    } else if (match[4] !== undefined) {
      out.push(h("strong", { key }, match[4]));
    } else if (match[5] !== undefined) {
      out.push(h("em", { key }, match[5]));
    }
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < text.length) {
    out.push(text.slice(lastIndex));
  }
  return out;
}

type Block =
  | { kind: "heading"; level: number; text: string }
  | { kind: "code"; text: string }
  | { kind: "blockquote"; lines: string[] }
  | { kind: "ul"; items: string[] }
  | { kind: "ol"; items: string[] }
  | { kind: "paragraph"; lines: string[] };

function parseBlocks(source: string): Block[] {
  const lines = source.split(/\r\n|\r|\n/);
  const blocks: Block[] = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];

    if (line.trim() === "") {
      i++;
      continue;
    }

    // Fenced code block: ``` ... ``` (language info string, if any, is discarded — never used to
    // select a highlighter or otherwise influence rendering).
    const fenceMatch = /^```/.exec(line);
    if (fenceMatch) {
      const codeLines: string[] = [];
      i++;
      while (i < lines.length && !/^```/.test(lines[i])) {
        codeLines.push(lines[i]);
        i++;
      }
      i++; // skip closing fence (or end of input if unterminated)
      blocks.push({ kind: "code", text: codeLines.join("\n") });
      continue;
    }

    // ATX heading: 1-6 '#' followed by a space.
    const headingMatch = /^(#{1,6})\s+(.*)$/.exec(line);
    if (headingMatch) {
      blocks.push({
        kind: "heading",
        level: headingMatch[1].length,
        text: headingMatch[2].trim(),
      });
      i++;
      continue;
    }

    // Blockquote: consecutive lines starting with '>'.
    if (/^>\s?/.test(line)) {
      const quoteLines: string[] = [];
      while (i < lines.length && /^>\s?/.test(lines[i])) {
        quoteLines.push(lines[i].replace(/^>\s?/, ""));
        i++;
      }
      blocks.push({ kind: "blockquote", lines: quoteLines });
      continue;
    }

    // Unordered list: consecutive lines starting with '-', '*', or '+' followed by a space.
    if (/^[-*+]\s+/.test(line)) {
      const items: string[] = [];
      while (i < lines.length && /^[-*+]\s+/.test(lines[i])) {
        items.push(lines[i].replace(/^[-*+]\s+/, ""));
        i++;
      }
      blocks.push({ kind: "ul", items });
      continue;
    }

    // Ordered list: consecutive lines starting with '<number>.' followed by a space.
    if (/^\d+\.\s+/.test(line)) {
      const items: string[] = [];
      while (i < lines.length && /^\d+\.\s+/.test(lines[i])) {
        items.push(lines[i].replace(/^\d+\.\s+/, ""));
        i++;
      }
      blocks.push({ kind: "ol", items });
      continue;
    }

    // Paragraph: consecutive non-blank, non-block-starting lines.
    const paraLines: string[] = [];
    while (
      i < lines.length &&
      lines[i].trim() !== "" &&
      !/^```/.test(lines[i]) &&
      !/^(#{1,6})\s+/.test(lines[i]) &&
      !/^>\s?/.test(lines[i]) &&
      !/^[-*+]\s+/.test(lines[i]) &&
      !/^\d+\.\s+/.test(lines[i])
    ) {
      paraLines.push(lines[i]);
      i++;
    }
    blocks.push({ kind: "paragraph", lines: paraLines });
  }
  return blocks;
}

/**
 * Renders a restricted Markdown subset to real Preact VNodes (never an HTML string, never
 * dangerouslySetInnerHTML). See module doc comment for the security model and allowed vocabulary.
 */
export function renderMarkdownToVNodes(source: string): AnyVNode {
  const blocks = parseBlocks(source ?? "");
  const children: AnyVNode[] = blocks.map((block, blockIndex) => {
    const key = `md-block-${blockIndex}`;
    switch (block.kind) {
      case "heading":
        return h(
          `h${block.level}`,
          { key },
          renderInline(block.text, key),
        );
      case "code":
        return h("pre", { key }, h("code", {}, block.text));
      case "blockquote":
        return h(
          "blockquote",
          { key },
          block.lines.map((line, lineIndex) =>
            h(
              "p",
              { key: `${key}-line-${lineIndex}` },
              renderInline(line, `${key}-line-${lineIndex}`),
            )
          ),
        );
      case "ul":
        return h(
          "ul",
          { key },
          block.items.map((item, itemIndex) =>
            h(
              "li",
              { key: `${key}-item-${itemIndex}` },
              renderInline(item, `${key}-item-${itemIndex}`),
            )
          ),
        );
      case "ol":
        return h(
          "ol",
          { key },
          block.items.map((item, itemIndex) =>
            h(
              "li",
              { key: `${key}-item-${itemIndex}` },
              renderInline(item, `${key}-item-${itemIndex}`),
            )
          ),
        );
      case "paragraph": {
        const paraChildren: (AnyVNode | string)[] = [];
        block.lines.forEach((line, lineIndex) => {
          if (lineIndex > 0) paraChildren.push(h("br", { key: `${key}-br-${lineIndex}` }));
          paraChildren.push(...renderInline(line, `${key}-line-${lineIndex}`));
        });
        return h("p", { key }, paraChildren);
      }
    }
  });
  return h(Fragment, {}, children);
}
