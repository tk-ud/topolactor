// markdownRenderer.test.ts
//
// Real-DOM proof (not source-string checks) for frontend/lib/markdownRenderer.ts, the shared safe
// Markdown-to-Preact-VNode rendering core reused by MdViewer.tsx (RenderedMarkdownPanel) and the
// md_viewer.projection runtime factory's bare-markdown mode.
//
// Two separately-reported proof axes per the task contract:
//   1. Positive: allowed Markdown vocabulary (heading/list/link/code/bold/italic) actually renders
//      as the corresponding real DOM elements — this is a rendering-fidelity proof, not a security
//      proof, and must not be conflated with #2.
//   2. Negative: executable script / raw HTML / disallowed-scheme links never reach the DOM as
//      executable or navigable content — a security proof, verified independently of #1.

import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { renderMarkdownToVNodes } from "../lib/markdownRenderer.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

async function renderMarkdown(source: string): Promise<{ container: Element; cleanup: () => void }> {
  const { container, cleanup } = setupDom();
  render(h("div", {}, renderMarkdownToVNodes(source)), container);
  await flushUpdates();
  return { container, cleanup };
}

// ─── 1. Positive: allowed vocabulary renders as real DOM elements ─────────────

Deno.test("markdownRenderer positive: ATX headings render as real h1..h6 elements", async () => {
  const { container, cleanup } = await renderMarkdown("# Title one\n\n## Title two\n\n###### Title six");
  try {
    assertEquals(container.querySelector("h1")?.textContent, "Title one");
    assertEquals(container.querySelector("h2")?.textContent, "Title two");
    assertEquals(container.querySelector("h6")?.textContent, "Title six");
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer positive: unordered and ordered lists render as real ul/ol/li elements", async () => {
  const { container, cleanup } = await renderMarkdown(
    "- first\n- second\n- third\n\n1. alpha\n2. beta",
  );
  try {
    const ul = container.querySelector("ul");
    assert(ul, "ul must exist");
    assertEquals(Array.from(ul!.querySelectorAll("li")).map((li) => li.textContent), [
      "first",
      "second",
      "third",
    ]);
    const ol = container.querySelector("ol");
    assert(ol, "ol must exist");
    assertEquals(Array.from(ol!.querySelectorAll("li")).map((li) => li.textContent), [
      "alpha",
      "beta",
    ]);
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer positive: allow-listed link renders as a real navigable <a href>", async () => {
  const { container, cleanup } = await renderMarkdown("[docs](https://example.com/docs)");
  try {
    const link = container.querySelector("a");
    assert(link, "anchor element must exist for an allow-listed https: link");
    assertEquals(link!.getAttribute("href"), "https://example.com/docs");
    assertEquals(link!.textContent, "docs");
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer positive: inline code and fenced code blocks render as real code/pre elements", async () => {
  const { container, cleanup } = await renderMarkdown(
    "Use `inline()` here.\n\n```\nfenced line one\nfenced line two\n```",
  );
  try {
    const inlineCode = container.querySelector("p code");
    assertEquals(inlineCode?.textContent, "inline()");
    const pre = container.querySelector("pre");
    assert(pre, "pre must exist for a fenced code block");
    assertEquals(pre!.querySelector("code")?.textContent, "fenced line one\nfenced line two");
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer positive: bold and italic render as real strong/em elements", async () => {
  const { container, cleanup } = await renderMarkdown("This is **bold** and this is *italic*.");
  try {
    assertEquals(container.querySelector("strong")?.textContent, "bold");
    assertEquals(container.querySelector("em")?.textContent, "italic");
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer positive: blockquote renders as a real blockquote element", async () => {
  const { container, cleanup } = await renderMarkdown("> quoted line one\n> quoted line two");
  try {
    const bq = container.querySelector("blockquote");
    assert(bq, "blockquote must exist");
    assertEquals(bq!.textContent?.trim(), "quoted line one\nquoted line two".replace("\n", ""));
  } finally {
    cleanup();
  }
});

// ─── 2. Negative: executable/raw-HTML content never reaches the DOM as executable ─────────────

Deno.test("markdownRenderer security: a <script> tag in source never becomes a real <script> element", async () => {
  const { container, cleanup } = await renderMarkdown(
    "before <script>window.__pwned = true;</script> after",
  );
  try {
    assertEquals(
      container.querySelectorAll("script").length,
      0,
      "no <script> element may exist in the rendered DOM regardless of source content",
    );
    // The literal characters render as inert text content, not interpreted markup.
    assert(
      container.textContent?.includes("<script>"),
      "the script tag text must appear as literal escaped text, not be silently dropped",
    );
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer security: an <img onerror=...> tag never becomes a real <img> element with an event handler", async () => {
  const { container, cleanup } = await renderMarkdown(
    '<img src=x onerror="window.__pwned=true">',
  );
  try {
    assertEquals(
      container.querySelectorAll("img").length,
      0,
      "no <img> element may be created from raw HTML in source",
    );
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer security: javascript: scheme link never becomes a navigable <a href>", async () => {
  const { container, cleanup } = await renderMarkdown("[click me](javascript:alert(1))");
  try {
    assertEquals(
      container.querySelectorAll("a").length,
      0,
      "a javascript: URL must never be emitted as an <a href>",
    );
    assert(
      container.textContent?.includes("click me"),
      "the link text still renders, as inert plain text",
    );
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer security: data: scheme link never becomes a navigable <a href>", async () => {
  const { container, cleanup } = await renderMarkdown(
    "[open](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)",
  );
  try {
    assertEquals(container.querySelectorAll("a").length, 0, "a data: URL must never become an <a href>");
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer security: vbscript: and unrecognized-scheme links never become navigable <a href>", async () => {
  const { container: c1, cleanup: cleanup1 } = await renderMarkdown("[x](vbscript:msgbox(1))");
  try {
    assertEquals(c1.querySelectorAll("a").length, 0);
  } finally {
    cleanup1();
  }
  const { container: c2, cleanup: cleanup2 } = await renderMarkdown("[x](file:///etc/passwd)");
  try {
    assertEquals(c2.querySelectorAll("a").length, 0);
  } finally {
    cleanup2();
  }
});

Deno.test("markdownRenderer security: mailto: and http:/https: links remain the only allow-listed navigable schemes", async () => {
  const { container, cleanup } = await renderMarkdown(
    "[a](http://example.com) [b](https://example.com) [c](mailto:test@example.com)",
  );
  try {
    const hrefs = Array.from(container.querySelectorAll("a")).map((a) => a.getAttribute("href"));
    assertEquals(hrefs, ["http://example.com", "https://example.com", "mailto:test@example.com"]);
  } finally {
    cleanup();
  }
});

Deno.test("markdownRenderer security: raw HTML anywhere in source is never parsed as markup, even absent a recognized Markdown construct", async () => {
  const { container, cleanup } = await renderMarkdown(
    '<div onclick="doEvil()"><a href="javascript:evil()">click</a></div>',
  );
  try {
    assertEquals(container.querySelectorAll("div[onclick]").length, 0);
    // The only <a> the renderer itself could have produced here is from Markdown link syntax --
    // there is none in this source, and the raw HTML anchor is never parsed as one.
    assertEquals(container.querySelectorAll("a").length, 0);
  } finally {
    cleanup();
  }
});
