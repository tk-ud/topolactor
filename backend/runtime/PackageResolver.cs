using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Resolves the package definition from the repository using the PackageId on the working shape.
/// Returns an updated RuntimeWorkingShape with PackageDef populated.
/// An error is returned (via exception) if PackageId is missing or the package is not found.
/// </summary>
public class PackageResolver
{
    private readonly TopologyRepository _topologyRepository;

    public PackageResolver(TopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// Loads the package for the given shape. Throws if PackageId is missing or not found.
    /// No silent fallback.
    /// </summary>
    public async Task<RuntimeWorkingShape> Resolve(RuntimeWorkingShape shape, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.PackageId is null)
            throw new InvalidOperationException(
                "Cannot resolve package: PackageId is null on RuntimeWorkingShape.");

        var record = await _topologyRepository.LoadPackageAsync(shape.PackageId.Value, ct);

        if (record is null)
            throw new InvalidOperationException(
                $"Package '{shape.PackageId}' not found. Broken reference — no fallback.");

        return shape with { PackageDef = record };
    }
}
