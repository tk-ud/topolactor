using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// credential-management external_api_credential category
// (docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
// credentials.categories.external_api_credential). Request payload shapes only -- the
// secret-free response projection is built directly from
// Topolactor.Repository.ExternalApiCredentialRecord in AdminRuntime.ExternalApiCredential.cs.

public record ExternalApiCredentialGetRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId);

// PlaintextSecret/EncryptionKeyReference are vault-only, write-only: never appear on any response,
// preview, or diff_log -- see AdminRuntime.ExternalApiCredential.cs's sanitized audit envelope.
public record ExternalApiCredentialCreateRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("providerKind")] string ProviderKind,
    [property: JsonPropertyName("requiredByBundle")] string RequiredByBundle,
    [property: JsonPropertyName("referenceKey")] string? ReferenceKey = null,
    [property: JsonPropertyName("tokenKind")] string? TokenKind = null,
    [property: JsonPropertyName("refreshBeforeSeconds")] int? RefreshBeforeSeconds = null,
    [property: JsonPropertyName("urlOrEnvReference")] string? UrlOrEnvReference = null,
    [property: JsonPropertyName("credentialKind")] string? CredentialKind = null,
    [property: JsonPropertyName("hookPath")] string? HookPath = null,
    [property: JsonPropertyName("headerKey")] string? HeaderKey = null,
    [property: JsonPropertyName("routeKey")] string? RouteKey = null,
    [property: JsonPropertyName("plaintextSecret")] string? PlaintextSecret = null,
    [property: JsonPropertyName("encryptionKeyReference")] string? EncryptionKeyReference = null);

public record ExternalApiCredentialUpdateRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId,
    [property: JsonPropertyName("providerKind")] string? ProviderKind = null,
    [property: JsonPropertyName("requiredByBundle")] string? RequiredByBundle = null,
    [property: JsonPropertyName("referenceKey")] string? ReferenceKey = null,
    [property: JsonPropertyName("active")] bool? Active = null,
    [property: JsonPropertyName("tokenKind")] string? TokenKind = null,
    [property: JsonPropertyName("refreshBeforeSeconds")] int? RefreshBeforeSeconds = null,
    [property: JsonPropertyName("urlOrEnvReference")] string? UrlOrEnvReference = null,
    [property: JsonPropertyName("credentialKind")] string? CredentialKind = null,
    [property: JsonPropertyName("hookPath")] string? HookPath = null,
    [property: JsonPropertyName("headerKey")] string? HeaderKey = null,
    [property: JsonPropertyName("routeKey")] string? RouteKey = null,
    [property: JsonPropertyName("newPlaintextSecret")] string? NewPlaintextSecret = null,
    [property: JsonPropertyName("encryptionKeyReference")] string? EncryptionKeyReference = null);

public record ExternalApiCredentialDeleteRequestDto(
    [property: JsonPropertyName("recordKind")] string RecordKind,
    [property: JsonPropertyName("recordId")] string RecordId);
