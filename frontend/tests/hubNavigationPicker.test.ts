import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { ContentBundleListItem } from "../api/adminApi.ts";
import {
  hubDestinationOptionLabel,
  hubDestinationPickerOptions,
} from "../lib/hubNavigationPicker.ts";

function hub(
  partial: Partial<ContentBundleListItem> & Pick<ContentBundleListItem, "id">,
): ContentBundleListItem {
  return {
    kind: "hub",
    label: "",
    state: "active",
    summary: "",
    ...partial,
  };
}

Deno.test("hubDestinationPickerOptions: keeps hubs with summary or label only", () => {
  const options = hubDestinationPickerOptions([
    hub({ id: "a", summary: "顧客一覧", label: "customers" }),
    hub({ id: "b", summary: "", label: "" }),
    hub({ id: "c", summary: "", label: "注文" }),
  ]);
  assertEquals(options.map((h) => h.id), ["a", "c"]);
});

Deno.test("hubDestinationOptionLabel: summary-first then label", () => {
  assertEquals(
    hubDestinationOptionLabel(hub({ id: "a", summary: "顧客一覧", label: "customers" })),
    "顧客一覧",
  );
  assertEquals(
    hubDestinationOptionLabel(hub({ id: "b", summary: "", label: "注文" })),
    "注文",
  );
});

Deno.test("listContentHubs: rejects non-array emission.data (explicit API boundary)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = () =>
    Promise.resolve(new Response(JSON.stringify({
      success: true,
      emission: { data: { manifestId: "not-a-list" } },
    }), { status: 200 }));
  try {
    const { listContentHubs } = await import("../api/adminApi.ts");
    await assertRejects(
      () => listContentHubs(),
      Error,
      "content_bundle:list_hubs: emission.data must be an array",
    );
  } finally {
    globalThis.fetch = original;
  }
});
