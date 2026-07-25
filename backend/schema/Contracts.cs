using System.Text.Json;

namespace Topolactor.Schema;

/// <summary>
/// Inbound DTO from the caller/frontend. Represents a user operation request.
/// trigger_kind identifies the trigger origin (cron|hook|client) per SSOT minimal_event_shape.
/// role carries the JWT token claim for manifest axis resolution.
/// </summary>
public record EndpointRequestDto(
    string? OperationType,
    string? Target,
    string? Layer,
    string? Action,
    Guid? IdOrHubId,
    JsonElement? Payload,
    Dictionary<string, string>? Context,
    string? TriggerKind = null,
    string? Role = null
);

/// <summary>
/// Outbound DTO returned to the caller. Contains either an Emission or validation errors.
/// </summary>
public record EndpointResponseDto(
    bool Success,
    Emission? Emission,
    IReadOnlyList<ValidationError> Errors
);

/// <summary>
/// Internal runtime concept. Public only for C# accessibility consistency.
/// Must not be returned to the frontend or exposed through EndpointResponseDto.
/// Derived from EndpointRequestDto after input mapping.
/// Context route fields (ContextSessionId, ContextUserId, ContextTokenIds, ContextRecordId)
/// are extracted from EndpointRequestDto.Context for use by the recommendation resolver.
/// </summary>
public record OperationVector(
    string? Target,
    string? Layer,
    string? Action,
    string? AttractorKey,
    string? UserRole,
    JsonElement? Payload,
    string? RequestedProjection,
    // Context route recommendation fields — nullable, no guard enforcement
    string? ContextSessionId = null,
    string? ContextUserId = null,
    string? ContextTokenIds = null,     // comma-separated Guid list
    string? ContextRecordId = null,
    Guid? IdOrHubId = null,
    // state_pressure lane: explicit enum transition observation (dispatch context keys)
    string? ContextEnumGroupId = null,
    int? ContextPrevEnumIndex = null,
    int? ContextNextEnumIndex = null,
    // trigger_kind from SSOT minimal_event_shape: cron | hook | client
    string? TriggerKind = null,
    // Server-verified JWT identity, sourced only from DispatchAuthContext's reserved context keys
    // (authenticated_user_id / authenticated_roles). Never sourced from client-supplied context —
    // this is the non-spoofable audit-actor / self-operation-subject identity.
    string? AuthenticatedUserId = null,
    string? AuthenticatedRole = null
);

/// <summary>
/// A single layout projection node in the Emission, derived from layout_patch_json.nodes[].
/// Carries the full structural and positional fields authored in the UI builder canvas.
/// ComponentId comes from nodes[].componentId (not positional structure_maps.component_ids).
/// NodeKind is "catalog_component" | "structural_html" | "structural_node" | "unresolved_gap".
/// structural_html nodes carry HtmlTag; catalog_component nodes carry ComponentKey.
/// structural_node nodes (Category/Section/Form/Workflow/Validation from
/// components_layout_design.layout_schema_json.records[] — the structural authority tree)
/// carry RecordType/Label and never a ComponentId/ComponentKind.
/// unresolved_gap nodes (topology_ui_unresolved from the same structural authority tree) carry
/// RecordType/Label/KnownGapRefs and never a ComponentId/ComponentKind — frontend renderEmission()
/// always projects them as an explicit error, never a resolvable component.
/// ParentNodeId establishes the DOM nesting tree; OrderIndex drives sibling render order.
/// X/Y/Width/Height are canvas geometry for DOM style projection.
/// LayoutClassRefs are SSOT topology-layout-class vocabulary refs for className resolution.
/// WiringId/WiringKey/WiringKind/TargetSurface/TargetRef carry the full admin-configured
/// wiring spec from ui_wiring_registry — used by the frontend to build the executable dispatch spec.
/// </summary>
public record LayoutNode(
    string NodeId,
    string? NodeKind,
    string? HtmlTag,
    string? ComponentKey,
    string? ComponentId,
    string? ParentNodeId,
    string? SlotKey,
    int OrderIndex,
    double X,
    double Y,
    object? Width,
    object? Height,
    IReadOnlyList<string>? LayoutClassRefs = null,
    /// <summary>Component kind from ui_component_registry — required for runtime rendering of catalog_component nodes.</summary>
    string? ComponentKind = null,
    /// <summary>Runtime dispatch action derived from ui_wiring_registry.wiring_kind via tensor JOIN. Null when no wiring configured.</summary>
    string? RuntimeDispatchAction = null,
    // Wiring spec from ui_wiring_registry — carried for frontend dispatch spec construction.
    string? WiringId = null,
    string? WiringKey = null,
    string? WiringKind = null,
    string? TargetSurface = null,
    string? TargetRef = null,
    /// <summary>Node-local props override JSON string authored in UI Builder. Null when not set. renderEmission merges this over default props; invalid JSON → explicit error spec.</summary>
    string? PropsJson = null,
    /// <summary>Node-local state JSON string authored in UI Builder (e.g. open:bool for modal/drawer). Null when not set. renderEmission merges into props.data; invalid JSON → explicit error spec.</summary>
    string? StateJson = null,
    /// <summary>Array prop bindings authored in UI Builder. Null when not set. Serialized as JSON object in emission. renderEmission resolves from emission.data after propsJson/stateJson.</summary>
    JsonElement? PropBindings = null,
    /// <summary>Canonical runtime UI interactions from layout_patch_json.nodes[].runtimeInteractions. Legacy propsJson.eventWirings is fallback only.</summary>
    JsonElement? RuntimeInteractions = null,
    /// <summary>
    /// Node-local admin_runtime dispatch payload binding from
    /// layout_patch_json.nodes[].dispatchPayloadFromByTrigger — { trigger: { field: source } }.
    /// Data-only: supplies payloadFrom fields for this SAME node's admin_runtime dispatch
    /// (WiringKind="admin_runtime" + TargetRef); carries no action authority and is independent
    /// of RuntimeInteractions/actionType. Null when not authored.
    /// </summary>
    JsonElement? DispatchPayloadFromByTrigger = null,
    /// <summary>
    /// Structural-node semantic type (topology_ui_category/topology_ui_section/topology_ui_form/
    /// topology_ui_workflow/topology_ui_validation) when NodeKind is "structural_node" — sourced
    /// from components_layout_design.layout_schema_json.records[], the structural authority tree.
    /// Null for "catalog_component"/"structural_html" nodes.
    /// </summary>
    string? RecordType = null,
    /// <summary>
    /// Authored display label — present for every schema-composed node (structural_node,
    /// catalog_component, and unresolved_gap alike). Null for tensor-only nodes composed outside
    /// the layout-schema structural authority path.
    /// </summary>
    string? Label = null,
    /// <summary>Authored known-gap references for an unresolved_gap node. Null for every other NodeKind.</summary>
    IReadOnlyList<string>? KnownGapRefs = null
);

/// <summary>
/// Internal runtime concept. Public only for C# accessibility consistency.
/// Must not be returned to the frontend, exposed through EndpointResponseDto,
/// or persisted as a business fact.
/// Holds intermediate resolved state as the runtime progresses through the pipeline.
/// ContextRouteRecommendation is populated by context_route_recommendation_resolve
/// and forwarded to EmissionBuilder.
/// LayoutId is the optional admin-authored layout reference from structure_maps.layout_id,
/// forwarded to EmissionBuilder for inclusion in Emission.
/// LayoutNodes carries tensor-derived slot placement when LayoutId is set and tensor rows exist.
/// </summary>
public record RuntimeWorkingShape(
    OperationVector? Vector,
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    object? PackageDef,
    object? SchemaDef,
    JsonElement? ResolvedData,
    IReadOnlyList<ValidationError>? Errors,
    IReadOnlyList<RuntimeJumpEvent>? JumpEvents = null,
    // Raw JSONB from structure_maps.state_policy — used by ContextRouteRecommendationResolver
    // to resolve a scoped context_route_policy_ref instead of the global default_policy.
    string? StructureMapStatePolicyJson = null,
    ContextRouteRecommendationResult? ContextRouteRecommendation = null,
    RecommendNavigationProjectionSpec? RecommendNavigationProjection = null,
    string? LayoutId = null,
    IReadOnlyList<LayoutNode>? LayoutNodes = null,
    // Frontend-local calculation bindings extracted verbatim from layout_patch_json.calculationBindings[].
    // Backend does not evaluate these — they are forwarded raw for client-side computation.
    JsonElement? CalculationBindings = null
);

/// <summary>
/// Validated output returned in the response. Contains resolved identifiers and data.
/// ContextRouteRecommendation carries next operation and token candidates derived
/// from the context route recommendation runtime. Status is always explicit —
/// InsufficientHistory when not enough history exists, never silently null.
/// RecommendNavigationProjection is the backend-resolved render-only child island
/// spec under the main projection island; frontend must not derive lane mixing,
/// topology promotion, or executable wiring from raw candidates.
/// ProjectionDefinition carries the projection_constructor_mapping from the resolved manifest
/// topology entry. Frontend uses this to call setProjectionDefinition on the projection runtime
/// before processing SSE projection events. Null when no manifest is configured or no
/// projection_constructor_mapping entry exists in the manifest topology.
/// LayoutId is the optional admin-authored layout reference from structure_maps.layout_id.
/// Null when no layout is bound to the resolved structure map entry.
/// LayoutNodes carries the full layout projection spec from layout_patch_json.nodes[],
/// ordered by OrderIndex. Present when LayoutId is set and tensor rows contain nodes.
/// Absent (not null — absent) when no layout is bound. Frontend must not silently fall back
/// to flat componentIds rendering when LayoutId is present but LayoutNodes is absent.
/// Each LayoutNode includes NodeId, NodeKind, HtmlTag, ComponentKey, ComponentId,
/// ParentNodeId, SlotKey, OrderIndex, X, Y, Width, Height, and LayoutClassRefs.
/// </summary>
public record Emission(
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    IReadOnlyList<string>? ComponentIds,
    JsonElement? Data,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<RuntimeJumpEvent>? JumpEvents = null,
    ContextRouteRecommendationResult? ContextRouteRecommendation = null,
    RecommendNavigationProjectionSpec? RecommendNavigationProjection = null,
    JsonElement? ProjectionDefinition = null,
    IReadOnlyList<HubNavigationSequenceItemDto>? NavigationSequence = null,
    string? LayoutId = null,
    IReadOnlyList<LayoutNode>? LayoutNodes = null,
    // Frontend-local calculation bindings verbatim from layout_patch_json.calculationBindings[].
    // Null when absent. Never evaluated server-side.
    JsonElement? CalculationBindings = null,
    // Identity of the resolved topology_manifest for this dispatch — the "current topology
    // phase" a frontend nav surface needs to know which manifest is currently loaded when
    // rendering NavigationSequence. Set by ManifestDispatcher for any manifest-resolved
    // dispatch, regardless of runtime_destination. Null when no manifest was resolved
    // (e.g. dev bypass path).
    string? ManifestId = null
);

/// <summary>SSOT runtime_jump_event_contract: scope, from, to, planned, reason.
/// FromAddress = address at event time (currentAddress at jump origin).
/// ToAddress = destination (0 for route_missing sentinel; plannedAddress for user_action).
/// PlannedAddress = intended destination for user_action jumps; 0 for route_missing.
/// JSON shape: scope/from/to/planned/reason (unchanged).</summary>
public record RuntimeJumpEvent(
    string Scope,
    [property: System.Text.Json.Serialization.JsonPropertyName("from")]
    int FromAddress,
    [property: System.Text.Json.Serialization.JsonPropertyName("to")]
    int ToAddress,
    [property: System.Text.Json.Serialization.JsonPropertyName("planned")]
    int PlannedAddress,
    string Reason
);

/// <summary>
/// Result of attractor resolution: maps an attractor key to its structure map, package, and schema.
/// </summary>
public record AttractorResult(
    string AttractorKey,
    string StructureMapId,
    Guid PackageId,
    Guid SchemaId
);

/// <summary>
/// A structured validation error returned when resolution fails at any pipeline stage.
/// </summary>
public record ValidationError(
    string Code,
    string Message
);


/// <summary>
/// Existing-system change intake contract (external hook boundary).
/// table_name is the registry resolution key candidate.
/// changed_data_jsonb or diff_jsonb must be present.
/// Role is taken from JWT claim at the endpoint — not from request body.
/// </summary>
public record ExistingSystemChangeIntakeRequestDto(
    string? TableName,
    string? RowId,
    string? Operation,
    JsonElement? ChangedDataJsonb,
    JsonElement? DiffJsonb,
    string? Actor = null,
    string? Source = null,
    DateTimeOffset? OccurredAt = null
);

public record ExistingSystemChangeIntakeResponseDto(
    bool Accepted,
    string? QueueStatus,
    IReadOnlyList<ValidationError> Errors
);

/// <summary>
/// A content_bundle entity draft for promote staging (not /demo preview).
/// Source: topology.content_entity_drafts WHERE status='draft'.
/// Label is extracted from entity_jsonb (label/name/title fields), fallback to "Draft {id-prefix}".
/// </summary>
public record EntityDraftListItemDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("draftId")] string DraftId,
    [property: System.Text.Json.Serialization.JsonPropertyName("label")] string Label,
    [property: System.Text.Json.Serialization.JsonPropertyName("hubId")] string HubId,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

/// <summary>Request body for POST /draft-preview/preview.</summary>
public record DraftPreviewRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("layoutId")] string? LayoutId
);

/// <summary>Tensor row context for a layout_id — used by draft preview projection.</summary>
public record LayoutTensorContextDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("packageId")] Guid PackageId,
    [property: System.Text.Json.Serialization.JsonPropertyName("routeKey")] string RouteKey,
    [property: System.Text.Json.Serialization.JsonPropertyName("rootLayoutClassRefs")]
    IReadOnlyList<string> RootLayoutClassRefs
);
