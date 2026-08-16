using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// credential-management unified search (round 4/5: docs/design/admin-normal-surface-projection-
// seed-ssot.yaml surface_axes.admin.surfaces.credentials.seed_contract.component_tree's
// credential_category_filter/credential_search/credential_result_list, shared across all three
// categories). ONE dispatch entry (manifest 092's own dispatcher_mapping -- a manifest may declare
// only one), category-routed to the category's own existing search authority (never a new parallel
// search implementation): users -> AuthMasterRepository.ListUsersAsync, external_api_credential ->
// IExternalApiCredentialAdminRepository.SearchAsync, external_instance_credential ->
// IExternalInstanceCredentialAdminRepository.SearchAsync. See
// AdminRuntime.CredentialManagementSearch.cs.
public record CredentialManagementSearchRequestDto(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("query")] string? Query = null,
    [property: JsonPropertyName("recordKind")] string? RecordKind = null,
    [property: JsonPropertyName("providerKind")] string? ProviderKind = null,
    [property: JsonPropertyName("requiredByBundle")] string? RequiredByBundle = null,
    [property: JsonPropertyName("active")] bool? Active = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("expiresBefore")] DateTimeOffset? ExpiresBefore = null,
    [property: JsonPropertyName("expiresAfter")] DateTimeOffset? ExpiresAfter = null);

public static class CredentialManagementCategories
{
    public const string Users = "users";
    public const string ExternalApiCredential = "external_api_credential";
    public const string ExternalInstanceCredential = "external_instance_credential";
    public static readonly IReadOnlyList<string> All = new[] { Users, ExternalApiCredential, ExternalInstanceCredential };
}
