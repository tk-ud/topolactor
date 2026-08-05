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
