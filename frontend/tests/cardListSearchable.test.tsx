/**
 * cardListSearchable.test.tsx
 *
 * CardList's built-in client-side search filter (searchable prop) — added for
 * admin-surface-topology-seed-conversion's admin-dashboard hub_relation_link_list, which needs
 * search: [relation_label_or_manifest_metadata_when_available] without any new
 * propBindingResolver/backend filter plumbing (there is none for filtering a bound list prop by
 * a live local input value). The filter is local Preact state inside CardList itself.
 */
import { assert, assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { CardList, type CardListItem } from "../components/CardList.tsx";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const ITEMS: CardListItem[] = [
  { id: 1, title: "Credential management", body: "auth.external" },
  { id: 2, title: "Enum roster", body: "enum dictionary" },
  { id: 3, title: "Scheduler settings", body: "scheduler jobs" },
];

function dispatchInputValue(element: Element, value: string) {
  (element as HTMLInputElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

Deno.test("CardList: searchable=false renders no search box (existing behavior unchanged)", () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(CardList, { items: ITEMS }), container);
    assertEquals(container.querySelector("[data-card-list-search]"), null);
  } finally {
    cleanup();
  }
});

Deno.test("CardList: searchable=true renders a search box and shows all items with empty query", () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(CardList, { items: ITEMS, searchable: true }), container);
    const search = container.querySelector("[data-card-list-search]");
    assert(search, "search box should be rendered when searchable=true");
    assertEquals(container.querySelectorAll("section").length, 3);
  } finally {
    cleanup();
  }
});

Deno.test("CardList: typing filters items by title substring (case-insensitive)", async () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(CardList, { items: ITEMS, searchable: true }), container);
    const search = container.querySelector("[data-card-list-search]")!;
    dispatchInputValue(search, "enum");
    await flushUpdates();
    await flushUpdates();
    assertEquals(container.querySelectorAll("section").length, 1);
    assert(
      container.textContent?.includes("Enum roster"),
      "filtered result should include the matching item",
    );
    assert(
      !container.textContent?.includes("Scheduler settings"),
      "non-matching items must not remain visible",
    );
  } finally {
    cleanup();
  }
});

Deno.test("CardList: typing filters items by body substring too", async () => {
  const { container, cleanup } = setupDom();
  try {
    render(h(CardList, { items: ITEMS, searchable: true }), container);
    const search = container.querySelector("[data-card-list-search]")!;
    dispatchInputValue(search, "scheduler jobs");
    await flushUpdates();
    await flushUpdates();
    assertEquals(container.querySelectorAll("section").length, 1);
    assert(container.textContent?.includes("Scheduler settings"));
  } finally {
    cleanup();
  }
});

Deno.test("CardList: no matches shows emptyMessage, not a silently empty list", async () => {
  const { container, cleanup } = setupDom();
  try {
    render(
      h(CardList, { items: ITEMS, searchable: true, emptyMessage: "該当なし" }),
      container,
    );
    const search = container.querySelector("[data-card-list-search]")!;
    dispatchInputValue(search, "nonexistent-query-xyz");
    await flushUpdates();
    await flushUpdates();
    assertEquals(container.querySelectorAll("section").length, 0);
    assert(container.textContent?.includes("該当なし"));
  } finally {
    cleanup();
  }
});

Deno.test("CardList: onSelect after filtering fires with the correct (filtered) item, not a stale full-list index", async () => {
  const { container, cleanup } = setupDom();
  const selectedRef: { current: CardListItem | null } = { current: null };
  try {
    render(
      h(CardList, {
        items: ITEMS,
        searchable: true,
        onSelect: (item) => {
          selectedRef.current = item;
        },
      }),
      container,
    );
    const search = container.querySelector("[data-card-list-search]")!;
    // Filters down to a single item ("Scheduler settings", originally index 2 in the
    // unfiltered array) so its *visible* index (0) differs from its original index (2) — this
    // is exactly the mismatch the cardListFactory index-based re-lookup bug would have hit.
    dispatchInputValue(search, "scheduler");
    await flushUpdates();
    await flushUpdates();
    const card = container.querySelector("section") as HTMLElement;
    card.click();
    await flushUpdates();
    assertEquals(selectedRef.current?.title, "Scheduler settings");
  } finally {
    cleanup();
  }
});
