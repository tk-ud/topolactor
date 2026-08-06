using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

public sealed class InMemoryEnumDictionaryRepository : EnumDictionaryRepository
{
    public static readonly Guid FixtureGroupId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid UserStatusGroupId = AuthMasterRosterConstants.UserStatusGroupId;

    private readonly Dictionary<Guid, EnumDictionaryGroupDetailDto> _groups;
    private readonly Dictionary<int, string> _itemsByIndex = new();

    public InMemoryEnumDictionaryRepository(IEnumerable<EnumDictionaryGroupDetailDto> groups)
        : base("in-memory")
    {
        _groups = groups.ToDictionary(g => g.GroupId);
        foreach (var g in _groups.Values)
            foreach (var item in g.Items)
                _itemsByIndex[item.IndexNum] = item.Name;
    }

    public static InMemoryEnumDictionaryRepository WithFixtureSeed() =>
        new([
            new EnumDictionaryGroupDetailDto(
                FixtureGroupId,
                1,
                "fixture_status",
                [
                    new EnumDictionaryItemDto(1, "fixture_active"),
                    new EnumDictionaryItemDto(2, "fixture_inactive"),
                    new EnumDictionaryItemDto(3, "fixture_pending"),
                ],
                [1, 2, 3]),
            new EnumDictionaryGroupDetailDto(
                UserStatusGroupId,
                2,
                "user_status",
                [
                    new EnumDictionaryItemDto(10, "active"),
                    new EnumDictionaryItemDto(11, "inactive"),
                    new EnumDictionaryItemDto(12, "suspended"),
                    new EnumDictionaryItemDto(13, "archived"),
                ],
                [10, 11, 12, 13]),
        ]);

    public static InMemoryEnumDictionaryRepository WithEmptyGroup(Guid groupId) =>
        new([
            new EnumDictionaryGroupDetailDto(groupId, 99, "empty_group", [], []),
        ]);

    public override Task<IReadOnlyList<EnumDictionaryGroupDto>> ListGroupsAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EnumDictionaryGroupDto>>(
            _groups.Values
                .Select(g => new EnumDictionaryGroupDto(g.GroupId, g.IndexNum, g.GroupName))
                .OrderBy(g => g.IndexNum)
                .ToList());

    public override Task<IReadOnlyList<EnumDictionaryGroupWithItemsSummaryDto>> ListGroupsWithItemsSummaryAsync(
        string? search = null, string? groupNameFilter = null, int? groupIndexNumFilter = null,
        int? itemPositionFilter = null, CancellationToken ct = default)
    {
        var trimmedSearch = search?.Trim();
        var trimmedGroupNameFilter = groupNameFilter?.Trim();
        IEnumerable<EnumDictionaryGroupDetailDto> groups = _groups.Values;
        if (!string.IsNullOrEmpty(trimmedSearch))
        {
            // Mirrors NpgsqlEnumDictionaryRepository's SQL exactly: matches the group's own
            // identity (group_name/index_num) OR any member item's identity/position
            // (items.name/items.index_num/group_items.position, position == 0-based index within
            // g.Items, the same order SetGroupItemsAsync assigns).
            groups = groups.Where(g =>
                g.GroupName.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                || g.IndexNum.ToString().Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                || g.Items.Select((item, position) => (item, position)).Any(t =>
                    t.item.Name.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || t.item.IndexNum.ToString().Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || t.position.ToString().Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)));
        }
        // round 37: the owning SSOT's three declared filter target fields, mirroring
        // NpgsqlEnumDictionaryRepository's exact-match SQL clauses.
        if (!string.IsNullOrEmpty(trimmedGroupNameFilter))
        {
            groups = groups.Where(g => g.GroupName == trimmedGroupNameFilter);
        }
        if (groupIndexNumFilter.HasValue)
        {
            groups = groups.Where(g => g.IndexNum == groupIndexNumFilter.Value);
        }
        if (itemPositionFilter.HasValue)
        {
            groups = groups.Where(g =>
                g.Items.Select((item, position) => position).Any(position => position == itemPositionFilter.Value));
        }
        return Task.FromResult<IReadOnlyList<EnumDictionaryGroupWithItemsSummaryDto>>(
            groups
                .OrderBy(g => g.IndexNum)
                .Select(g => new EnumDictionaryGroupWithItemsSummaryDto(
                    g.GroupId,
                    g.IndexNum,
                    g.GroupName,
                    string.Join(", ", g.Items.Select(i => $"{i.IndexNum}:{i.Name}")),
                    string.Join(",", g.Items.Select(i => i.IndexNum))))
                .ToList());
    }

    public override Task<EnumDictionaryGroupDetailDto?> GetGroupDetailAsync(
        Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(_groups.TryGetValue(groupId, out var detail) ? detail : null);

    public override Task<EnumDictionaryGroupDto> CreateGroupAsync(
        string groupName, int? indexNum, CancellationToken ct = default)
    {
        var idx = indexNum ?? (_groups.Count == 0 ? 1 : _groups.Values.Max(g => g.IndexNum) + 1);
        if (_groups.Values.Any(g => g.IndexNum == idx))
            throw new InvalidOperationException("ENUM_GROUP_INDEX_CONFLICT");
        var id = Guid.NewGuid();
        var detail = new EnumDictionaryGroupDetailDto(id, idx, groupName, [], []);
        _groups[id] = detail;
        return Task.FromResult(new EnumDictionaryGroupDto(id, idx, groupName));
    }

    public override Task<EnumDictionaryGroupDto?> UpdateGroupAsync(
        Guid groupId, string? groupName, int? indexNum, CancellationToken ct = default)
    {
        if (!_groups.TryGetValue(groupId, out var existing)) return Task.FromResult<EnumDictionaryGroupDto?>(null);
        var name = groupName ?? existing.GroupName;
        var idx = indexNum ?? existing.IndexNum;
        if (idx != existing.IndexNum && _groups.Values.Any(g => g.GroupId != groupId && g.IndexNum == idx))
            throw new InvalidOperationException("ENUM_GROUP_INDEX_CONFLICT");
        _groups[groupId] = existing with { GroupName = name, IndexNum = idx };
        return Task.FromResult<EnumDictionaryGroupDto?>(new EnumDictionaryGroupDto(groupId, idx, name));
    }

    public override Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(_groups.Remove(groupId));

    public override Task<EnumDictionaryItemDto> CreateItemAsync(
        string name, int? indexNum, CancellationToken ct = default)
    {
        var idx = indexNum ?? (_itemsByIndex.Count == 0 ? 1 : _itemsByIndex.Keys.Max() + 1);
        if (_itemsByIndex.ContainsKey(idx))
            throw new InvalidOperationException("ENUM_ITEM_INDEX_CONFLICT");
        _itemsByIndex[idx] = name;
        return Task.FromResult(new EnumDictionaryItemDto(idx, name));
    }

    public override Task<EnumDictionaryItemDto?> GetItemAsync(
        int indexNum, CancellationToken ct = default) =>
        Task.FromResult(
            _itemsByIndex.TryGetValue(indexNum, out var name)
                ? new EnumDictionaryItemDto(indexNum, name)
                : null);

    public override Task<EnumDictionaryItemDto?> UpdateItemAsync(
        int indexNum, string? name, int? newIndexNum, CancellationToken ct = default)
    {
        if (!_itemsByIndex.TryGetValue(indexNum, out var existingName))
            return Task.FromResult<EnumDictionaryItemDto?>(null);
        if (newIndexNum.HasValue && newIndexNum.Value != indexNum)
        {
            if (_itemsByIndex.ContainsKey(newIndexNum.Value))
                throw new InvalidOperationException("ENUM_ITEM_INDEX_CONFLICT");
            if (_groups.Values.Any(g => g.ItemsIndexNums.Contains(indexNum)))
                throw new InvalidOperationException("ENUM_ITEM_IN_USE");
            _itemsByIndex.Remove(indexNum);
            indexNum = newIndexNum.Value;
            _itemsByIndex[indexNum] = name ?? existingName;
        }
        else if (name is not null)
        {
            _itemsByIndex[indexNum] = name;
        }

        return Task.FromResult<EnumDictionaryItemDto?>(new EnumDictionaryItemDto(indexNum, _itemsByIndex[indexNum]));
    }

    public override Task<bool> DeleteItemAsync(int indexNum, CancellationToken ct = default)
    {
        if (_groups.Values.Any(g => g.ItemsIndexNums.Contains(indexNum)))
            throw new InvalidOperationException("ENUM_ITEM_IN_USE");
        return Task.FromResult(_itemsByIndex.Remove(indexNum));
    }

    public override Task<bool> IsItemReferencedInGroupsAsync(
        int indexNum, CancellationToken ct = default) =>
        Task.FromResult(_groups.Values.Any(g => g.ItemsIndexNums.Contains(indexNum)));

    public override Task<EnumDictionaryGroupDetailDto?> SetGroupItemsAsync(
        Guid groupId, IReadOnlyList<int> enumIndexNums, CancellationToken ct = default)
    {
        if (!_groups.TryGetValue(groupId, out var existing)) return Task.FromResult<EnumDictionaryGroupDetailDto?>(null);
        var items = enumIndexNums
            .Where(n => _itemsByIndex.ContainsKey(n))
            .Select(n => new EnumDictionaryItemDto(n, _itemsByIndex[n]))
            .ToList();
        var detail = existing with { Items = items, ItemsIndexNums = enumIndexNums.ToList() };
        _groups[groupId] = detail;
        return Task.FromResult<EnumDictionaryGroupDetailDto?>(detail);
    }
}
