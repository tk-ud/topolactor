import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  ACCEPTANCE_FLOW_STEPS,
  ADMIN_INDEX_GUIDE,
  ADMIN_MAIN_FLOW_STEPS,
  ADMIN_ROUTE_CARDS,
  ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING,
} from "../content/adminGuides.ts";
import {
  ADMIN_CANONICAL_ROUTE_IDENTITY,
  ADMIN_MAIN_FLOW_ROUTE_ORDER,
} from "../content/adminRouteIdentity.generated.ts";
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

/**
 * admin-surface-topology-seed-conversion round 5 (PR #594 review): the round 4 version of this
 * test only validated that the independently-hardcoded ADMIN_ROUTE_CARDS array was a subset of
 * SSOT-declared routes -- production code (frontend/content/adminGuides.ts) still read only its
 * own static TypeScript literal as authority, so an SSOT change could still silently diverge from
 * the real consumer. Round 5 replaces that with a real source-to-consumer path:
 * frontend/content/adminRouteIdentity.generated.ts is generated from
 * docs/design/admin-console-workflow-ssot.yaml (.agent/scripts/generate_admin_route_identity.py)
 * and adminGuides.ts derives ADMIN_ROUTE_CARDS/ADMIN_MAIN_FLOW_STEPS href+order FROM that
 * generated file, matching each identity entry to hand-authored wording. This test independently
 * re-derives the same identity straight from the SSOT raw text (mirroring the generator's own
 * algorithm) and cross-checks the checked-in generated file against it, so a hand-edited or
 * stale generated file -- not just a stale hardcoded literal -- fails here. Route SET/ORDER only,
 * never wording/copy: frontend-canonical-surface-structure-label-boundary Bundle owns wording.
 */
async function deriveExpectedAdminRouteIdentity() {
  const ssotYaml = (await Deno.readTextFile(
    new URL(
      "../../docs/design/admin-console-workflow-ssot.yaml",
      import.meta.url,
    ),
  )).replace(/\r\n/g, "\n");

  const canonicalRoutesMatch = ssotYaml.match(
    /\n {4}canonical_routes:\n((?: {6}- \/[^\n]+\n)+)/,
  );
  if (!canonicalRoutesMatch) {
    throw new Error(
      "could not locate authority.canonical_routes in admin-console-workflow-ssot.yaml — SSOT structure changed; update this test's extraction regex",
    );
  }
  const canonicalRoutes = [...canonicalRoutesMatch[1].matchAll(/- (\/\S+)/g)]
    .map((m) => m[1]);

  const masterRosterMatch = ssotYaml.match(
    /\n {4}master_roster_routes:\n([\s\S]*?)\n {2}\S/,
  );
  if (!masterRosterMatch) {
    throw new Error(
      "could not locate other_admin_routes.master_roster_routes in admin-console-workflow-ssot.yaml — SSOT structure changed; update this test's extraction regex",
    );
  }
  const masterRosterRoutes = [...masterRosterMatch[1].matchAll(/route: (\/\S+)/g)]
    .map((m) => m[1]);

  const authoringOrderMatch = ssotYaml.match(
    /\n {2}canonical_authoring_order:\n([\s\S]*?)\n {2}\S/,
  );
  if (!authoringOrderMatch) {
    throw new Error(
      "could not locate canonical_authoring_order in admin-console-workflow-ssot.yaml — SSOT structure changed; update this test's extraction regex",
    );
  }
  const authoringOrderBlock = "\n" + authoringOrderMatch[1];
  const subsectionRoute = (key: string): string => {
    const escapedKey = key.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const subsectionMatch = authoringOrderBlock.match(
      new RegExp(`\\n {4}${escapedKey}:\\n((?:.*\\n)*?)(?=\\n {4}\\S|$)`),
    );
    if (!subsectionMatch) {
      throw new Error(
        `could not locate canonical_authoring_order.${key} in admin-console-workflow-ssot.yaml`,
      );
    }
    const routeMatch = subsectionMatch[1].match(/route: (\/\S+)/);
    if (!routeMatch) {
      throw new Error(
        `could not locate route in canonical_authoring_order.${key} in admin-console-workflow-ssot.yaml`,
      );
    }
    return routeMatch[1];
  };
  const mainFlowRouteOrder = [
    subsectionRoute("contents_pipeline"),
    subsectionRoute("canvas_workspace_entry"),
    subsectionRoute("post_contents_entry"),
  ];

  const identity: { href: string; category: string }[] = [];
  const seen = new Set<string>();
  for (const href of mainFlowRouteOrder) {
    if (!seen.has(href)) {
      identity.push({ href, category: "main_flow" });
      seen.add(href);
    }
  }
  for (const href of masterRosterRoutes) {
    if (!seen.has(href)) {
      identity.push({ href, category: "master_roster" });
      seen.add(href);
    }
  }
  for (const href of canonicalRoutes) {
    if (href === "/admin") continue;
    if (!seen.has(href)) {
      identity.push({ href, category: "canonical_route" });
      seen.add(href);
    }
  }

  return { identity, mainFlowRouteOrder };
}

Deno.test("adminRouteIdentity.generated.ts matches independent re-derivation from admin-console-workflow-ssot.yaml (no generator drift)", async () => {
  const expected = await deriveExpectedAdminRouteIdentity();
  assertEquals(
    [...ADMIN_CANONICAL_ROUTE_IDENTITY],
    expected.identity,
    "frontend/content/adminRouteIdentity.generated.ts is out of sync with docs/design/admin-console-workflow-ssot.yaml — regenerate with: python3 .agent/scripts/generate_admin_route_identity.py",
  );
  assertEquals(
    [...ADMIN_MAIN_FLOW_ROUTE_ORDER],
    expected.mainFlowRouteOrder,
    "ADMIN_MAIN_FLOW_ROUTE_ORDER is out of sync with docs/design/admin-console-workflow-ssot.yaml canonical_authoring_order",
  );
});

Deno.test("ADMIN_ROUTE_CARDS order and membership are sourced from ADMIN_CANONICAL_ROUTE_IDENTITY (no fabricated/reordered route)", () => {
  const expectedHrefs = ADMIN_CANONICAL_ROUTE_IDENTITY
    .map((identity) => identity.href)
    .filter((href) => !ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING.includes(href));
  assertEquals(ADMIN_ROUTE_CARDS.map((card) => card.href), expectedHrefs);
});

/**
 * /admin/team-dashboard is canonical per admin-console-workflow-ssot.yaml authority.canonical_routes
 * but has no ADMIN_ROUTE_CARD_WORDING entry and no existing Japanese UX copy anywhere in this
 * repo; authoring that copy is frontend-canonical-surface-structure-label-boundary Bundle's job
 * (wording), not this Bundle's (sourcing). This test pins that as an explicit, tracked,
 * intentional gap rather than an accident: if a NEW canonical route appears with no wording, this
 * assertion fails and forces an explicit decision (add wording, or add it here with a reason)
 * instead of the card silently never appearing.
 */
Deno.test("ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING is exactly the tracked/expected unresolved-wording set", () => {
  assertEquals(ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING, ["/admin/team-dashboard"]);
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

/**
 * admin-surface-topology-seed-conversion round 5 (PR #594 review): ADMIN_INDEX_GUIDE audit.
 * ADMIN_INDEX_GUIDE has no href/order array field of its own (unlike ADMIN_ROUTE_CARDS/
 * ADMIN_MAIN_FLOW_STEPS) -- every field (title/purpose/howToSteps/prerequisites/inputs/actions/
 * outputs/boundaryNotes) is hand-authored prose, which is wording and stays out of this Bundle's
 * scope (frontend-canonical-surface-structure-label-boundary Bundle owns it). The one
 * structural (non-wording) property this guide's prose does carry is ORDER: purpose/howToSteps
 * mention the main-flow routes' UX labels in the same sequence as the actual authoring order.
 * That sequence is checked here against ADMIN_MAIN_FLOW_STEPS (itself sourced from
 * ADMIN_MAIN_FLOW_ROUTE_ORDER, i.e. admin-console-workflow-ssot.yaml canonical_authoring_order)
 * without asserting anything about the surrounding wording text itself.
 */
Deno.test("ADMIN_INDEX_GUIDE.purpose mentions main-flow step labels in canonical_authoring_order sequence", () => {
  const labelsInOrder = ADMIN_MAIN_FLOW_STEPS.map((step) => step.label);
  const positions = labelsInOrder.map((label) => ADMIN_INDEX_GUIDE.purpose.indexOf(label));
  for (let i = 0; i < labelsInOrder.length; i++) {
    assertEquals(positions[i] >= 0, true, `ADMIN_INDEX_GUIDE.purpose does not mention label ${JSON.stringify(labelsInOrder[i])}`);
  }
  const sorted = [...positions].sort((a, b) => a - b);
  assertEquals(positions, sorted, "ADMIN_INDEX_GUIDE.purpose mentions main-flow labels out of canonical_authoring_order sequence");
});

Deno.test("ADMIN_INDEX_GUIDE.howToSteps mentions main-flow step labels in canonical_authoring_order sequence", () => {
  const labelsInOrder = ADMIN_MAIN_FLOW_STEPS.map((step) => step.label);
  const joinedSteps = ADMIN_INDEX_GUIDE.howToSteps.join("\n");
  const positions = labelsInOrder.map((label) => joinedSteps.indexOf(label));
  for (const position of positions) {
    assertEquals(position >= 0, true, "ADMIN_INDEX_GUIDE.howToSteps does not mention every main-flow step label");
  }
  const sorted = [...positions].sort((a, b) => a - b);
  assertEquals(positions, sorted, "ADMIN_INDEX_GUIDE.howToSteps mentions main-flow labels out of canonical_authoring_order sequence");
});
