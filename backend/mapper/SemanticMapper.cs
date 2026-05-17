using Topolactor.Schema;

namespace Topolactor.Mapper;

/// <summary>
/// Maps a RuntimeWorkingShape to a RepositoryCommand.
/// The mapper translates semantic meaning from the resolved working shape into
/// a command that the repository layer can act upon.
/// This is not an ORM mapper — it maps domain meaning, not object graphs.
/// </summary>
public class SemanticMapper
{
    /// <summary>
    /// Maps a resolved RuntimeWorkingShape to a RepositoryCommand.
    /// Stub implementation: returns a command populated with available shape fields.
    /// </summary>
    public RepositoryCommand MapToRepositoryCommand(RuntimeWorkingShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return new RepositoryCommand(
            StructureMapId: shape.StructureMapId,
            PackageId: shape.PackageId,
            SchemaId: shape.SchemaId,
            Action: shape.Vector?.Action,
            Target: shape.Vector?.Target,
            Layer: shape.Vector?.Layer,
            Payload: shape.Vector?.Payload
        );
    }
}

/// <summary>
/// Represents a semantic repository command derived from a resolved working shape.
/// Not an ORM entity — encodes domain intent.
/// </summary>
internal record RepositoryCommand(
    string? StructureMapId,
    Guid? PackageId,
    Guid? SchemaId,
    string? Action,
    string? Target,
    string? Layer,
    System.Text.Json.JsonElement? Payload
);
