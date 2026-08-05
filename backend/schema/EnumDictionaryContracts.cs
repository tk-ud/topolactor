using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public record EnumDictionaryGroupDto(
    [property: JsonPropertyName("groupId")] Guid GroupId,
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("groupName")] string GroupName);

public record EnumDictionaryItemDto(
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("name")] string Name);

public record EnumDictionaryGroupDetailDto(
    [property: JsonPropertyName("groupId")] Guid GroupId,
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("groupName")] string GroupName,
    [property: JsonPropertyName("items")] IReadOnlyList<EnumDictionaryItemDto> Items,
    [property: JsonPropertyName("itemsIndexNums")] IReadOnlyList<int> ItemsIndexNums);

// admin-enum subBundle closure round (.agent/tasks/todo.md): items-browse UX composed into
// list_groups' OWN parent read (the existing enum_dictionary:get_group item-join folded into
// list_groups' own query) -- no new list_items action, no cross-manifest dispatch, no direct
// child-Emission adoption. itemsSummary is a flattened, human-readable "index:name, ..." string
// (empty for a group with no items) rendered as an ordinary enum_table column.
public record EnumDictionaryGroupWithItemsSummaryDto(
    [property: JsonPropertyName("groupId")] Guid GroupId,
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("groupName")] string GroupName,
    [property: JsonPropertyName("itemsSummary")] string ItemsSummary);

public record EnumDictionaryGetGroupRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId);

// generic list_groups search/filter (admin-enum subBundle closure round; extended round 36 to
// the owning SSOT's full declared search/filter target field set -- docs/design/
// admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.enum.
// capability_requirements.search/filter): two data-defined OPTIONAL payload fields on the
// EXISTING list_groups read action, never a new action or enum-specific lane.
// Search (free-text substring match) covers ALL FIVE owning-SSOT-declared search target fields
// across enum.groups/enum.items/enum.group_items -- see
// NpgsqlEnumDictionaryRepository.ListGroupsWithItemsSummaryAsync for the exact per-field match
// shape (group_name/groups.index_num on the group row itself; items.name/items.index_num/
// group_items.position on ANY member item, matching the group that contains it). Absent/null/
// empty Search means no filter (the canonical full list).
// GroupIdFilter (exact match, the enum_group_filter select control's own selected groupId) scopes
// the roster to a single group -- the owning SSOT's declared filter target fields
// (groups.group_name/groups.index_num identify the group; group_items.position is inherently
// preserved by itemsSummary's existing position-ordering once scoped to that one group, not a
// second independent filter input). Absent/null means no group scoping (the canonical full list).
// A payload key present but carrying a non-string JSON value fails the request's own JSON
// deserialization (System.Text.Json raises before this record is ever constructed), so
// DataEnumDictionaryListGroupsAsync's own malformed-payload branch already fails closed for that
// case -- no extra check needed here. A present-but-non-UUID-parseable GroupIdFilter string is
// checked explicitly by DataEnumDictionaryListGroupsAsync (JSON deserialization alone cannot
// catch that -- a non-UUID string is still valid JSON).
public record EnumDictionaryListGroupsRequestDto(
    [property: JsonPropertyName("search")] string? Search,
    [property: JsonPropertyName("groupIdFilter")] string? GroupIdFilter);
