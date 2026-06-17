namespace Topolactor.Runtime;

/// <summary>
/// Resolves a credential vault record for a given port record using provider_kind and required_by_bundle.
/// This is the generic lookup path; provider-specific credential branching is prohibited.
/// </summary>
public sealed class ExternalPortCredentialReferenceResolver : IExternalPortCredentialReferenceResolver
{
    private readonly IExternalCredentialVaultRepository _vaultRepository;

    public ExternalPortCredentialReferenceResolver(IExternalCredentialVaultRepository vaultRepository)
    {
        _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));
    }

    public Task<ExternalCredentialVaultRecord?> ResolveCredentialReferenceAsync(ExternalPortRecord portRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(portRecord);
        return _vaultRepository.LoadByProviderAndBundleAsync(portRecord.ProviderKind, portRecord.RequiredByBundle, ct);
    }
}
