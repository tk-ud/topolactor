using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

public sealed class InMemoryEnumDictionaryRepository : EnumDictionaryRepository
{
    public static readonly Guid DemoGroupId = Guid.Parse("22222222-2222-2222-2222-222222222201");

    private readonly Dictionary<Guid, EnumDictionaryGroupDetailDto> _groups;

    public InMemoryEnumDictionaryRepository(IEnumerable<EnumDictionaryGroupDetailDto> groups)
    {
        _connectionString = "in-memory";
        _groups = groups.ToDictionary(g => g.GroupId);
    }

    public static InMemoryEnumDictionaryRepository WithDemoSeed() =>
        new([
            new EnumDictionaryGroupDetailDto(
                DemoGroupId,
                1,
                "demo_status",
                [
                    new EnumDictionaryItemDto(1, "demo_active"),
                    new EnumDictionaryItemDto(2, "demo_inactive"),
                    new EnumDictionaryItemDto(3, "demo_pending"),
                ],
                [1, 2, 3]),
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

    public override Task<EnumDictionaryGroupDetailDto?> GetGroupDetailAsync(
        Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(_groups.TryGetValue(groupId, out var detail) ? detail : null);
}
