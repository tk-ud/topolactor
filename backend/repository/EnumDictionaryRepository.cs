using Topolactor.Schema;

namespace Topolactor.Repository;

public abstract class EnumDictionaryRepository
{
    protected readonly string _connectionString;

    protected EnumDictionaryRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public abstract Task<IReadOnlyList<EnumDictionaryGroupDto>> ListGroupsAsync(
        CancellationToken ct = default);

    public abstract Task<EnumDictionaryGroupDetailDto?> GetGroupDetailAsync(
        Guid groupId, CancellationToken ct = default);

    public virtual async Task<ValidationError?> ValidateGroupReferenceAsync(
        Guid groupId, CancellationToken ct = default)
    {
        var detail = await GetGroupDetailAsync(groupId, ct);
        if (detail is null)
        {
            return new ValidationError(
                "ENUM_GROUP_NOT_FOUND",
                $"Enum group {groupId} was not found in the canonical dictionary.");
        }

        if (detail.Items.Count == 0)
        {
            return new ValidationError(
                "ENUM_GROUP_ITEMS_EMPTY",
                $"Enum group {groupId} ({detail.GroupName}) has no items.");
        }

        return null;
    }
}
