import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { constructProjection, type ProjectionDefinition } from "../runtime/projectionConstructor.ts";

Deno.test("constructProjection: form_inputs behavior remains stable", () => {
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "form_inputs",
    fieldDefs: [{ key: "name", label: "Name", kind: "text", required: true }],
  };

  const result = constructProjection({ name: "Alice" }, def);
  assertExists(result.projection);
  if (!result.projection || result.projection.kind !== "form_inputs") throw new Error("unexpected projection kind");
  assertEquals(result.projection.fields[0].value, "Alice");
});

Deno.test("constructProjection: component_projection creates ComponentDataHub without frontend topology/sql judgment", () => {
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp-1",
    projectionOverrides: { label: "Override" },
    componentDefinition: {
      componentId: "cmp-1",
      componentKey: "button.primitive",
      component_kind: "action/button",
      parameter_schema: {
        required: ["label"],
        properties: { label: { type: "string" }, disabled: { type: "boolean" } },
      },
      default_parameters: { label: "Default", disabled: false },
      event_binding: { click: "emit.button.click" },
    },
  };

  const result = constructProjection({ disabled: true }, def);
  assertExists(result.projection);
  if (!result.projection || result.projection.kind !== "component_projection") throw new Error("unexpected projection kind");
  assertEquals(result.projection.props.label, "Override");
  assertEquals(result.projection.props.disabled, true);
  assertEquals(result.projection.componentDataHub.eventBinding.click, "emit.button.click");
});

Deno.test("constructProjection: unknown component_kind returns explicit error", () => {
  const result = constructProjection({}, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp-2", componentDefinition: { componentId: "cmp-2", component_kind: "unknown/kind" } });
  assertEquals(result.error?.startsWith("PROJECTION_CONSTRUCTOR_UNSUPPORTED_COMPONENT_KIND"), true);
});

Deno.test("constructProjection: required parameter missing returns explicit error", () => {
  const result = constructProjection({}, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp-3", componentDefinition: { componentId: "cmp-3", component_kind: "action/button", parameter_schema: { required: ["label"] } } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_SCHEMA_REQUIRED_MISSING: label");
});

Deno.test("constructProjection: event_binding non-object returns explicit error", () => {
  const result = constructProjection({}, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp", componentDefinition: { componentId: "cmp", component_kind: "display/card", event_binding: "bad" as unknown as Record<string, unknown> } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_INVALID_EVENT_BINDING: EVENT_BINDING must be object");
});

Deno.test("constructProjection: default_parameters non-object returns explicit error", () => {
  const result = constructProjection({}, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp", componentDefinition: { componentId: "cmp", component_kind: "display/card", default_parameters: [] as unknown as Record<string, unknown> } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_INVALID_DEFAULT_PARAMETERS: DEFAULT_PARAMETERS must be object");
});

Deno.test("constructProjection: projectionOverrides non-object returns explicit error", () => {
  const result = constructProjection({}, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp", projectionOverrides: "x" as unknown as Record<string, unknown>, componentDefinition: { componentId: "cmp", component_kind: "display/card" } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_INVALID_PROJECTION_OVERRIDES: PROJECTION_OVERRIDES must be object");
});

Deno.test("constructProjection: disabled string is explicit error without coercion", () => {
  const result = constructProjection({ label: "x", disabled: "false" }, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp", componentDefinition: { componentId: "cmp", component_kind: "action/button" } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_DISABLED: disabled must be boolean");
});

Deno.test("constructProjection: schema type mismatch is explicit error", () => {
  const result = constructProjection({ label: "x", disabled: "false" }, { constructorKey: "k", packageIds: ["p"], outputKind: "component_projection", componentId: "cmp", componentDefinition: { componentId: "cmp", component_kind: "action/button", parameter_schema: { properties: { disabled: { type: "boolean" } } } } });
  assertEquals(result.error, "PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: disabled expected boolean");
});
