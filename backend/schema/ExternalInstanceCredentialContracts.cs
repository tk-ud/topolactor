using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// credential-management external_instance_credential category (docs/design/admin-normal-surface-
// projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.categories.
// external_instance_credential). Request payload shapes only -- the secret-free response
// projection is built directly from Topolactor.Repository.ExternalInstanceCredentialRecord in
// AdminRuntime.ExternalInstanceCredential.cs. No plaintext/secret field exists anywhere in this
// file: this category never creates or rotates vault secrets, only references an existing
// external_credential_vault row by reference_key (mutation_boundary: "no new credential table or
// plane is defined here").

public record ExternalInstanceCredentialGetRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId);

public record ExternalInstanceCredentialCreateRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("instanceAuthorityKey")] string InstanceAuthorityKey,
    [property: JsonPropertyName("providerKind")] string ProviderKind,
    [property: JsonPropertyName("requiredByBundle")] string RequiredByBundle,
    [property: JsonPropertyName("referenceKey")] string ReferenceKey);

public record ExternalInstanceCredentialUpdateRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId,
    [property: JsonPropertyName("providerKind")] string? ProviderKind = null,
    [property: JsonPropertyName("requiredByBundle")] string? RequiredByBundle = null,
    [property: JsonPropertyName("referenceKey")] string? ReferenceKey = null,
    [property: JsonPropertyName("active")] bool? Active = null);

public record ExternalInstanceCredentialDeleteRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId);
