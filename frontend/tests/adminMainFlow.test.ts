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
    ["/admin/contents", "/admin/ui-builder", "/admin/manifests"],
  );
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: /auth is not a canonical admin workflow step", () => {
  const hrefs = ADMIN_MAIN_FLOW_STEPS.map((s) => s.href);
  assertEquals(hrefs.includes("/auth"), false);
  const labels = ADMIN_MAIN_FLOW_STEPS.map((s) => s.label);
  assertEquals(labels.includes("ログイン"), false);
});

Deno.test("ACCEPTANCE_FLOW_STEPS matches main flow order", () => {
  assertEquals(ACCEPTANCE_FLOW_STEPS, ADMIN_MAIN_FLOW_STEPS);
});

Deno.test("ADMIN_ROUTE_CARDS contain canonical admin routes only", () => {
  assertEquals(
    ADMIN_ROUTE_CARDS.map((card) => card.href),
    [
      "/admin/contents",
      "/admin/ui-builder",
      "/admin/manifests",
      "/admin/enums",
      "/admin/credentials",
      "/admin/users",
      "/admin/scheduler",
    ],
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

/**
 * admin-surface-topology-seed-conversion (Phase 2 proof-drift fix, issue-12): this test
 * previously compared frontend/fresh.gen.ts against a second, independently hardcoded literal
 * array — never actually reading docs/design/runtime-orchestration-ssot.yaml despite its own
 * name's claim to. That let /admin/scheduler drift out of the SSOT registry undetected (issue-03)
 * even though this test's hardcoded expectation happened to already include it. This version
 * reads frontend_routes.admin directly out of the SSOT YAML (raw-text regex extraction, the same
 * pattern already used elsewhere in this test suite for admin-console-workflow-ssot.yaml — see
 * frontend/tests/adminUxGuard.test.ts), so a real SSOT/Fresh-route-tree divergence fails here.
 */
Deno.test("Fresh /admin route registry matches runtime-orchestration SSOT exactly", async () => {
  const generatedManifest = await Deno.readTextFile(
    new URL("../fresh.gen.ts", import.meta.url),
  );
  const adminRoutes = [
    ...new Set(
      [...generatedManifest.matchAll(/"\.\/routes\/admin\/([^"]+)"/g)]
        .map((match) => match[1])
        .filter((route) => route !== "_middleware.ts")
        .map((route) => {
          if (route === "index.tsx") return "/admin";
          return `/admin/${route.replace(/(?:\/index)?\.tsx$/, "")}`;
        }),
    ),
  ].sort();

  const ssotYaml = await Deno.readTextFile(
    new URL(
      "../../docs/design/runtime-orchestration-ssot.yaml",
      import.meta.url,
    ),
  );
  const adminBlockMatch = ssotYaml.match(
    /\n {4}admin:\n((?: {6}- \/[^\n]+\n)+)/,
  );
  if (!adminBlockMatch) {
    throw new Error(
      "could not locate frontend_routes.admin list in runtime-orchestration-ssot.yaml — SSOT structure changed; update this test's extraction regex",
    );
  }
  const ssotAdminRoutes = [...adminBlockMatch[1].matchAll(/- (\/\S+)/g)]
    .map((m) => m[1])
    .sort();

  assertEquals(
    adminRoutes,
    ssotAdminRoutes,
    "Fresh route tree (frontend/routes/admin/*) must match docs/design/runtime-orchestration-ssot.yaml frontend_routes.admin exactly",
  );
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: contents step uses user-facing step wording", () => {
  const contents = ADMIN_MAIN_FLOW_STEPS.find((s) =>
    s.href === "/admin/contents"
  )!;
  assertEquals(contents.purpose.includes("step 1"), true);
  assertEquals(contents.purpose.includes("step 3"), true);
  assertEquals(contents.purpose.toLowerCase().includes("pipeline"), false);
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: contents subSteps have unique labels for stepper keys", () => {
  const contents = ADMIN_MAIN_FLOW_STEPS.find((s) =>
    s.href === "/admin/contents"
  )!;
  const labels = (contents.subSteps ?? []).map((sub) => sub.label);
  assertEquals(new Set(labels).size, labels.length);
});

Deno.test("ADMIN_MAIN_FLOW_STEPS: ui-builder step references canvas workspace", () => {
  const ui = ADMIN_MAIN_FLOW_STEPS.find((s) => s.href === "/admin/ui-builder")!;
  assertEquals(ui.purpose.includes("ルート"), true);
  assertEquals(ui.purpose.includes("canvas workspace"), true);
});

Deno.test("Fresh registry does not retain deleted /dev/admin helper wrappers", async () => {
  const generatedManifest = await Deno.readTextFile(
    new URL("../fresh.gen.ts", import.meta.url),
  );
  for (const route of DELETED_DEV_ADMIN_HELPER_ROUTES) {
    const routeFile = `./routes${route}.tsx`;
    assertEquals(
      generatedManifest.includes(routeFile),
      false,
      `${routeFile} must not remain in generated Fresh registry`,
    );
  }
});
