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

public record EnumDictionaryGetGroupRequestDto(
    [property: JsonPropertyName("groupId")] string GroupId);
