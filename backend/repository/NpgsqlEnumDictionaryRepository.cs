using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class NpgsqlEnumDictionaryRepository : EnumDictionaryRepository
{
    public NpgsqlEnumDictionaryRepository(string connectionString) : base(connectionString) { }

    public override async Task<IReadOnlyList<EnumDictionaryGroupDto>> ListGroupsAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT group_id, index_num, group_name FROM enum.groups ORDER BY index_num";
        var list = new List<EnumDictionaryGroupDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new EnumDictionaryGroupDto(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetString(2)));
        }

        return list;
    }

    public override async Task<EnumDictionaryGroupDetailDto?> GetGroupDetailAsync(
        Guid groupId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var groupCmd = conn.CreateCommand();
        groupCmd.CommandText =
            "SELECT group_id, index_num, group_name FROM enum.groups WHERE group_id = @id LIMIT 1";
        groupCmd.Parameters.AddWithValue("id", groupId);
        await using var groupReader = await groupCmd.ExecuteReaderAsync(ct);
        if (!await groupReader.ReadAsync(ct)) return null;

        var gid = groupReader.GetGuid(0);
        var indexNum = groupReader.GetInt32(1);
        var groupName = groupReader.GetString(2);
        await groupReader.CloseAsync();

        await using var itemsCmd = conn.CreateCommand();
        itemsCmd.CommandText =
            """
            SELECT gi.enum_index_num, i.name
            FROM enum.group_items gi
            JOIN enum.items i ON i.index_num = gi.enum_index_num
            WHERE gi.group_id = @id
            ORDER BY gi.position
            """;
        itemsCmd.Parameters.AddWithValue("id", gid);
        var items = new List<EnumDictionaryItemDto>();
        var indexNums = new List<int>();
        await using var itemsReader = await itemsCmd.ExecuteReaderAsync(ct);
        while (await itemsReader.ReadAsync(ct))
        {
            var itemIndex = itemsReader.GetInt32(0);
            items.Add(new EnumDictionaryItemDto(itemIndex, itemsReader.GetString(1)));
            indexNums.Add(itemIndex);
        }

        return new EnumDictionaryGroupDetailDto(gid, indexNum, groupName, items, indexNums);
    }
}
