using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// AdminRuntime partial: credential_registry layer handlers.
/// Delegates to SecretCredentialBundleRuntime via the standard IDispatchableRuntime path.
///
/// SSOT: docs/design/runtime-bundle-secret-credential-ssot.yaml
///   trigger_kind.allowed: admin_config_credential_registration
///   authority_boundary: admin_config_and_runtime_secret_store
///
/// Security boundary:
///   All credential operations through this layer require admin JWT authentication
///   (enforced by the admin dispatch middleware upstream).
///   No credential values appear in responses — reference metadata only.
/// </summary>
public partial class AdminRuntime
{
    private async Task<(JsonElement? data, ValidationError? error)> DataCredentialListAsync(
        CancellationToken ct)
    {
        if (_credentialRuntime is null)
            return (null, new ValidationError("CREDENTIAL_RUNTIME_NOT_AVAILABLE",
                "Credential runtime is not registered."));

        var request = new EndpointRequestDto("Search", "credential", "admin", "list", null, null, null);
        var response = await _credentialRuntime.ExecuteAsync(request, manifestId: null, ct);

        if (!response.Success)
            return (null, response.Errors.FirstOrDefault() ??
                new ValidationError("CREDENTIAL_LIST_FAILED", "Failed to list credential references."));

        return (response.Emission?.Data, null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCredentialRegisterAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_credentialRuntime is null)
            return (null, new ValidationError("CREDENTIAL_RUNTIME_NOT_AVAILABLE",
                "Credential runtime is not registered."));

        var request = new EndpointRequestDto("Create", "credential", "admin", "register",
            null, vector.Payload, null);
        var response = await _credentialRuntime.ExecuteAsync(request, manifestId: null, ct);

        if (!response.Success)
            return (null, response.Errors.FirstOrDefault() ??
                new ValidationError("CREDENTIAL_REGISTER_FAILED", "Failed to register credential reference."));

        return (response.Emission?.Data, null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCredentialValidateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_credentialRuntime is null)
            return (null, new ValidationError("CREDENTIAL_RUNTIME_NOT_AVAILABLE",
                "Credential runtime is not registered."));

        var request = new EndpointRequestDto("Search", "credential", "admin", "validate",
            null, vector.Payload, null);
        var response = await _credentialRuntime.ExecuteAsync(request, manifestId: null, ct);

        if (!response.Success)
            return (null, response.Errors.FirstOrDefault() ??
                new ValidationError("CREDENTIAL_VALIDATION_FAILED", "Credential validation failed."));

        return (response.Emission?.Data, null);
    }

    private async Task<(JsonElement? data, ValidationError? error)> DataCredentialRotateAsync(
        OperationVector vector, CancellationToken ct)
    {
        if (_credentialRuntime is null)
            return (null, new ValidationError("CREDENTIAL_RUNTIME_NOT_AVAILABLE",
                "Credential runtime is not registered."));

        var request = new EndpointRequestDto("Update", "credential", "admin", "rotate",
            null, vector.Payload, null);
        var response = await _credentialRuntime.ExecuteAsync(request, manifestId: null, ct);

        if (!response.Success)
            return (null, response.Errors.FirstOrDefault() ??
                new ValidationError("CREDENTIAL_ROTATION_FAILED", "Failed to rotate credential."));

        return (response.Emission?.Data, null);
    }
}
