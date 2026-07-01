import {
  assert,
  assertEquals,
  assertExists,
  assertFalse,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import {
  __testOnly,
  queueAdminClientCommand,
} from "../runtime/frontendScheduler.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  projectionFromEmission,
  renderRuntimeComponents,
} from "../runtime/renderEmission.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";
import { renderRuntimeComponent } from "../runtime/runtimePrimitiveRenderer.ts";
import {
  buildLayoutPreviewRuntimeSpec,
  renderLayoutComponentPreview,
} from "../runtime/layoutComponentPreview.ts";
import { LayoutVisualAuditCanvas } from "../components/LayoutVisualAuditCanvas.tsx";
import type { DispatchResponse, Emission } from "../api/dispatch.ts";
import type { ProjectionDefinition } from "../runtime/projectionConstructor.ts";

const proofCatalogEntryCandidate = COMPONENT_CATALOG_ENTRIES.find((entry) =>
  entry.componentKey === "button.primitive"
);
assertExists(
  proofCatalogEntryCandidate,
  "test precondition: button.primitive must be catalog registered",
);
const proofCatalogEntry = proofCatalogEntryCandidate;

function makeProjectionDefinition(label: string): ProjectionDefinition {
  return {
    constructorKey: "admin-readback-button-projection",
    packageIds: ["pkg-admin-readback-proof"],
    outputKind: "component_projection",
    componentId: "cmp-admin-readback-button",
    componentDefinition: {
      componentId: "cmp-admin-readback-button",
      componentKey: proofCatalogEntry.componentKey,
      packageId: "pkg-admin-readback-proof",
      layoutId: "layout-admin-readback-proof",
      wiringId: "wiring-admin-readback-proof",
      component_kind: proofCatalogEntry.componentKind,
      semantic_role: proofCatalogEntry.semanticRole,
      visual_role: proofCatalogEntry.visualRole,
      parameter_schema: {
        required: ["label"],
        properties: {
          label: { type: "string" },
          variant: { type: "string" },
          disabled: { type: "boolean" },
          type: { type: "string" },
        },
      },
      default_parameters: {
        label,
        variant: "primary",
        disabled: false,
        type: "button",
      },
      event_binding: {
        click: {
          eventType: "click",
          actorOrSource: "admin_projection_update",
          payload: { source: "admin/**" },
        },
      },
      design: {
        className: "admin-readback-proof",
        state: "default",
      },
    },
  };
}

function makeAdminReadbackResponse(label: string): DispatchResponse {
  return {
    success: true,
    emission: {
      data: { label },
      componentIds: ["cmp-admin-readback-button"],
      projectionDefinition: makeProjectionDefinition(label),
    },
  };
}

async function runAdminRegistrationOrUpdateReadback(
  label: string,
): Promise<Emission> {
  const originalFetch = globalThis.fetch;
  const seenBodies: Record<string, unknown>[] = [];
  globalThis.fetch = (_url: string | URL | Request, init?: RequestInit) => {
    seenBodies.push(JSON.parse(String(init?.body ?? "{}")));
    return Promise.resolve(
      new Response(JSON.stringify(makeAdminReadbackResponse(label)), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };
  try {
    const response = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "component_registration",
      action: "register_or_update_projection_component",
      payload: {
        adminRoute: "/admin/ui-builder/components",
        componentKey: proofCatalogEntry.componentKey,
        componentKind: proofCatalogEntry.componentKind,
        projectionDefinition: makeProjectionDefinition(label),
      },
    });
    assertEquals(
      seenBodies.length,
      1,
      "admin operation must be the proof entrypoint",
    );
    assertEquals(seenBodies[0].target, "admin");
    assertEquals(seenBodies[0].triggerKind, "client");
    assertEquals(
      seenBodies[0].action,
      "register_or_update_projection_component",
    );
    assertExists(
      response.emission?.projectionDefinition,
      "admin response must read back projectionDefinition",
    );
    return response.emission;
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
}

Deno.test("admin projection expression E2E: admin update readback reaches catalog, runtime, layout preview, visual canvas, and user-facing DOM", async () => {
  const before = await runAdminRegistrationOrUpdateReadback(
    "Before admin update",
  );
  const after = await runAdminRegistrationOrUpdateReadback(
    "After admin update",
  );

  assertFalse(
    before.data?.label === after.data?.label,
    "update must produce a downstream contract diff",
  );

  for (const emission of [before, after]) {
    const definition = emission.projectionDefinition;
    assertExists(definition, "readback projectionDefinition is required");
    const componentDefinition = definition.componentDefinition;
    assertExists(
      componentDefinition,
      "readback componentDefinition is required",
    );

    const catalog = COMPONENT_CATALOG_ENTRIES.find((entry) =>
      entry.componentKey === componentDefinition.componentKey
    );
    assertExists(catalog, "componentKey must resolve to a catalog entry");
    assertEquals(componentDefinition.component_kind, catalog.componentKind);
    assertEquals(componentDefinition.semantic_role, catalog.semanticRole);
    assertEquals(componentDefinition.visual_role, catalog.visualRole);
    assertEquals(catalog.capabilityTags.includes("emits_event"), true);
    assertEquals(
      catalog.capabilityTags.includes("requires_event_binding"),
      true,
    );
    assertEquals(catalog.capabilityTags.includes("accepts_design"), true);

    const projectionResult = projectionFromEmission(emission, definition);
    assertExists(projectionResult.projection, projectionResult.error);
    assertEquals(projectionResult.projection.kind, "component_projection");
    if (projectionResult.projection.kind !== "component_projection") {
      throw new Error("projection must be component_projection");
    }
    const hub = projectionResult.projection.componentDataHub;
    assertEquals(hub.componentKey, catalog.componentKey);
    assertEquals(
      hub.eventBinding.click,
      componentDefinition.event_binding?.click,
    );
    assertEquals(hub.design?.className, "admin-readback-proof");
    assertEquals(hub.props.label, emission.data?.label);

    const adapted = adaptComponentDataHub(hub);
    assertEquals(adapted.ok, true, adapted.ok ? undefined : adapted.error);
    if (!adapted.ok) return;
    const rendered = renderRuntimeComponent(adapted.value);
    assertEquals(rendered.ok, true, rendered.ok ? undefined : rendered.error);
    if (!rendered.ok) return;

    const renderedComponents = renderRuntimeComponents([hub]);
    assertEquals(renderedComponents.length, 1);
    assertFalse(
      renderedComponents[0].componentType === "error",
      "fallback/error component must not be accepted",
    );
    const runtimeHtml = renderToString(rendered.node);
    assert(
      runtimeHtml.includes(String(emission.data?.label)),
      "runtime DOM must contain user-facing label",
    );
    assert(
      runtimeHtml.includes("<button"),
      "runtime DOM must contain user-facing button element",
    );
    assertFalse(runtimeHtml.includes("ComponentRegistry: unknown"));

    const previewSpec = buildLayoutPreviewRuntimeSpec({
      componentKey: catalog.componentKey,
      componentKind: catalog.componentKind,
      componentId: hub.componentId,
    });
    assertEquals(
      previewSpec.ok,
      true,
      previewSpec.ok ? undefined : previewSpec.reason,
    );
    const preview = renderLayoutComponentPreview({
      componentKey: catalog.componentKey,
      componentKind: catalog.componentKind,
      componentId: hub.componentId,
    });
    assertEquals(preview.ok, true, preview.ok ? undefined : preview.reason);

    const canvasHtml = renderToString(h(LayoutVisualAuditCanvas, {
      title: "Admin projection visual audit",
      nodes: [{
        nodeId: "node-admin-readback-button",
        componentKey: catalog.componentKey,
        componentKind: catalog.componentKind,
        componentId: hub.componentId,
        nodeKind: "catalog_component",
        x: 16,
        y: 24,
        width: 180,
        height: 64,
      }],
    }));
    assert(canvasHtml.includes("Admin projection visual audit"));
    assert(
      canvasHtml.includes("1 ノード"),
      "visual audit canvas must not be empty",
    );
    assert(
      canvasHtml.includes("button.primitive preview"),
      "visual audit canvas must render catalog component preview",
    );
    assertFalse(
      canvasHtml.includes("配置ノードがありません"),
      "empty canvas must not be accepted",
    );
    assertFalse(
      canvasHtml.includes("部品が未接続"),
      "draft-only fallback must not be accepted",
    );
  }
});
