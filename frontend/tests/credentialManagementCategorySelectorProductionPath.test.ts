/**
 * Production-path proof for manifest 092 (credential-management)'s category selector —
 * structural_subtree_conditional_visibility_contract's CONSUMER/evidence (docs/design/
 * admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.
 * seed_contract.component_tree[credential_category_filter].conditional_visibility_note).
 *
 * Unlike layoutProjectionTreeVisibilityRender.test.ts (generic, domain-neutral catA/catB
 * fixture proving the SHARED evaluator mechanism) and layoutSchemaStructuralRender.test.ts's
 * own manifest-092 DOM test (proves a single already-active category's real Action leaves
 * reach the DOM), THIS file proves the full, real, production category-SWITCH operation
 * chain for manifest 092 specifically, against a real DOM (happy-dom + Preact render(), not
 * renderToString) and a real native tab `click` event — never a direct dispatcher.set()
 * standing in for the user's own interaction:
 *
 *   real tab buttons exist (3, matching Topolactor.Schema.CredentialManagementCategories.All
 *   exactly — see LayoutSchemaStructuralCompositionTests.cs's own
 *   ManifestCd004RealSeedContent_CategoryFilterTabsItems_* companion proof) -> initial
 *   aria-selected tab matches the declared default -> firing a REAL native "click" DOM event on
 *   a REAL tab `<button role="tab">` (tabEl.dispatchEvent(new MouseEvent("click"))) drives the
 *   SAME onClick/onSelect closure runtimeComponentFactory.ts's tabsFactory wires for production ->
 *   emitBoundEvent -> the authored setState runtimeInteraction -> the projection-local
 *   selectedCategory state slot -> resolveNodeVisibility -> DOM mount of the new category's real
 *   Action leaves / unmount of the old category's -> a hide-then-show round trip reproduces
 *   byte-identical markup for an unaffected action (dispatchTargetRef/payloadFrom survive).
 *
 * (structural-subtree-conditional-visibility-implementation, tabs-presentation closure round:
 * credential_category_filter's presentation moved from select.template to tabs.template —
 * docs/design/admin-normal-surface-projection-seed-ssot.yaml's own presentation_history field —
 * driving the SAME ui-local:credential_category_filter.selectedCategory source slot the generic
 * SSOT's presentation_component_independence text already anticipated a tabs.template consumer
 * would use, with zero change to the generic evaluator. This file was a <select>/"change"-event
 * proof before that round; it is rewritten here to drive the real tabs UI instead, never a
 * `dispatcher.set()` standing in for it.)
 *
 * The physical category node's own key (users/external_api_credential/instance_settings) is
 * independent from the visibilityBinding matchValue/tab item key (users/
 * external_api_credential/external_instance_credential) for the third category — see the
 * ManifestCd002RealSeedContent test's own doc comment for why "instance_settings" and
 * "external_instance_credential" are deliberately different, SSOT-grounded strings, never
 * conflated or invented here.
 *
 * No lifecycle-triggered (initial_mount/route_enter/initial_display) runtimeInteraction exists
 * anywhere in manifest 092's real seed today, so this file does not fabricate one; lifecycle
 * reachability gating by the SAME resolveNodeVisibility evaluator is proven generically by
 * frontend/tests/uiEventEffectRunner.test.ts instead.
 *
 * structural-subtree-conditional-visibility-implementation round (owner-directed
 * credential-management seed UI日本語化): action labels asserted here are the real, seed-authored
 * Japanese display text (admin-normal-surface-projection-seed-ssot.yaml credentials.seed_contract.
 * display_language_boundary) -- machine identity (data-node-id, the tab item `key`s asserted
 * against EXPECTED_CATEGORY_VALUES, targetRef/payloadFrom) stays canonical/English and is never
 * used interchangeably with a label string as an identifier. Step 6 below additionally audits
 * that no pre-translation English operation sentence remains reachable anywhere in this
 * manifest's rendered surface, across all three categories.
 */
import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import type { Emission, LayoutNode } from "../api/dispatch.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { LayoutProjectionTree } from "../components/LayoutProjectionTree.tsx";
import {
  createProjectionStateDispatcher,
  createRuntimeLocalStateStore,
} from "../runtime/uiEventEffectRunner.ts";
import { createLiveNodeValueTracker } from "../runtime/liveNodeValueTracker.ts";
import type { WiringNode } from "../lib/uiBuilderWiringProjection.ts";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

/** The SAME 3-member enum backend/schema/CredentialManagementSearchContracts.cs's
 * CredentialManagementCategories.All declares — asserted equal to the real seed's authored
 * options by LayoutSchemaStructuralCompositionTests.cs; duplicated here only as an assertion
 * target for THIS file's own DOM proof, never as a second source of truth the seed is
 * generated from. */
const EXPECTED_CATEGORY_VALUES = ["users", "external_api_credential", "external_instance_credential"];

function toRunnerWiringNodes(layoutNodes: readonly LayoutNode[]): WiringNode[] {
  return layoutNodes
    .filter((n): n is LayoutNode & { nodeId: string } => typeof n.nodeId === "string" && n.nodeId.length > 0)
    .map((n) => ({
      nodeId: n.nodeId,
      componentKey: n.componentKey,
      componentKind: n.componentKind,
      stateJson: n.stateJson ?? undefined,
      runtimeInteractions: n.runtimeInteractions ?? undefined,
      parentNodeId: n.parentNodeId,
      visibilityBinding: n.visibilityBinding ?? undefined,
    }));
}

async function loadManifest092Fixture(): Promise<LayoutNode[]> {
  const text = await Deno.readTextFile(
    new URL("./fixtures/manifest_0092_bare_entry_layout_nodes.json", import.meta.url),
  );
  return JSON.parse(text) as LayoutNode[];
}

function getTabButtons(container: Element): { key: string; el: Element }[] {
  const buttons = Array.from(
    container.querySelectorAll('[data-node-id="credential_category_filter"] button[role="tab"]'),
  );
  return buttons.map((el, i) => ({ key: EXPECTED_CATEGORY_VALUES[i], el }));
}

function clickTab(container: Element, key: string): void {
  const tabs = getTabButtons(container);
  const target = tabs.find((t) => t.key === key);
  assert(target, `expected a real tab button for category "${key}"`);
  (target!.el as unknown as { dispatchEvent: (e: Event) => boolean }).dispatchEvent(
    new Event("click", { bubbles: true }),
  );
}

function activeTabKey(container: Element): string | undefined {
  const tabs = getTabButtons(container);
  return tabs.find((t) => (t.el as unknown as { getAttribute: (n: string) => string | null }).getAttribute("aria-selected") === "true")?.key;
}

Deno.test("production path: manifest 092's real credential_category_filter tabs drive a real category switch end to end (real DOM, real click event, real evaluator, real mount/unmount)", async () => {
  const layoutNodes = await loadManifest092Fixture();
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), localStore);
  const tracker = createLiveNodeValueTracker();

  const { container, cleanup } = setupDom();
  try {
    function renderTree(): void {
      const specs = renderEmission(emission, defaultComponentRegistry, {
        localStateStore: dispatcher,
        payloadFromNodeValues: tracker.snapshot(),
        onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
      });
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }

    // Mirrors ProjectionShell.tsx's own production wiring: a state-store mutation (from the
    // setState runtimeInteraction the real "click"/"select" event below fires) notifies this
    // listener, which re-runs renderEmission() and re-renders — never a test-only manual
    // re-render call standing in for the production reactive loop.
    localStore.subscribe(renderTree);

    renderTree();
    await flushUpdates();

    const tabsRoot = container.querySelector('[data-node-id="credential_category_filter"]');
    assert(tabsRoot, "expected the real category filter tabs node to exist in the DOM");

    // 1. Real, data-defined tab buttons — never an empty tablist masquerading as a completed
    // category selector, and never a fabricated/invented category label.
    const tabButtons = getTabButtons(container);
    assertEquals(tabButtons.map((t) => t.key), EXPECTED_CATEGORY_VALUES);
    assertEquals(tabButtons.length, 3, "expected exactly 3 real tab buttons, matching Topolactor.Schema.CredentialManagementCategories.All");

    // 2. Initial active tab matches the declared default (stateJson's selectedCategory).
    assertEquals(activeTabKey(container), "external_api_credential");

    // 3. Initial DOM: only the default-active category's real Action leaves are reachable.
    let html = container.innerHTML;
    assert(html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's own action in the initial DOM");
    assert(!html.includes("ユーザーアカウントを作成"), "expected users' action absent from the initial DOM");
    assert(!html.includes(">検証<"), "expected instance_settings/external_instance_credential's action absent from the initial DOM");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes on initial render");

    const firstApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assert(firstApiCredentialActionHtml, "expected to capture the external_api_credential action's initial markup");

    // 4. Fire a REAL native "click" DOM event on a REAL tab `<button role="tab">` — the same
    // interaction a browser user performs — never a direct dispatcher.set()/store mutation
    // standing in for it.
    clickTab(container, "external_instance_credential");
    await flushUpdates();

    html = container.innerHTML;
    assertEquals(
      activeTabKey(container),
      "external_instance_credential",
      "expected the real tab's aria-selected state to follow the user's own real click",
    );
    assert(html.includes(">検証<"), "expected instance_settings/external_instance_credential's own action to mount after the real switch");
    assert(!html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's action to unmount — never merely hidden — after switching away from it");
    assert(!html.includes("ユーザーアカウントを作成"), "expected users' action to remain absent");
    assert(!/rounded border border-red-200/.test(html), "expected zero visible error boxes after the real switch");
    // The tab buttons themselves never change — this is a structural mount/unmount of content,
    // not a re-population of the filter control itself.
    assertEquals(getTabButtons(container).map((t) => t.key), EXPECTED_CATEGORY_VALUES);

    // 5. Fire a SECOND real click back to the original category — a hide-then-show round
    // trip through the real control — and prove the unaffected action's own markup (dispatch
    // target / payloadFrom wiring) is byte-identical to its first-render form.
    clickTab(container, "external_api_credential");
    await flushUpdates();

    html = container.innerHTML;
    assert(html.includes("外部APIクレデンシャルレコードを作成"), "expected external_api_credential's action to re-mount on switching back");
    assert(!html.includes(">検証<"), "expected instance_settings/external_instance_credential's action to unmount again after switching away");
    const roundTrippedApiCredentialActionHtml = container.querySelector(
      '[data-node-id="external_api_credential_create_button"]',
    )?.outerHTML;
    assertEquals(
      roundTrippedApiCredentialActionHtml,
      firstApiCredentialActionHtml,
      "expected the external_api_credential action's markup (dispatchTargetRef/payloadFrom wiring) to survive a real hide-then-show round trip byte-identically",
    );

    // 5b. A disabled tab must never dispatch — hidden-category action/lifecycle unreachability
    // is proven generically elsewhere, but the tabs control itself must also refuse a click on
    // a disabled item. Manifest 092's real seed authors zero disabled tabs today, so this only
    // asserts the structural guard the shared Tabs.tsx component itself enforces (see its own
    // `!tab.disabled && onSelect(tab.key)` onClick guard) rather than fabricating a disabled
    // category that doesn't exist in the real seed.
    for (const t of getTabButtons(container)) {
      assertEquals(
        (t.el as unknown as { disabled: boolean }).disabled,
        false,
        `expected no real category tab ("${t.key}") to be disabled in manifest 092's authored seed`,
      );
    }

    // 6. Japanese seed UI audit (structural-subtree-conditional-visibility-implementation round,
    // owner-directed credential-management seed UI日本語化): none of the pre-translation English
    // operation sentences this exact production-path proof used to assert are still reachable
    // anywhere in this manifest's rendered surface, across every category. Cycle through all
    // three categories (the third, external_instance_credential, was never re-selected above) so
    // this check covers content this test's earlier steps never rendered.
    let allHtml = html;
    for (const category of EXPECTED_CATEGORY_VALUES) {
      clickTab(container, category);
      await flushUpdates();
      allHtml += container.innerHTML;
    }
    for (
      const staleEnglish of [
        "Create external API credential record",
        "Create user account (with initial credential)",
        "Create external instance credential record",
        ">Validate<",
        ">Preview<",
        ">Apply<",
        ">Approve<",
      ]
    ) {
      assert(
        !allHtml.includes(staleEnglish),
        `expected no pre-translation English operation sentence "${staleEnglish}" to remain reachable anywhere in the credential-management surface`,
      );
    }
  } finally {
    render(null, container as unknown as Element);
    cleanup();
  }
});

/**
 * Confirmation-Modal production-path proof (existing-pr-update round, credential-category
 * tabs source-lineage closure): the category-switch proof above only proves category state
 * change and structural mount/unmount; it never opens a Modal. This test drives, via real DOM
 * clicks (never dispatcher.set()/direct state mutation), the full preview -> dryRun dispatch ->
 * settle -> deferred openModal local-state mutation -> real Modal DOM chain for every reachable
 * mutation-preview action under the default-active external_api_credential category (the same
 * category credential_category_filter's own stateJson default already renders without a tab
 * switch): create / update / delete, plus the sibling consumer_reference_binding action
 * (configure_scheduler_job_credential_or_port_binding), whose Modal body was pre-translation
 * English until this round (see admin-normal-surface-projection-seed-ssot.yaml
 * display_language_boundary).
 *
 * Mid-work discovery this round: db/seed_empty.sql's manifest-092 cd004 tensor authored every
 * one of these 12 preview buttons' openModal runtimeInteraction directly on the BUTTON's own
 * tensor node (sourceActionKey == nodeId), but LayoutSchemaTensorComposer.cs's merge only
 * resolves a leaf's runtimeInteractions via "{resolvedParentNodeId}::{key}" -- the SAME
 * parent-scoped convention credential_search_section/credential_category_filter's own tabs
 * wiring already uses correctly. The self-scoped authoring silently orphaned every openModal
 * interaction at real compose time (proven empirically: a real click never set the Modal's
 * open state before this round's fix relocated each entry onto its owning Section's own tensor
 * node, sourceActionKey unchanged). This was a genuine, systemic, pre-existing production gap
 * across the WHOLE credential-management surface (also affects the users/eic CRUD Modals this
 * test does not exercise), not something this round's translation work introduced -- fixed here
 * because it directly blocks this round's own explicit requirement to prove a Modal actually
 * opens via a real click, never a hidden/never-reachable disclosure state.
 *
 * credential-management-ux-gaps-followup round: the 37 payloadFrom-referenced Fields this test's
 * scenarios read (control was form_input/form_field -- a label plus a hardcoded empty <span>,
 * never a real <input>, until that round re-authored them to form_input/input) are now driven by
 * real native <input> "input" events on their real rendered elements (typeIntoRealInput /
 * typeRealInputsForCategoryOnce below), not tracker.set() -- see the comment at their call site
 * for the still-open, out-of-scope exception (the handful of sibling Select fields whose seed
 * carries no real, non-placeholder option to select from).
 */
Deno.test("production path: real dryRun-preview click opens each reachable confirmation Modal (real DOM, real settled dispatch, Japanese title/body/confirm/cancel, dispatchTargetRef/payloadFrom preserved on Confirm)", async () => {
  const layoutNodes = await loadManifest092Fixture();
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  const scenarios: {
    base: string;
    category: string;
    title: string;
    bodyIncludes: string;
    confirmLabel: string;
  }[] = [
    {
      base: "external_api_credential_create",
      category: "external_api_credential",
      title: "外部APIクレデンシャルレコードを作成",
      bodyIncludes: "選択したレコード種別に対して新しいレコードを作成します",
      confirmLabel: "作成",
    },
    {
      base: "external_api_credential_update",
      category: "external_api_credential",
      title: "外部APIクレデンシャルレコードを更新",
      bodyIncludes: "指定したレコードのメタデータを更新し",
      confirmLabel: "更新",
    },
    {
      base: "external_api_credential_delete",
      category: "external_api_credential",
      title: "外部APIクレデンシャルレコードを無効化",
      bodyIncludes: "無効化(論理削除)します",
      confirmLabel: "無効化",
    },
    {
      base: "configure_scheduler_job_credential_or_port_binding",
      category: "external_api_credential",
      title: "スケジューラージョブのクレデンシャル/ポートバインディングを設定",
      bodyIncludes: "クレデンシャル/ポート参照をスケジューラージョブに紐付けます",
      confirmLabel: "設定",
    },
    // structural-subtree-conditional-visibility-implementation bundle closure round: the
    // remaining 8 of 12 confirmation-Modal pairs, requiring a real tab switch (users /
    // external_instance_credential are never the default-active category) to mount their
    // preview button at all — proving the SAME open/Cancel-close/reopen/Confirm chain survives
    // a real category hide->show round trip, not just the already-active default category.
    {
      base: "credentials_users_create",
      category: "users",
      title: "ユーザーアカウントを作成",
      bodyIncludes: "指定したユーザー名・初期パスワード・ロール等でアカウントと初期クレデンシャルを作成します",
      confirmLabel: "作成を確定",
    },
    {
      base: "credentials_users_update",
      category: "users",
      title: "ユーザーアカウントを更新",
      bodyIncludes: "指定したユーザーIDのアカウントメタデータ",
      confirmLabel: "更新を確定",
    },
    {
      base: "credentials_users_delete",
      category: "users",
      title: "アカウントを削除",
      bodyIncludes: "指定したユーザーIDのアカウントと紐づくクレデンシャルを削除します",
      confirmLabel: "削除を確定",
    },
    {
      base: "credentials_users_revoke_credential",
      category: "users",
      title: "クレデンシャルを失効",
      bodyIncludes: "指定したユーザーIDのクレデンシャル行を削除し",
      confirmLabel: "クレデンシャル失効を確定",
    },
    {
      base: "credentials_users_revoke_sessions",
      category: "users",
      title: "セッションを失効",
      bodyIncludes: "指定したセッションID(空欄の場合は全て)のアクティブセッションを失効させます",
      confirmLabel: "セッション失効を確定",
    },
    {
      base: "eic_create",
      category: "external_instance_credential",
      title: "外部インスタンスクレデンシャルを作成",
      bodyIncludes: "選択したレコード種別に対して新しいレコードを作成します",
      confirmLabel: "作成を確定",
    },
    {
      base: "eic_update",
      category: "external_instance_credential",
      title: "外部インスタンスクレデンシャルを更新",
      bodyIncludes: "指定したレコードIDのメタデータを更新します",
      confirmLabel: "更新を確定",
    },
    {
      base: "eic_delete",
      category: "external_instance_credential",
      title: "外部インスタンスクレデンシャルを無効化",
      bodyIncludes: "指定したレコードIDを無効化します",
      confirmLabel: "無効化を確定",
    },
  ];

  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const dispatchedBodies: Record<string, unknown>[] = [];
  // deno-lint-ignore no-explicit-any
  (globalThis as any).fetch = (url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path !== "/api/dispatch") {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
    dispatchedBodies.push(body);
    // target_ref rides inside the request's own "payload" object (see
    // frontendScheduler.ts enqueueRuntimeComponentCommand: payload.target_ref = spec.targetRef),
    // never at the request body's top level.
    const requestPayload = body.payload as Record<string, unknown> | undefined;
    const targetRef = typeof requestPayload?.target_ref === "string" ? requestPayload.target_ref : undefined;
    const manifestMatch = targetRef ? /^manifest:([^:]+):/.exec(targetRef) : null;
    const manifestId = manifestMatch ? manifestMatch[1] : "00000000-0000-0000-0000-000000000000";
    // A settled dispatch is only "accepted" (see runtimeDispatchSettlement.ts) when it carries
    // an emission whose manifestId matches the targetRef's own embedded manifest UUID -- the
    // SAME real acceptance gate a genuine backend response must satisfy, never bypassed here.
    return Promise.resolve(
      new Response(
        JSON.stringify({
          success: true,
          errors: [],
          emission: { manifestId, layoutId: "mock-confirm-modal-dispatch", layoutNodes: [] },
        }),
        { status: 200 },
      ),
    );
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), localStore);
  const tracker = createLiveNodeValueTracker();
  const { container, cleanup } = setupDom();

  try {
    function renderTree(): void {
      const specs = renderEmission(emission, defaultComponentRegistry, {
        localStateStore: dispatcher,
        payloadFromNodeValues: tracker.snapshot(),
        onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
      });
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }
    localStore.subscribe(renderTree);
    renderTree();
    await flushUpdates();

    // credential-management-ux-gaps-followup round: the 37 payloadFrom-referenced Fields that
    // were authored control=form_input/form_field (formFieldFactory -> a label plus a hardcoded
    // empty <span>, no real <input> at all) were re-authored to control=form_input/input (see
    // db/seed_empty.sql's cd002 layout row and .agent/tests/fixtures/
    // react-schema-topology-seed-translator/credential-management-0092.input.json). The comment
    // this replaced claimed a full form-typing proof existed "elsewhere" for these fields, which
    // was never true for THIS surface's own 37 fields (they were not typeable in production at
    // all until that round) — so this test now drives every one of them via a REAL native <input>
    // "input" event on the REAL rendered element (typeIntoRealInput below), the same write path
    // liveNodeValueTracker.ts's onNodeValueChange documents a genuine keystroke using, never
    // tracker.set() standing in for it.
    //
    // The handful of already-select-controlled fields in the same forms (record_kind/active/
    // approve/role_name — control=form_input/select, unaffected by the form_input/form_field bug
    // this round fixed) are a SEPARATE, still-open, out-of-scope gap: manifest 092's real seed
    // authors these Select nodes with only the placeholder "" option — data.options is empty —
    // confirmed against the real DOM dump this Bundle used as its own bug evidence, so there is no
    // real non-empty <option> a genuine user could select today. Faking one here would misrepresent
    // that gap as fixed. These keep using tracker.set() (documented, not hidden) until that
    // separate seed/options-wiring gap is addressed.
    function typeIntoRealInput(nodeId: string, value: string): void {
      const inputEl = container.querySelector(
        `[data-node-id="${nodeId}"] input`,
      ) as unknown as { value: string; dispatchEvent: (e: Event) => boolean } | null;
      assert(inputEl, `expected a real <input> for Field "${nodeId}" (control=form_input/input) in the currently-active category's mounted DOM`);
      inputEl.value = value;
      inputEl.dispatchEvent(new Event("input", { bubbles: true }));
    }

    const realInputFieldsByCategory: Record<string, string[]> = {
      external_api_credential: [
        "external_api_credential_form_hook_path_input",
        "external_api_credential_form_route_key_input",
        "external_api_credential_form_header_key_input",
        "external_api_credential_form_token_kind_input",
        "external_api_credential_form_provider_kind_input",
        "external_api_credential_form_reference_key_input",
        "external_api_credential_form_credential_kind_input",
        "external_api_credential_form_secret_input",
        "external_api_credential_form_required_by_bundle_input",
        "external_api_credential_form_url_or_env_reference_input",
        "external_api_credential_form_refresh_before_seconds_input",
        "external_api_credential_form_encryption_key_reference_input",
        "external_api_credential_form_record_id_input",
        "scheduler_job_id_input",
        "scheduler_external_port_ref_input",
        "scheduler_credential_requirement_ref_input",
      ],
      users: [
        "credentials_users_username_input",
        "credentials_users_password_input",
        "credentials_users_status_input",
        "credentials_users_suspended_from_input",
        "credentials_users_suspended_until_input",
        "credentials_users_state_note_input",
        "credentials_users_user_id_input",
        "credentials_users_session_id_input",
      ],
      external_instance_credential: [
        "eic_provider_kind_input",
        "eic_reference_key_input",
        "eic_required_by_bundle_input",
        "eic_instance_authority_key_input",
        "eic_record_id_input",
      ],
    };
    // Select-controlled Fields sharing the SAME forms above — see the out-of-scope note above.
    for (
      const fieldNodeId of [
        "external_api_credential_form_record_kind_input",
        "external_api_credential_form_active_input",
        "credentials_users_approve_input",
        "credentials_users_role_name_input",
        "credentials_users_active_input",
        "eic_record_kind_input",
        "eic_active_input",
      ]
    ) {
      tracker.set(fieldNodeId, "test-value");
    }

    const categoriesAlreadyTyped = new Set<string>();
    function typeRealInputsForCategoryOnce(category: string): void {
      if (categoriesAlreadyTyped.has(category)) return;
      categoriesAlreadyTyped.add(category);
      for (const nodeId of realInputFieldsByCategory[category] ?? []) {
        typeIntoRealInput(nodeId, `real-dom-value:${nodeId}`);
      }
    }

    function modalHtml(base: string): string {
      // Scoped to the Modal's OWN DOM subtree — never the whole page's innerHTML, since the
      // preview button's own label is byte-identical to its paired Modal's title for several of
      // these scenarios (e.g. "外部APIクレデンシャルレコードを作成" names both), which would make a
      // whole-page substring check pass even while the Modal itself never actually opened.
      return container.querySelector(`[data-node-id="${base}_confirm_modal"]`)?.innerHTML ?? "";
    }

    for (const scenario of scenarios) {
      // Real tab click to mount this scenario's own category — a no-op re-click when the
      // category is already active (external_api_credential, the stateJson default), and a
      // real hide->show round trip for users/external_instance_credential, which are never
      // default-active. Proves the confirmation-Modal chain survives a real category switch,
      // not just the already-mounted default category.
      if (activeTabKey(container) !== scenario.category) {
        clickTab(container, scenario.category);
        await flushUpdates();
      }
      assertEquals(activeTabKey(container), scenario.category, `expected category "${scenario.category}" to be active before exercising "${scenario.base}"`);

      // Real native <input> "input" events on this category's own now-mounted real <input>
      // elements — see typeRealInputsForCategoryOnce above. Runs once per category (the first
      // scenario to visit it); later scenarios sharing the same category/form reuse the same
      // already-typed live-tracker values, exactly as a real user re-using an already-filled form
      // across several of its own action buttons would.
      typeRealInputsForCategoryOnce(scenario.category);

      const previewButton = container.querySelector(
        `[data-node-id="${scenario.base}_button"] button`,
      ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
      assert(previewButton, `expected a real preview button for "${scenario.base}" under category "${scenario.category}"`);

      // Hidden-until-opened: the Modal's own subtree must render nothing before the real click
      // (modalFactory renders an empty subtree while closed — see runtimeComponentFactory.ts).
      assertEquals(modalHtml(scenario.base), "", `expected "${scenario.base}_confirm_modal" to render empty before the preview click`);

      // 1. Real native click on the REAL preview button — never dispatcher.set()/direct state
      // mutation standing in for the user's own dryRun-preview interaction.
      previewButton!.dispatchEvent(new Event("click", { bubbles: true }));
      // The dryRun dispatch settles asynchronously through the real FIFO queue + fetch mock;
      // poll real microtask/macrotask turns until the Modal's local "open" state actually flips.
      let opened = false;
      for (let i = 0; i < 40 && !opened; i++) {
        await flushUpdates();
        opened = dispatcher.get(`${scenario.base}_confirm_modal`, "open") === true;
      }
      assert(opened, `expected "${scenario.base}_confirm_modal"'s open state to become true after the real dryRun-preview click settles`);

      // 2. The real Modal DOM now shows the seed-authored Japanese title/body — never the
      // pre-translation English body this round replaced, and never a string-absence-only proof
      // standing in for actually opening the Modal.
      assert(modalHtml(scenario.base).includes(scenario.title), `expected the real Modal DOM to show title "${scenario.title}" after opening`);
      assert(modalHtml(scenario.base).includes(scenario.bodyIncludes), `expected the real Modal DOM to show the Japanese body containing "${scenario.bodyIncludes}"`);
      assert(modalHtml(scenario.base).includes(`>${scenario.confirmLabel}<`), `expected the real Modal DOM to show the Confirm button label "${scenario.confirmLabel}"`);
      assert(modalHtml(scenario.base).includes(">キャンセル<"), `expected the real Modal DOM to show the Cancel button label "キャンセル"`);

      // 3. Real Cancel click closes the Modal (toggle/closeModal) — machine wiring intact.
      const cancelButton = container.querySelector(
        `[data-node-id="${scenario.base}_cancel_button"] button`,
      ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
      assert(cancelButton, `expected a real Cancel button for "${scenario.base}"`);
      cancelButton!.dispatchEvent(new Event("click", { bubbles: true }));
      await flushUpdates();
      assertEquals(
        dispatcher.get(`${scenario.base}_confirm_modal`, "open"),
        false,
        `expected "${scenario.base}_confirm_modal" to close on a real Cancel click`,
      );
      assertEquals(modalHtml(scenario.base), "", `expected "${scenario.base}_confirm_modal" to render empty again after Cancel`);

      // 4. Re-open, then click Confirm for real — proving the Confirm button's own
      // dispatchTargetRef/payloadFrom (never the preview button's dryRun copy) is what actually
      // gets dispatched, confirmed=true, and the Modal closes only once that dispatch settles.
      previewButton!.dispatchEvent(new Event("click", { bubbles: true }));
      opened = false;
      for (let i = 0; i < 40 && !opened; i++) {
        await flushUpdates();
        opened = dispatcher.get(`${scenario.base}_confirm_modal`, "open") === true;
      }
      assert(opened, `expected "${scenario.base}_confirm_modal" to re-open on a second real preview click`);

      const confirmButtonNode = layoutNodes.find((n) => n.nodeId === `${scenario.base}_confirm_button`);
      assert(confirmButtonNode, `expected a real confirm button LayoutNode for "${scenario.base}"`);
      const expectedTargetRef =
        (confirmButtonNode!.dispatchTargetRefByTrigger as Record<string, string> | undefined)?.click;
      assert(expectedTargetRef, `expected the seed-authored dispatchTargetRefByTrigger.click on "${scenario.base}_confirm_button"`);

      const confirmButton = container.querySelector(
        `[data-node-id="${scenario.base}_confirm_button"] button`,
      ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
      assert(confirmButton, `expected a real Confirm button for "${scenario.base}"`);
      const dispatchedBeforeConfirm = dispatchedBodies.length;
      confirmButton!.dispatchEvent(new Event("click", { bubbles: true }));
      let closed = false;
      for (let i = 0; i < 40 && !closed; i++) {
        await flushUpdates();
        closed = dispatcher.get(`${scenario.base}_confirm_modal`, "open") === false;
      }
      assert(closed, `expected "${scenario.base}_confirm_modal" to close once the real Confirm dispatch settles`);
      assertEquals(
        dispatchedBodies.length,
        dispatchedBeforeConfirm + 1,
        `expected exactly one new /api/dispatch request from the real Confirm click`,
      );
      const confirmBody = dispatchedBodies[dispatchedBodies.length - 1];
      const confirmPayload = confirmBody.payload as Record<string, unknown> | undefined;
      assert(confirmPayload, "expected the real Confirm dispatch to carry a payload");
      assertEquals(
        confirmPayload!.target_ref,
        expectedTargetRef,
        `expected the real Confirm dispatch to carry "${scenario.base}_confirm_button"'s own seed-authored dispatchTargetRef, unchanged by opening/closing the Modal`,
      );
      // resolvePayloadFrom resolves "literal:true" to the string "true" (see
      // payloadFromResolver.ts) — the SAME literal-string convention dryRun's own
      // "literal:true" resolves to on the preview button, never a JS boolean.
      assertEquals(
        confirmPayload!.confirmed,
        "true",
        `expected the real Confirm dispatch payload to carry confirmed="true" (never the preview button's dryRun="true")`,
      );
      assert(
        !("dryRun" in confirmPayload!),
        `expected the real Confirm dispatch payload to carry no dryRun flag at all (unlike the preview button's own payload)`,
      );

      // Full round trip proof: at least one value this scenario actually TYPED into a real
      // <input> (typeRealInputsForCategoryOnce above) is present somewhere in the real Confirm
      // dispatch payload -- native input event -> liveNodeValueTracker (onNodeValueChange) ->
      // payloadFrom "node:<id>.value" resolution -> this exact request body. Scoped to fields
      // this scenario's own category actually has (some categories share fewer fields with a
      // given business action than others), so this never spuriously requires a field a
      // scenario's own dispatch never reads.
      const typedFieldsForThisCategory = realInputFieldsByCategory[scenario.category] ?? [];
      const confirmPayloadValues = Object.values(confirmPayload!).map((v) => String(v));
      assert(
        typedFieldsForThisCategory.some((nodeId) =>
          confirmPayloadValues.includes(`real-dom-value:${nodeId}`)
        ),
        `expected "${scenario.base}"'s real Confirm dispatch payload to carry at least one value ` +
          `actually typed into a real <input> for category "${scenario.category}" (payloadFrom ` +
          `round trip from native DOM input through liveNodeValueTracker), got ${
            JSON.stringify(confirmPayload)
          }`,
      );

      assertEquals(modalHtml(scenario.base), "", `expected "${scenario.base}_confirm_modal" to render empty again after a real Confirm settles and closes the Modal`);
    }
  } finally {
    render(null, container as unknown as Element);
    cleanup();
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});

/**
 * credential-management-ux-gaps SSOT/proof-closure round: the Modal-chain test above only
 * exercises admin_runtime_dispatch_override_wiring's Section-owned preview/Confirm shape. Two
 * OTHER real, non-Modal, single-click direct-dispatch action families in the same manifest also
 * read now-interactive Fields via payloadFrom and were never proven with real native DOM input:
 *
 * 1. credential_search_button (credential_search_section, always-mounted regardless of active
 *    category) — admin_runtime_dispatch_override_wiring, no Modal, one click posts directly.
 *    Proves credential_filter_status_input/provider_kind_input/required_by_bundle_input/
 *    expires_before_input/expires_after_input (the 5 of the 37 form_input/input fields under this
 *    section not exercised by the Modal-chain test, which never switches away from a category
 *    whose own CRUD form shares none of these fields).
 * 2. instance_settings category's validate/preview/apply/approve buttons — a DIFFERENT dispatch
 *    lane (external_instance_wiring / dispatchInstanceOperation, not admin_runtime), also no
 *    Modal. Proves instance_authority_key/operation_binding_key.
 *
 * A third instance_settings Field, template_file, is DELIBERATELY not proven to reach a dispatch
 * payload here, because it does not: json_import's own wiringLane (internal_instance_wiring) maps
 * to actionType "localStateMutation", and frontend/runtime/uiEventEffectRunner.ts's
 * resolveUiStateUpdateMutation resolves EVERY localStateMutation to the fixed literal `true` via
 * UI_STATE_UPDATE_OPEN_ACTIONS (see that map's own comment: "a localStateMutation trigger writes
 * true to its ui-local: target -- a flag write ... never a business-data value") — the SAME
 * fixed-boolean branch openModal/closeModal use, never consulting payloadFrom.template_file at
 * all. This is confirmed here as a real, pre-existing, out-of-scope gap (the seed's own
 * payloadFrom="template_file:node:template_file.value" is authored but structurally unreachable
 * for this actionType), the SAME area docs/projection_design/credential-management-projection-
 * design.md's instance_settings_admin_authoring_ui_pending known gap already tracks -- not
 * newly introduced and not fixed by this round. What IS proven for template_file is the WRITE
 * side only: a real native <input> keystroke still reaches liveNodeValueTracker (the same
 * onNodeValueChange path every other Field uses), and the button's own real local-state effect
 * (writing true to instance_settings_import_form.template_import_trigger) still fires on a real
 * click -- neither of those depends on the broken payloadFrom-to-localStateMutation path.
 */
Deno.test("production path: real native <input> values reach non-Modal direct-dispatch actions (credential_search_button, instance_settings validate/preview/apply/approve), and template_file's known dead payloadFrom is confirmed, not silently assumed fixed", async () => {
  const layoutNodes = await loadManifest092Fixture();
  const emission: Emission = {
    layoutId: "00000000-0000-0000-0000-0000000cd002",
    layoutNodes,
    packageId: "00000000-0000-0000-0000-0000000cd005",
    manifestId: "00000000-0000-0000-0000-000000000092",
  };

  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const dispatchedBodies: Record<string, unknown>[] = [];
  // deno-lint-ignore no-explicit-any
  (globalThis as any).fetch = (url: string, init?: RequestInit) => {
    const path = url.toString();
    if (path !== "/api/dispatch") {
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    }
    const body = JSON.parse(String(init?.body ?? "{}")) as Record<string, unknown>;
    dispatchedBodies.push(body);
    const requestPayload = body.payload as Record<string, unknown> | undefined;
    const targetRef = typeof requestPayload?.target_ref === "string" ? requestPayload.target_ref : undefined;
    const manifestMatch = targetRef ? /^manifest:([^:]+):/.exec(targetRef) : null;
    const manifestId = manifestMatch ? manifestMatch[1] : "00000000-0000-0000-0000-000000000000";
    return Promise.resolve(
      new Response(
        JSON.stringify({
          success: true,
          errors: [],
          emission: { manifestId, layoutId: "mock-non-modal-dispatch", layoutNodes: [] },
        }),
        { status: 200 },
      ),
    );
  };

  const localStore = createRuntimeLocalStateStore();
  const dispatcher = createProjectionStateDispatcher(toRunnerWiringNodes(layoutNodes), localStore);
  const tracker = createLiveNodeValueTracker();
  const { container, cleanup } = setupDom();

  try {
    function renderTree(): void {
      const specs = renderEmission(emission, defaultComponentRegistry, {
        localStateStore: dispatcher,
        payloadFromNodeValues: tracker.snapshot(),
        onNodeValueChange: (nodeId, value) => tracker.set(nodeId, value),
      });
      render(
        h(LayoutProjectionTree, { specs, layoutId: emission.layoutId, localStateStore: dispatcher }),
        container,
      );
    }
    localStore.subscribe(renderTree);
    renderTree();
    await flushUpdates();

    function typeIntoRealInput(nodeId: string, value: string): void {
      const inputEl = container.querySelector(
        `[data-node-id="${nodeId}"] input`,
      ) as unknown as { value: string; dispatchEvent: (e: Event) => boolean } | null;
      assert(inputEl, `expected a real <input> for Field "${nodeId}" in the currently-mounted DOM`);
      inputEl.value = value;
      inputEl.dispatchEvent(new Event("input", { bubbles: true }));
    }

    // --- 1. credential_search_button: always-mounted, admin_runtime, no Modal ---
    // credential_filter_active_input / credential_filter_record_kind_input are the pre-existing,
    // separately-tracked Select fields with no real option (see the out-of-scope note in the
    // Modal-chain test above) -- tracker.set() here for the SAME documented reason, not silently
    // reintroduced without explanation.
    tracker.set("credential_filter_active_input", "test-value");
    tracker.set("credential_filter_record_kind_input", "test-value");

    // credential_category_filter (the tabs) and credential_search_input are ALSO referenced by
    // credential_search_button's own payloadFrom (category/query) but were never exercised by the
    // Modal-chain test above (which never clicks the search button). A real click on the
    // already-active tab is a genuine native interaction (the tabs' own onSelect still fires and
    // captures the current key into liveNodeValueTracker, the SAME "re-click a no-op tab" pattern
    // the Modal-chain test's own category-switch proof already relies on) -- never dispatcher.set().
    clickTab(container, "external_api_credential");
    await flushUpdates();
    typeIntoRealInput("credential_search_input", "real-dom-value:credential_search_input");

    const searchFields: Record<string, string> = {
      credential_filter_status_input: "real-dom-value:credential_filter_status_input",
      credential_filter_provider_kind_input: "real-dom-value:credential_filter_provider_kind_input",
      credential_filter_required_by_bundle_input: "real-dom-value:credential_filter_required_by_bundle_input",
      credential_filter_expires_before_input: "real-dom-value:credential_filter_expires_before_input",
      credential_filter_expires_after_input: "real-dom-value:credential_filter_expires_after_input",
    };
    for (const [nodeId, value] of Object.entries(searchFields)) {
      typeIntoRealInput(nodeId, value);
    }

    const searchButton = container.querySelector(
      '[data-node-id="credential_search_button"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(searchButton, "expected a real credential_search_button in the DOM");
    const dispatchedBeforeSearch = dispatchedBodies.length;
    searchButton!.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    assertEquals(
      dispatchedBodies.length,
      dispatchedBeforeSearch + 1,
      "expected exactly one new /api/dispatch request from the real credential_search_button click",
    );
    const searchPayload = dispatchedBodies[dispatchedBodies.length - 1].payload as
      | Record<string, unknown>
      | undefined;
    assert(searchPayload, "expected the real search dispatch to carry a payload");
    assertEquals(
      searchPayload!.category,
      "external_api_credential",
      "expected the real search dispatch payload to carry the real tab click's own category value",
    );
    assertEquals(
      searchPayload!.query,
      "real-dom-value:credential_search_input",
      "expected the real search dispatch payload to carry the real typed credential_search_input value",
    );
    for (const [nodeId, value] of Object.entries(searchFields)) {
      assert(
        Object.values(searchPayload!).includes(value),
        `expected the real search dispatch payload to carry the value typed into "${nodeId}" ` +
          `(native <input> -> liveNodeValueTracker -> payloadFrom round trip), got ${
            JSON.stringify(searchPayload)
          }`,
      );
    }

    // --- 2. instance_settings category (external_instance_credential tab): validate/preview/
    // apply/approve, dispatchInstanceOperation lane, no Modal ---
    clickTab(container, "external_instance_credential");
    await flushUpdates();
    assertEquals(activeTabKey(container), "external_instance_credential");

    typeIntoRealInput("instance_authority_key", "real-dom-value:instance_authority_key");
    typeIntoRealInput("operation_binding_key", "real-dom-value:operation_binding_key");

    const instanceActions: { nodeId: string; expectedTargetRefPrefix: string }[] = [
      { nodeId: "validate", expectedTargetRefPrefix: "instance-port:db_instance_port:" },
      { nodeId: "preview", expectedTargetRefPrefix: "instance-port:runtime_instance_port:" },
      { nodeId: "apply", expectedTargetRefPrefix: "instance-port:db_instance_port:" },
      { nodeId: "approve", expectedTargetRefPrefix: "instance-port:runtime_instance_port:" },
    ];
    for (const action of instanceActions) {
      const buttonEl = container.querySelector(
        `[data-node-id="${action.nodeId}"] button`,
      ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
      assert(buttonEl, `expected a real "${action.nodeId}" button in the DOM`);
      const dispatchedBefore = dispatchedBodies.length;
      buttonEl!.dispatchEvent(new Event("click", { bubbles: true }));
      await flushUpdates();
      assertEquals(
        dispatchedBodies.length,
        dispatchedBefore + 1,
        `expected exactly one new /api/dispatch request from the real "${action.nodeId}" click`,
      );
      const body = dispatchedBodies[dispatchedBodies.length - 1];
      const payload = body.payload as Record<string, unknown> | undefined;
      assert(payload, `expected the real "${action.nodeId}" dispatch to carry a payload`);
      assert(
        typeof payload!.instance_target_ref === "string" &&
          (payload!.instance_target_ref as string).startsWith(action.expectedTargetRefPrefix),
        `expected "${action.nodeId}"'s instance_target_ref to start with "${action.expectedTargetRefPrefix}", got ${
          JSON.stringify(payload)
        }`,
      );
      const dispatchPayload = payload!.dispatch_payload as Record<string, unknown> | undefined;
      assert(dispatchPayload, `expected "${action.nodeId}"'s payload to carry a nested dispatch_payload`);
      assertEquals(
        dispatchPayload!.instance_authority_key,
        "real-dom-value:instance_authority_key",
        `expected "${action.nodeId}"'s dispatch_payload to carry the real typed instance_authority_key value`,
      );
      assertEquals(
        dispatchPayload!.operation_binding_key,
        "real-dom-value:operation_binding_key",
        `expected "${action.nodeId}"'s dispatch_payload to carry the real typed operation_binding_key value`,
      );
    }

    // --- 3. template_file / json_import: write side works, read side is a confirmed dead end ---
    typeIntoRealInput("template_file", "real-dom-value:template_file");
    assertEquals(
      tracker.snapshot()["template_file"],
      "real-dom-value:template_file",
      "expected the real native <input> keystroke on template_file to still reach liveNodeValueTracker",
    );

    const jsonImportButton = container.querySelector(
      '[data-node-id="json_import"] button',
    ) as unknown as { dispatchEvent: (e: Event) => boolean } | null;
    assert(jsonImportButton, "expected a real json_import button in the DOM");
    const dispatchedBeforeImport = dispatchedBodies.length;
    jsonImportButton!.dispatchEvent(new Event("click", { bubbles: true }));
    await flushUpdates();
    assertEquals(
      dispatchedBodies.length,
      dispatchedBeforeImport,
      "expected json_import's real click to fire NO /api/dispatch request at all (localStateMutation " +
        "never posts to the network) -- confirms template_file's authored payloadFrom is structurally " +
        "unreachable for this actionType, not merely untested",
    );
    assertEquals(
      dispatcher.get("instance_settings_import_form", "template_import_trigger"),
      true,
      "expected json_import's real click to still flip its own local-state flag to true " +
        "(the one effect localStateMutation actually performs, ignoring template_file's typed value)",
    );
  } finally {
    render(null, container as unknown as Element);
    cleanup();
    globalThis.fetch = originalFetch;
    schedulerTestOnly.resetCommandQueue();
  }
});
