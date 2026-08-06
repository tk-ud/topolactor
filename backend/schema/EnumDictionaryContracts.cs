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
// itemsIndexNums (round 38): the SAME membership, as a bare comma-separated index-num list
// (empty string for a group with no items) -- the exact CSV shape
// AdminRuntimeMasterRoster.TryParseSetGroupItemsPayload's own string branch already accepts for
// enumIndexNums. This is what makes the selected group's CURRENT membership available as a real,
// generic node:enum_table.value.itemsIndexNums prefill source for enum_set_group_items_input
// (the same node-value-prefill mechanism enum_update_group_name_input's
// propBindings.value.source="node:enum_table.value.groupName" already uses) -- without it, a user
// editing group membership had to blindly retype the full member list from scratch, since
// itemsSummary's "index:name, ..." display text is not itself valid enumIndexNums input.
public record EnumDictionaryGroupWithItemsSummaryDto(
    [property: JsonPropertyName("groupId")] Guid GroupId,
    [property: JsonPropertyName("indexNum")] int IndexNum,
    [property: JsonPropertyName("groupName")] string GroupName,
    [property: JsonPropertyName("itemsSummary")] string ItemsSummary,
    [property: JsonPropertyName("itemsIndexNums")] string ItemsIndexNums);

public record EnumDictionaryGetGroupRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId);

// generic list_groups search/filter (admin-enum subBundle closure round; extended round 36 to the
// owning SSOT's full declared search target field set, and round 37 to the owning SSOT's own
// declared FILTER target field set -- docs/design/admin-normal-surface-projection-seed-ssot.yaml
// surface_axes.admin.surfaces.enum.capability_requirements.search/filter): data-defined OPTIONAL
// payload fields on the EXISTING list_groups read action, never a new action or enum-specific
// lane.
// Search (free-text substring match) covers ALL FIVE owning-SSOT-declared search target fields
// across enum.groups/enum.items/enum.group_items -- see
// NpgsqlEnumDictionaryRepository.ListGroupsWithItemsSummaryAsync for the exact per-field match
// shape. Absent/null/empty Search means no filter (the canonical full list).
// GroupNameFilter/GroupIndexNumFilter/ItemPositionFilter (round 37) are the owning SSOT's own
// THREE declared filter target fields (enum.groups.group_name, enum.groups.index_num,
// enum.group_items.position) as three independent, combinable, exact-match payload fields --
// replacing round 36's GroupIdFilter (an exact-match on enum.groups.group_id, a field the owning
// SSOT never actually declares as part of capability_requirements.filter; keeping it would have
// been a substitute for the declared fields, not an implementation of them). The
// enum_group_filter select control drives GroupNameFilter (its own option value IS the group's
// name now, not an opaque id -- see propBindings.options.valuePath="groupName" on that node).
// GroupIndexNumFilter/ItemPositionFilter are declared-field-complete backend capability with no
// dedicated UI control yet (same shape as Search's five fields, all reachable through one input).
// Absent/null on any of the three means that field does not narrow the result (the canonical full
// list when none are supplied). A payload key present but carrying a non-string/non-integer JSON
// value fails the request's own JSON deserialization (System.Text.Json raises before this record
// is ever constructed), so DataEnumDictionaryListGroupsAsync's own malformed-payload branch
// already fails closed for that case -- no extra check needed here.
public record EnumDictionaryListGroupsRequestDto(
    [property: JsonPropertyName("search")] string? Search,
    [property: JsonPropertyName("groupNameFilter")] string? GroupNameFilter,
    [property: JsonPropertyName("groupIndexNumFilter")] int? GroupIndexNumFilter,
    [property: JsonPropertyName("itemPositionFilter")] int? ItemPositionFilter);

// round 37: list_groups' full response envelope -- Groups is the (possibly search/filter-
// narrowed) roster enum_table's own rows bind to; GroupOptions is the SAME shape ListGroupsAsync
// already returns (group_id/index_num/group_name, always unfiltered) so enum_group_filter's own
// select options never self-narrow to whatever the CURRENT search/filter happens to match --
// closing the options-self-shrinking gap the shared single-array response shape had (both rows
// and options previously read the SAME "emission.data" array, so filtering the roster also
// filtered the filter control's own choices, including making it impossible to pick a different
// group, or leaving zero options, once any filter narrowed the result to nothing).
public record EnumDictionaryListGroupsResponseDto(
    [property: JsonPropertyName("groups")] IReadOnlyList<EnumDictionaryGroupWithItemsSummaryDto> Groups,
    [property: JsonPropertyName("groupOptions")] IReadOnlyList<EnumDictionaryGroupDto> GroupOptions);
