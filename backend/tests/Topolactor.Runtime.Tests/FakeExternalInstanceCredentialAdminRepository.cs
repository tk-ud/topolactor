using Topolactor.Repository;

namespace Topolactor.Runtime.Tests;

// In-memory double for IExternalInstanceCredentialAdminRepository (round 5). Mirrors the real
// NpgsqlExternalInstanceCredentialAdminRepository's search/filter semantics (recordKind/
// providerKind/requiredByBundle/active/query/expiresBefore/expiresAfter) and its
// ReferenceKeyExists-based create/update fail-close (mutation_boundary: create/update only ever
// point a port row at an EXISTING vault reference_key, never write the vault itself) without a
// real database.
public sealed class FakeExternalInstanceCredentialAdminRepository : IExternalInstanceCredentialAdminRepository
{
    public List<ExternalInstanceCredentialRecord> Rows { get; } = new();
    public HashSet<string> ExistingVaultReferenceKeys { get; } = new(StringComparer.Ordinal);
    public List<ExternalInstanceCredentialCreateRequest> Created { get; } = new();
    public List<ExternalInstanceCredentialUpdateRequest> Updated { get; } = new();
    public List<(string RecordKind, Guid RecordId)> Deactivated { get; } = new();

    public Task<IReadOnlyList<ExternalInstanceCredentialRecord>> SearchAsync(
        string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
        DateTimeOffset? expiresBefore = null, DateTimeOffset? expiresAfter = null,
        CancellationToken ct = default)
    {
        IEnumerable<ExternalInstanceCredentialRecord> rows = Rows;
        if (recordKind is not null) rows = rows.Where(r => r.RecordKind == recordKind);
        if (providerKind is not null) rows = rows.Where(r => r.ProviderKind == providerKind);
        if (requiredByBundle is not null) rows = rows.Where(r => r.RequiredByBundle == requiredByBundle);
        if (active is not null) rows = rows.Where(r => r.Active == active);
        if (!string.IsNullOrEmpty(query))
            rows = rows.Where(r =>
                r.InstanceAuthorityKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.ProviderKind.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.RequiredByBundle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.ReferenceKey.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (expiresBefore is not null) rows = rows.Where(r => r.ExpiresAt is not null && r.ExpiresAt < expiresBefore);
        if (expiresAfter is not null) rows = rows.Where(r => r.ExpiresAt is not null && r.ExpiresAt > expiresAfter);
        return Task.FromResult<IReadOnlyList<ExternalInstanceCredentialRecord>>(rows.ToList());
    }

    public Task<ExternalInstanceCredentialRecord?> GetAsync(string recordKind, Guid recordId, CancellationToken ct = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId));

    public ExternalInstanceCredentialWriteResult ValidateCreateRequest(ExternalInstanceCredentialCreateRequest request)
    {
        if (!ExternalInstanceCredentialRecordKinds.All.Contains(request.RecordKind))
            return new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
        if (string.IsNullOrWhiteSpace(request.InstanceAuthorityKey) || string.IsNullOrWhiteSpace(request.ProviderKind) ||
            string.IsNullOrWhiteSpace(request.RequiredByBundle) || string.IsNullOrWhiteSpace(request.ReferenceKey))
            return new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.RequiredFieldMissing,
                Detail: "instanceAuthorityKey, providerKind, requiredByBundle, and referenceKey are required.");
        return new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.Ok);
    }

    public Task<ExternalInstanceCredentialWriteResult> ValidateCreateRequestAsync(ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation.Outcome != ExternalInstanceCredentialOutcome.Ok) return Task.FromResult(validation);
        if (!ExistingVaultReferenceKeys.Contains(request.ReferenceKey))
            return Task.FromResult(new ExternalInstanceCredentialWriteResult(
                ExternalInstanceCredentialOutcome.ReferenceKeyNotFound, Detail: $"referenceKey '{request.ReferenceKey}' does not exist in the external credential vault."));
        return Task.FromResult(new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.Ok));
    }

    public async Task<ExternalInstanceCredentialWriteResult> CreateAsync(ExternalInstanceCredentialCreateRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateCreateRequestAsync(request, ct);
        if (validation.Outcome != ExternalInstanceCredentialOutcome.Ok) return validation;

        Created.Add(request);
        var record = new ExternalInstanceCredentialRecord(
            request.RecordKind, Guid.NewGuid(), request.InstanceAuthorityKey, request.ProviderKind, request.RequiredByBundle,
            request.ReferenceKey, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 1);
        Rows.Add(record);
        return new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.Ok, record);
    }

    public Task<ExternalInstanceCredentialWriteResult> ValidateUpdateRequestAsync(ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var existing = Rows.FirstOrDefault(r => r.RecordKind == request.RecordKind && r.RecordId == request.RecordId);
        if (existing is null)
            return Task.FromResult(new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.NotFound, Detail: "not found"));
        return Task.FromResult(new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.Ok, existing));
    }

    public async Task<ExternalInstanceCredentialWriteResult> UpdateAsync(ExternalInstanceCredentialUpdateRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateUpdateRequestAsync(request, ct);
        if (validation.Outcome != ExternalInstanceCredentialOutcome.Ok) return validation;
        if (request.ReferenceKey is not null && !ExistingVaultReferenceKeys.Contains(request.ReferenceKey))
            return new ExternalInstanceCredentialWriteResult(
                ExternalInstanceCredentialOutcome.ReferenceKeyNotFound, Detail: $"referenceKey '{request.ReferenceKey}' does not exist in the external credential vault.");

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
        return new ExternalInstanceCredentialWriteResult(ExternalInstanceCredentialOutcome.Ok, updated);
    }

    public Task<ExternalInstanceCredentialOutcome> DeactivateAsync(string recordKind, Guid recordId, CancellationToken ct = default)
    {
        var existing = Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId);
        if (existing is null) return Task.FromResult(ExternalInstanceCredentialOutcome.NotFound);
        Deactivated.Add((recordKind, recordId));
        Rows.Remove(existing);
        Rows.Add(existing with { Active = false });
        return Task.FromResult(ExternalInstanceCredentialOutcome.Ok);
    }
}
