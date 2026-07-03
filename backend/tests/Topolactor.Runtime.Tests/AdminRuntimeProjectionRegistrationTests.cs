using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeProjectionRegistrationTests
{
    [Fact]
    public async Task ExecuteDataAsync_ComponentRegistrationRegisterOrUpdate_ReadsBackProjectionDefinition()
    {
        var repository = new StubUiTopologyRepository();
        var runtime = CreateRuntime(repository);
        var projectionDefinition = ProjectionDefinition("Before admin update");
        var vector = new OperationVector("admin", "component_registration", "register_or_update_projection_component", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                componentKey = "button.primitive",
                sourcePath = "frontend/components/Button.tsx",
                componentKind = "action/button",
                routeKey = "admin:projection-expression-proof",
                data = new { label = "Before admin update" },
                projectionDefinition
            }), null, TriggerKind: "client");

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("component_registration:register_or_update_projection_component", data!.Value.GetProperty("operation").GetString());
        Assert.True(data.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("button.primitive", data.Value.GetProperty("bucketItem").GetProperty("componentKey").GetString());
        Assert.Equal("action/button", data.Value.GetProperty("bucketItem").GetProperty("componentKind").GetString());
        Assert.Equal("cmp-admin-readback-button", data.Value.GetProperty("projectionDefinition").GetProperty("componentId").GetString());
        Assert.Equal("Before admin update", data.Value.GetProperty("data").GetProperty("label").GetString());
        Assert.Contains("cmp-admin-readback-button", data.Value.GetProperty("componentIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("button.primitive", repository.LastComponentKey);
        Assert.Contains("projectionDefinition", repository.LastMetadataJson);

        var updateVector = vector with
        {
            Payload = JsonSerializer.SerializeToElement(new
            {
                componentKey = "button.primitive",
                sourcePath = "frontend/components/Button.tsx",
                componentKind = "action/button",
                routeKey = "admin:projection-expression-proof",
                data = new { label = "After admin update" },
                projectionDefinition = ProjectionDefinition("After admin update")
            })
        };
        var (updatedData, updatedError) = await runtime.ExecuteDataAsync(updateVector);

        Assert.Null(updatedError);
        Assert.NotNull(updatedData);
        Assert.Equal("After admin update", updatedData!.Value.GetProperty("data").GetProperty("label").GetString());
        Assert.Equal("After admin update", updatedData.Value.GetProperty("projectionDefinition").GetProperty("componentDefinition").GetProperty("default_parameters").GetProperty("label").GetString());
        Assert.Equal(2, repository.RegisterOrUpdateCalls);
    }

    [Fact]
    public async Task DispatchAdapter_ComponentRegistration_CopiesReadbackProjectionDefinitionToEmissionContract()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository());
        var adapter = new AdminRuntimeDispatchAdapter(runtime, new OperationVectorResolver());
        var projectionDefinition = ProjectionDefinition("After admin update");
        var response = await adapter.ExecuteAsync(new EndpointRequestDto(
            OperationType: "admin",
            Target: "admin",
            Layer: "component_registration",
            Action: "register_or_update_projection_component",
            IdOrHubId: null,
            Payload: JsonSerializer.SerializeToElement(new
            {
                componentKey = "button.primitive",
                sourcePath = "frontend/components/Button.tsx",
                componentKind = "action/button",
                routeKey = "admin:projection-expression-proof",
                data = new { label = "After admin update" },
                projectionDefinition
            }),
            Context: null,
            TriggerKind: "client",
            Role: null), null);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.True(response.Emission!.ProjectionDefinition.HasValue);
        Assert.Equal("cmp-admin-readback-button", response.Emission.ProjectionDefinition!.Value.GetProperty("componentId").GetString());
        Assert.Contains("cmp-admin-readback-button", response.Emission.ComponentIds ?? []);
        Assert.Equal("After admin update", response.Emission.Data!.Value.GetProperty("data").GetProperty("label").GetString());
    }

    [Fact]
    public async Task ExecuteDataAsync_ComponentRegistrationRejectsMismatchedProjectionDefinition()
    {
        var runtime = CreateRuntime(new StubUiTopologyRepository());
        var vector = new OperationVector("admin", "component_registration", "register_or_update_projection_component", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                componentKey = "button.primitive",
                sourcePath = "frontend/components/Button.tsx",
                componentKind = "action/button",
                projectionDefinition = ProjectionDefinition("Bad", componentKey: "input.primitive")
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("COMPONENT_KEY_MISMATCH", error!.Code);
    }

    private static object ProjectionDefinition(string label, string componentKey = "button.primitive") => new
    {
        constructorKey = "admin-readback-button-projection",
        packageIds = new[] { "pkg-admin-readback-proof" },
        outputKind = "component_projection",
        componentId = "cmp-admin-readback-button",
        componentDefinition = new
        {
            componentId = "cmp-admin-readback-button",
            componentKey,
            packageId = "pkg-admin-readback-proof",
            layoutId = "layout-admin-readback-proof",
            wiringId = "wiring-admin-readback-proof",
            component_kind = "action/button",
            semantic_role = "action",
            visual_role = "button",
            parameter_schema = new
            {
                required = new[] { "label" },
                properties = new { label = new { type = "string" } }
            },
            default_parameters = new { label, variant = "primary", disabled = false, type = "button" },
            event_binding = new { click = new { eventType = "click", actorOrSource = "admin_projection_update", payload = new { source = "admin/**" } } },
            design = new { className = "admin-readback-proof", state = "default" }
        }
    };

    private static AdminRuntime CreateRuntime(UiTopologyRepository uiRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, null);
    }

    private sealed class StubUiTopologyRepository : UiTopologyRepository
    {
        public string LastComponentKey { get; private set; } = "";
        public string LastMetadataJson { get; private set; } = "";
        public int RegisterOrUpdateCalls { get; private set; }

        public StubUiTopologyRepository() : base(NullLogger<UiTopologyRepository>.Instance, "test-double") { }

        public override Task<UiComponentBucketCreateResult> RegisterOrUpdateProjectionComponentAsync(
            string componentKey,
            string sourcePath,
            string componentKind,
            string? metadataJson = null,
            CancellationToken ct = default)
        {
            LastComponentKey = componentKey;
            LastMetadataJson = metadataJson ?? "";
            RegisterOrUpdateCalls++;
            return Task.FromResult(new UiComponentBucketCreateResult(
                UiComponentBucketCreateCode.Success,
                new UiComponentBucketRecord(Guid.NewGuid(), componentKey, sourcePath, componentKind, "bucketed")));
        }
    }
}
