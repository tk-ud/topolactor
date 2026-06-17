namespace Topolactor.Runtime;

/// <summary>
/// Resolves a credential vault record for a given port record using the port's reference_key column.
/// This is the generic lookup path via LoadByReferenceKeyAsync; provider-specific credential branching is prohibited.
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
        if (string.IsNullOrWhiteSpace(portRecord.ReferenceKey))
            throw new InvalidOperationException("EXTERNAL_PORT_REFERENCE_KEY_MISSING");
        return _vaultRepository.LoadByReferenceKeyAsync(portRecord.ReferenceKey, ct);
    }
}
