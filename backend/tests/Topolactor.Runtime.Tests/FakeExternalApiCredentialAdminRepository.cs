using Topolactor.Repository;

namespace Topolactor.Runtime.Tests;

// Shared across AdminRuntimeExternalApiCredentialTests (get/create/update/delete) and
// AdminRuntimeCredentialManagementSearchTests (unified credential_management:search,
// category=external_api_credential) -- extracted to its own file (round 5) so both test classes
// exercise the SAME fake repository shape rather than each keeping a private drifted copy.
public sealed class FakeExternalApiCredentialAdminRepository : IExternalApiCredentialAdminRepository
{
    public List<ExternalApiCredentialRecord> Rows { get; } = new();
    public List<ExternalApiCredentialCreateRequest> Created { get; } = new();
    public List<ExternalApiCredentialUpdateRequest> Updated { get; } = new();
    public List<(string RecordKind, Guid RecordId)> Deactivated { get; } = new();

    public Task<IReadOnlyList<ExternalApiCredentialRecord>> SearchAsync(
        string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
        DateTimeOffset? expiresBefore = null, DateTimeOffset? expiresAfter = null,
        CancellationToken ct = default)
    {
        IEnumerable<ExternalApiCredentialRecord> rows = Rows;
        if (recordKind is not null) rows = rows.Where(r => r.RecordKind == recordKind);
        if (providerKind is not null) rows = rows.Where(r => r.ProviderKind == providerKind);
        if (requiredByBundle is not null) rows = rows.Where(r => r.RequiredByBundle == requiredByBundle);
        if (active is not null) rows = rows.Where(r => r.Active == active);
        if (expiresBefore is not null) rows = rows.Where(r => r.ExpiresAt is not null && r.ExpiresAt < expiresBefore);
        if (expiresAfter is not null) rows = rows.Where(r => r.ExpiresAt is not null && r.ExpiresAt > expiresAfter);
        return Task.FromResult<IReadOnlyList<ExternalApiCredentialRecord>>(rows.ToList());
    }

    public Task<ExternalApiCredentialRecord?> GetAsync(string recordKind, Guid recordId, CancellationToken ct = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId));

    public ExternalApiCredentialWriteResult ValidateCreateRequest(ExternalApiCredentialCreateRequest request)
    {
        if (!ExternalApiCredentialRecordKinds.All.Contains(request.RecordKind))
            return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        if (string.IsNullOrWhiteSpace(request.ProviderKind) || string.IsNullOrWhiteSpace(request.RequiredByBundle))
            return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.RequiredFieldMissing, Detail: "providerKind and requiredByBundle are required.");
        return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok);
    }

    public Task<ExternalApiCredentialWriteResult> CreateAsync(ExternalApiCredentialCreateRequest request, CancellationToken ct = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation.Outcome != ExternalApiCredentialOutcome.Ok) return Task.FromResult(validation);
        Created.Add(request);
        var record = new ExternalApiCredentialRecord(
            request.RecordKind, Guid.NewGuid(), request.ProviderKind, request.RequiredByBundle, request.ReferenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TokenKind, null, request.RefreshBeforeSeconds,
            1, request.UrlOrEnvReference, request.CredentialKind, request.HookPath, request.HeaderKey, request.RouteKey);
        Rows.Add(record);
        return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, record));
    }

    public Task<ExternalApiCredentialWriteResult> ValidateUpdateRequestAsync(ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var existing = Rows.FirstOrDefault(r => r.RecordKind == request.RecordKind && r.RecordId == request.RecordId);
        if (existing is null)
            return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.NotFound, Detail: "not found"));
        return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, existing));
    }

    public async Task<ExternalApiCredentialWriteResult> UpdateAsync(ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateUpdateRequestAsync(request, ct);
        if (validation.Outcome != ExternalApiCredentialOutcome.Ok) return validation;
        Updated.Add(request);
        var existing = validation.Record!;
        var updated = existing with
        {
            ProviderKind = request.ProviderKind ?? existing.ProviderKind,
            RequiredByBundle = request.RequiredByBundle ?? existing.RequiredByBundle,
            ReferenceKey = request.ReferenceKey ?? existing.ReferenceKey,
            Active = request.Active ?? existing.Active,
        };
        Rows.Remove(existing);
        Rows.Add(updated);
        return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, updated);
    }

    public Task<ExternalApiCredentialOutcome> DeactivateAsync(string recordKind, Guid recordId, CancellationToken ct = default)
    {
        var existing = Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId);
        if (existing is null) return Task.FromResult(ExternalApiCredentialOutcome.NotFound);
        Deactivated.Add((recordKind, recordId));
        Rows.Remove(existing);
        Rows.Add(existing with { Active = false });
        return Task.FromResult(ExternalApiCredentialOutcome.Ok);
    }
}
