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

    public override async Task<IReadOnlyList<EnumDictionaryGroupWithItemsSummaryDto>> ListGroupsWithItemsSummaryAsync(
        string? search = null, string? groupNameFilter = null, int? groupIndexNumFilter = null,
        int? itemPositionFilter = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        var trimmedGroupNameFilter = groupNameFilter?.Trim();
        var hasSearch = !string.IsNullOrEmpty(search?.Trim());
        var hasGroupNameFilter = !string.IsNullOrEmpty(trimmedGroupNameFilter);
        var hasGroupIndexNumFilter = groupIndexNumFilter.HasValue;
        var hasItemPositionFilter = itemPositionFilter.HasValue;

        var whereClauses = new List<string>();
        if (hasSearch)
        {
            // generic list_groups search (admin-enum subBundle closure round; extended round 36 to
            // the owning SSOT's full declared search target field set -- docs/design/
            // admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.enum.
            // capability_requirements.search): case-insensitive substring match, parameterized --
            // never string-concatenated -- across the group's OWN identity (group_name/index_num)
            // OR any MEMBER ITEM's identity/position (items.name/items.index_num/
            // group_items.position). An EXISTS subquery, not a join-level filter, so a group that
            // matches via one item still returns its FULL itemsSummary (every member item), not
            // only the matching one -- the outer LEFT JOIN below stays unfiltered for that reason.
            whereClauses.Add(
                """
                (g.group_name ILIKE @search
                 OR CAST(g.index_num AS TEXT) ILIKE @search
                 OR EXISTS (
                     SELECT 1 FROM enum.group_items gi2
                     JOIN enum.items i2 ON i2.index_num = gi2.enum_index_num
                     WHERE gi2.group_id = g.group_id
                       AND (i2.name ILIKE @search
                            OR CAST(i2.index_num AS TEXT) ILIKE @search
                            OR CAST(gi2.position AS TEXT) ILIKE @search)
                 ))
                """);
        }
        // round 37: the owning SSOT's own three declared filter target fields (enum.groups.
        // group_name, enum.groups.index_num, enum.group_items.position), each an independent
        // exact-match clause, AND-combined -- replaces round 36's groupIdFilter (an exact match on
        // enum.groups.group_id, a field the owning SSOT never actually declares as part of
        // capability_requirements.filter). enum_group_filter drives groupNameFilter (its own
        // option value is now the group's name, not an opaque id).
        if (hasGroupNameFilter)
        {
            whereClauses.Add("g.group_name = @groupNameFilter");
        }
        if (hasGroupIndexNumFilter)
        {
            whereClauses.Add("g.index_num = @groupIndexNumFilter");
        }
        if (hasItemPositionFilter)
        {
            // group_items.position lives on the per-item membership row, not the group row itself
            // -- an EXISTS subquery scopes to "this group has SOME member at exactly this
            // position", the same "matches via a member, but the group's FULL itemsSummary is
            // still returned" shape search's own EXISTS clause above already uses.
            whereClauses.Add(
                """
                EXISTS (
                    SELECT 1 FROM enum.group_items gi3
                    WHERE gi3.group_id = g.group_id AND gi3.position = @itemPositionFilter
                )
                """);
        }

        cmd.CommandText =
            """
            SELECT g.group_id, g.index_num, g.group_name,
                   COALESCE(
                       string_agg(i.index_num || ':' || i.name, ', ' ORDER BY gi.position),
                       ''
                   ) AS items_summary
            FROM enum.groups g
            LEFT JOIN enum.group_items gi ON gi.group_id = g.group_id
            LEFT JOIN enum.items i ON i.index_num = gi.enum_index_num
            """
            + (whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "") +
            """

            GROUP BY g.group_id, g.index_num, g.group_name
            ORDER BY g.index_num
            """;
        if (hasSearch)
        {
            cmd.Parameters.AddWithValue("search", $"%{search!.Trim()}%");
        }
        if (hasGroupNameFilter)
        {
            cmd.Parameters.AddWithValue("groupNameFilter", trimmedGroupNameFilter!);
        }
        if (hasGroupIndexNumFilter)
        {
            cmd.Parameters.AddWithValue("groupIndexNumFilter", groupIndexNumFilter!.Value);
        }
        if (hasItemPositionFilter)
        {
            cmd.Parameters.AddWithValue("itemPositionFilter", itemPositionFilter!.Value);
        }
        var list = new List<EnumDictionaryGroupWithItemsSummaryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new EnumDictionaryGroupWithItemsSummaryDto(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3)));
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

    public override async Task<EnumDictionaryGroupDto> CreateGroupAsync(
        string groupName, int? indexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var idx = indexNum ?? await NextGroupIndexAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO enum.groups (index_num, group_name) VALUES (@i, @n) RETURNING group_id, index_num, group_name";
        cmd.Parameters.AddWithValue("i", idx);
        cmd.Parameters.AddWithValue("n", groupName);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new EnumDictionaryGroupDto(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2));
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("ENUM_GROUP_INDEX_CONFLICT");
        }
    }

    public override async Task<EnumDictionaryGroupDto?> UpdateGroupAsync(
        Guid groupId, string? groupName, int? indexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var sets = new List<string>();
        await using var cmd = conn.CreateCommand();
        if (groupName is not null) { sets.Add("group_name = @n"); cmd.Parameters.AddWithValue("n", groupName); }
        if (indexNum.HasValue) { sets.Add("index_num = @i"); cmd.Parameters.AddWithValue("i", indexNum.Value); }
        if (sets.Count == 0) return (await ListGroupsAsync(ct)).FirstOrDefault(g => g.GroupId == groupId);
        cmd.CommandText = $"UPDATE enum.groups SET {string.Join(", ", sets)} WHERE group_id = @id RETURNING group_id, index_num, group_name";
        cmd.Parameters.AddWithValue("id", groupId);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new EnumDictionaryGroupDto(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2));
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("ENUM_GROUP_INDEX_CONFLICT");
        }
    }

    public override async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        if (await IsGroupReferencedInManifestsAsync(groupId, ct))
            throw new InvalidOperationException("ENUM_GROUP_IN_USE");
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM enum.groups WHERE group_id = @id";
        cmd.Parameters.AddWithValue("id", groupId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public override async Task<EnumDictionaryItemDto> CreateItemAsync(
        string name, int? indexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var idx = indexNum ?? await NextItemIndexAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO enum.items (index_num, name) VALUES (@i, @n) RETURNING index_num, name";
        cmd.Parameters.AddWithValue("i", idx);
        cmd.Parameters.AddWithValue("n", name);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new EnumDictionaryItemDto(reader.GetInt32(0), reader.GetString(1));
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Defense-in-depth: the action layer pre-checks index_num conflicts via GetItemAsync
            // before calling this, but a concurrent insert between check and write still hits
            // uq_enum_items_index (db/enum_tables.sql) -- translate it the same way
            // DeleteGroupAsync below translates ENUM_GROUP_IN_USE, rather than let a raw
            // PostgresException surface.
            throw new InvalidOperationException("ENUM_ITEM_INDEX_CONFLICT");
        }
    }

    public override async Task<EnumDictionaryItemDto?> GetItemAsync(
        int indexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT index_num, name FROM enum.items WHERE index_num = @i";
        cmd.Parameters.AddWithValue("i", indexNum);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new EnumDictionaryItemDto(reader.GetInt32(0), reader.GetString(1));
    }

    public override async Task<EnumDictionaryItemDto?> UpdateItemAsync(
        int indexNum, string? name, int? newIndexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        if (newIndexNum.HasValue && newIndexNum.Value != indexNum)
        {
            await using var moveCmd = conn.CreateCommand();
            moveCmd.CommandText = "UPDATE enum.items SET index_num = @new WHERE index_num = @old";
            moveCmd.Parameters.AddWithValue("new", newIndexNum.Value);
            moveCmd.Parameters.AddWithValue("old", indexNum);
            try
            {
                await moveCmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new InvalidOperationException("ENUM_ITEM_INDEX_CONFLICT");
            }
            catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                // Defense in depth: AdminRuntimeMasterRoster.DataEnumDictionaryUpdateItemAsync already
                // gates this with IsItemReferencedInGroupsAsync before reaching here (same constraint
                // DeleteItemAsync above guards), so this should not normally trigger.
                throw new InvalidOperationException("ENUM_ITEM_IN_USE");
            }
            indexNum = newIndexNum.Value;
        }

        if (name is not null)
        {
            await using var nameCmd = conn.CreateCommand();
            nameCmd.CommandText = "UPDATE enum.items SET name = @n WHERE index_num = @i RETURNING index_num, name";
            nameCmd.Parameters.AddWithValue("n", name);
            nameCmd.Parameters.AddWithValue("i", indexNum);
            await using var reader = await nameCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new EnumDictionaryItemDto(reader.GetInt32(0), reader.GetString(1));
        }

        await using var getCmd = conn.CreateCommand();
        getCmd.CommandText = "SELECT index_num, name FROM enum.items WHERE index_num = @i";
        getCmd.Parameters.AddWithValue("i", indexNum);
        await using var getReader = await getCmd.ExecuteReaderAsync(ct);
        if (!await getReader.ReadAsync(ct)) return null;
        return new EnumDictionaryItemDto(getReader.GetInt32(0), getReader.GetString(1));
    }

    public override async Task<bool> DeleteItemAsync(int indexNum, CancellationToken ct = default)
    {
        if (await IsItemReferencedInGroupsAsync(indexNum, ct))
            throw new InvalidOperationException("ENUM_ITEM_IN_USE");
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM enum.items WHERE index_num = @i";
        cmd.Parameters.AddWithValue("i", indexNum);
        try
        {
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("ENUM_ITEM_IN_USE");
        }
    }

    public override async Task<EnumDictionaryGroupDetailDto?> SetGroupItemsAsync(
        Guid groupId, IReadOnlyList<int> enumIndexNums, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var delCmd = conn.CreateCommand())
        {
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM enum.group_items WHERE group_id = @id";
            delCmd.Parameters.AddWithValue("id", groupId);
            await delCmd.ExecuteNonQueryAsync(ct);
        }

        try
        {
            for (var pos = 0; pos < enumIndexNums.Count; pos++)
            {
                await using var insCmd = conn.CreateCommand();
                insCmd.Transaction = tx;
                insCmd.CommandText =
                    "INSERT INTO enum.group_items (group_id, position, enum_index_num) VALUES (@g, @p, @e)";
                insCmd.Parameters.AddWithValue("g", groupId);
                insCmd.Parameters.AddWithValue("p", pos);
                insCmd.Parameters.AddWithValue("e", enumIndexNums[pos]);
                await insCmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // uq_enum_group_items_member (group_id, enum_index_num): the action layer
            // pre-checks enumIndexNums for duplicate values before calling this, but this stays
            // as defense-in-depth against the same input shape reaching the repository directly.
            throw new InvalidOperationException("ENUM_GROUP_ITEMS_DUPLICATE_MEMBERSHIP");
        }
        catch (PostgresException pe) when (pe.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // enum.group_items.enum_index_num REFERENCES enum.items(index_num): the action layer
            // pre-checks every provided index_num exists via GetItemAsync, but this stays as
            // defense-in-depth against a concurrently-deleted item.
            throw new InvalidOperationException("ENUM_ITEM_NOT_FOUND");
        }

        await tx.CommitAsync(ct);
        return await GetGroupDetailAsync(groupId, ct);
    }

    public override async Task<bool> IsGroupReferencedInManifestsAsync(
        Guid groupId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM manifest WHERE topology::text LIKE @p)";
        cmd.Parameters.AddWithValue("p", $"%{groupId}%");
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }

    public override async Task<bool> IsItemReferencedInGroupsAsync(
        int indexNum, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM enum.group_items WHERE enum_index_num = @i)";
        cmd.Parameters.AddWithValue("i", indexNum);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }

    private static async Task<int> NextGroupIndexAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(index_num), 0) + 1 FROM enum.groups";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<int> NextItemIndexAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(index_num), 0) + 1 FROM enum.items";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
