using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

// ---------------------------------------------------------------------------
// AdminRuntime — credential-management: unified category-driven search
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
//  credentials.seed_contract.component_tree: credential_category_filter/credential_search/
//  credential_result_list are shared across all three categories -- one search surface, not three
//  independent ones).
//
// Entry: AdminRuntime.ExecuteDataAsync layer=credential_management action=search
//
// Round 4/5 design: manifest 00000000-0000-0000-0000-000000000092 (credential-management) may
// declare only ONE dispatcher_mapping entry (ManifestTopologyValidator), so exactly one category's
// search could ever get the same-manifest Emission-adoption live result-list binding
// (ProjectionShell.tsx handleRuntimeDispatchResult's expectedManifestId===adoptedManifestId branch
// -- the same mechanism admin-enum's enum_table/list_groups pair uses) if search stayed
// per-category. Unifying all three categories behind ONE credential_management:search action
// hosted on 092's own dispatcher_mapping gives every category that same live binding through the
// credential_result_list table's propBindings.rows.source="emission.data.records" /
// columns.source="emission.data.activeColumns" (activeColumnsToTableColumns transform -- an
// EXISTING propBindings transform, not a new one).
//
// The category switch below selects WHICH EXISTING read authority to call -- never a new parallel
// search implementation: users -> AuthMasterRepository.ListUsersAsync (the SAME method
// auth_users:list/auth_users:search already call), external_api_credential ->
// IExternalApiCredentialAdminRepository.SearchAsync (Round 3/4, unchanged), external_instance_credential
// -> IExternalInstanceCredentialAdminRepository.SearchAsync (this round). This is a structural
// selector over WHICH REPOSITORY METHOD to call (three incompatible method signatures, physically
// unavoidable), never a business-logic branch on provider_kind/required_by_bundle/status VALUES --
// those stay inside each repository's own generic, data-defined execution, unchanged.
// ---------------------------------------------------------------------------

public partial class AdminRuntime
{
    private async Task<(JsonElement? data, ValidationError? error)>
        DataCredentialManagementSearchAsync(OperationVector vector, CancellationToken ct)
    {
        var request = DeserializePayload<CredentialManagementSearchRequestDto>(vector.Payload);
        if (request is null || !CredentialManagementCategories.All.Contains(request.Category))
            return (null, new ValidationError(
                "CREDENTIAL_MANAGEMENT_CATEGORY_INVALID",
                $"category must be one of: {string.Join(", ", CredentialManagementCategories.All)}."));

        return request.Category switch
        {
            CredentialManagementCategories.Users => await SearchUsersAsync(request, ct),
            CredentialManagementCategories.ExternalApiCredential => await SearchExternalApiCredentialAsync(request, ct),
            CredentialManagementCategories.ExternalInstanceCredential => await SearchExternalInstanceCredentialAsync(request, ct),
            _ => (null, new ValidationError("CREDENTIAL_MANAGEMENT_CATEGORY_INVALID", "Unknown category.")),
        };
    }

    private static readonly string[] UsersActiveColumns = ["username", "status", "active", "approve", "role", "updatedAt"];
    private static readonly string[] ExternalApiCredentialActiveColumns =
        ["recordKind", "providerKind", "requiredByBundle", "referenceKey", "active", "expiresAt", "updatedAt"];
    private static readonly string[] ExternalInstanceCredentialActiveColumns =
        ["recordKind", "instanceAuthorityKey", "providerKind", "requiredByBundle", "referenceKey", "active", "expiresAt", "updatedAt"];

    private async Task<(JsonElement? data, ValidationError? error)> SearchUsersAsync(
        CredentialManagementSearchRequestDto request, CancellationToken ct)
    {
        if (_authMasterRepository is null)
            return (null, new ValidationError("AUTH_USERS_NOT_AVAILABLE", "Auth master repository is not configured."));

        // Reuses the SAME canonical read authority auth_users:list/search already call -- filtering
        // by status/active beyond the underlying query text search is applied in-memory over the
        // already-secret-free roster, not a second SQL account lookup implementation.
        var users = await _authMasterRepository.ListUsersAsync(request.Query, ct);
        IEnumerable<object> rows = users;
        if (request.Status is not null)
            rows = users.Where(u => string.Equals(u.Status, request.Status, StringComparison.Ordinal));
        if (request.Active is not null)
            rows = ((IEnumerable<Schema.AuthUserRosterDto>)rows).Where(u => u.Active == request.Active);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            category = CredentialManagementCategories.Users,
            records = rows.ToList(),
            activeColumns = UsersActiveColumns,
        }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> SearchExternalApiCredentialAsync(
        CredentialManagementSearchRequestDto request, CancellationToken ct)
    {
        if (_externalApiCredentialAdminRepository is null)
            return (null, ExternalApiCredentialNotAvailable());
        if (request.RecordKind is not null && !Repository.ExternalApiCredentialRecordKinds.All.Contains(request.RecordKind))
            return (null, new ValidationError(
                "EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID", $"Unknown recordKind '{request.RecordKind}'."));

        var results = await _externalApiCredentialAdminRepository.SearchAsync(
            request.Query, request.RecordKind, request.ProviderKind, request.RequiredByBundle, request.Active,
            request.ExpiresBefore, request.ExpiresAfter, ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            category = CredentialManagementCategories.ExternalApiCredential,
            records = results.Select(ToProjection).ToList(),
            activeColumns = ExternalApiCredentialActiveColumns,
        }), null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> SearchExternalInstanceCredentialAsync(
        CredentialManagementSearchRequestDto request, CancellationToken ct)
    {
        if (_externalInstanceCredentialAdminRepository is null)
            return (null, ExternalInstanceCredentialNotAvailable());
        if (request.RecordKind is not null && !Repository.ExternalInstanceCredentialRecordKinds.All.Contains(request.RecordKind))
            return (null, new ValidationError(
                "EXTERNAL_INSTANCE_CREDENTIAL_RECORD_KIND_INVALID", $"Unknown recordKind '{request.RecordKind}'."));

        var results = await _externalInstanceCredentialAdminRepository.SearchAsync(
            request.Query, request.RecordKind, request.ProviderKind, request.RequiredByBundle, request.Active,
            request.ExpiresBefore, request.ExpiresAfter, ct);

        return (JsonSerializer.SerializeToElement(new
        {
            ok = true,
            category = CredentialManagementCategories.ExternalInstanceCredential,
            records = results.Select(ToProjection).ToList(),
            activeColumns = ExternalInstanceCredentialActiveColumns,
        }), null);
    }
}
