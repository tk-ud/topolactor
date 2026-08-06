using Topolactor.Schema;

namespace Topolactor.Repository;

public abstract class EnumDictionaryRepository
{
    protected readonly string _connectionString;

    protected EnumDictionaryRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public abstract Task<IReadOnlyList<EnumDictionaryGroupDto>> ListGroupsAsync(
        CancellationToken ct = default);

    // generic list_groups search/filter (admin-enum subBundle closure round; extended round 36 to
    // the owning SSOT's full declared search field set, and round 37 to the owning SSOT's own
    // declared FILTER field set -- see EnumDictionaryListGroupsRequestDto).
    // search is an OPTIONAL case-insensitive substring filter across group_name/groups.index_num
    // (the group's own identity) and items.name/items.index_num/group_items.position (any member
    // item's identity/position -- matches the whole group that contains it). groupNameFilter/
    // groupIndexNumFilter/itemPositionFilter (round 37) are the owning SSOT's three declared
    // filter target fields, each an OPTIONAL exact match, AND-combined with each other and with
    // search when more than one is supplied. Absent/null for all returns the canonical full list
    // -- the exact pre-existing behavior every prior caller relied on.
    public abstract Task<IReadOnlyList<EnumDictionaryGroupWithItemsSummaryDto>> ListGroupsWithItemsSummaryAsync(
        string? search = null, string? groupNameFilter = null, int? groupIndexNumFilter = null,
        int? itemPositionFilter = null, CancellationToken ct = default);

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

    public abstract Task<EnumDictionaryGroupDto> CreateGroupAsync(
        string groupName, int? indexNum, CancellationToken ct = default);

    public abstract Task<EnumDictionaryGroupDto?> UpdateGroupAsync(
        Guid groupId, string? groupName, int? indexNum, CancellationToken ct = default);

    public abstract Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct = default);

    public abstract Task<EnumDictionaryItemDto> CreateItemAsync(
        string name, int? indexNum, CancellationToken ct = default);

    public abstract Task<EnumDictionaryItemDto?> GetItemAsync(
        int indexNum, CancellationToken ct = default);

    public abstract Task<EnumDictionaryItemDto?> UpdateItemAsync(
        int indexNum, string? name, int? newIndexNum, CancellationToken ct = default);

    public abstract Task<bool> DeleteItemAsync(int indexNum, CancellationToken ct = default);

    public abstract Task<EnumDictionaryGroupDetailDto?> SetGroupItemsAsync(
        Guid groupId, IReadOnlyList<int> enumIndexNums, CancellationToken ct = default);

    public virtual Task<bool> IsGroupReferencedInManifestsAsync(
        Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(false);

    // Mirrors IsGroupReferencedInManifestsAsync's role for items: enum.group_items.enum_index_num
    // REFERENCES enum.items(index_num) with no ON DELETE clause (db/enum_tables.sql), so a
    // referenced item's delete would otherwise surface as a raw, untranslated FK violation.
    public virtual Task<bool> IsItemReferencedInGroupsAsync(
        int indexNum, CancellationToken ct = default) =>
        Task.FromResult(false);
}
