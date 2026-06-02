import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_FLOW_STEPS,
  ADMIN_MAIN_FLOW_STEPS,
  ADMIN_ROUTE_CARDS,
} from "../content/adminGuides.ts";
import { UX_MAIN_FLOW_STEP_LABELS } from "../content/adminUxTerms.ts";

const NON_CANONICAL_ADMIN_ROUTES = [
  "/admin/import",
  "/admin/hub-navigation",
  "/admin/runtime",
  "/admin/seed",
  "/admin/context-token-registry",
  "/admin/registry-vector-validate",
];

const DELETED_DEV_ADMIN_HELPER_ROUTES = [
  "/dev/admin/import",
  "/dev/admin/hub-navigation",
  "/dev/admin/runtime",
  "/dev/admin/seed",
  "/dev/admin/context-token-registry",
  "/dev/admin/registry-vector-validate",
];

Deno.test("ADMIN_MAIN_FLOW_STEPS matches canonical admin workflow", () => {
  assertEquals(ADMIN_MAIN_FLOW_STEPS.map((s) => s.label), [
    ...UX_MAIN_FLOW_STEP_LABELS,
  ]);
  assertEquals(
    ADMIN_MAIN_FLOW_STEPS.map((s) => s.href),
    ["/auth", "/admin/contents", "/admin/ui-builder", "/admin/manifests"],
  );
});

Deno.test("ACCEPTANCE_FLOW_STEPS matches main flow order", () => {
  assertEquals(ACCEPTANCE_FLOW_STEPS, ADMIN_MAIN_FLOW_STEPS);
});

Deno.test("ADMIN_ROUTE_CARDS contain canonical admin routes only", () => {
  assertEquals(
    ADMIN_ROUTE_CARDS.map((card) => card.href),
    ["/admin/contents", "/admin/ui-builder", "/admin/manifests"],
  );
});

Deno.test("canonical admin navigation does not expose removed legacy/debug routes", () => {
  const hrefs = [
    ...ADMIN_MAIN_FLOW_STEPS.map((step) => step.href),
    ...ACCEPTANCE_FLOW_STEPS.map((step) => step.href),
    ...ADMIN_ROUTE_CARDS.map((card) => card.href),
  ];
  for (const route of NON_CANONICAL_ADMIN_ROUTES) {
    assertEquals(hrefs.includes(route), false);
  }
});

Deno.test("Fresh /admin route registry matches runtime-orchestration SSOT exactly", async () => {
  const generatedManifest = await Deno.readTextFile(new URL("../fresh.gen.ts", import.meta.url));
  const adminRoutes = [...new Set(
    [...generatedManifest.matchAll(/"\.\/routes\/admin\/([^"]+)"/g)]
      .map((match) => match[1])
      .filter((route) => route !== "_middleware.ts")
      .map((route) => route === "index.tsx" ? "/admin" : `/admin/${route.replace(/\.tsx$/, "")}`),
  )].sort();
  assertEquals(adminRoutes, ["/admin", "/admin/contents", "/admin/manifests", "/admin/ui-builder"]);
});


Deno.test("Fresh registry does not retain deleted /dev/admin helper wrappers", async () => {
  const generatedManifest = await Deno.readTextFile(new URL("../fresh.gen.ts", import.meta.url));
  for (const route of DELETED_DEV_ADMIN_HELPER_ROUTES) {
    const routeFile = `./routes${route}.tsx`;
    assertEquals(
      generatedManifest.includes(routeFile),
      false,
      `${routeFile} must not remain in generated Fresh registry`,
    );
  }
});
