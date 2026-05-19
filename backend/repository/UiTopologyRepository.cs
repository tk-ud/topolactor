using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Repository for ui_topology_tables: ui_component_bucket and the
/// package-generation pipeline tables defined in db/ui_topology_tables.sql.
///
/// No in-memory skeleton or no-op fallback: both methods throw NotImplementedException
/// to make unintended injection an explicit failure rather than a silent fake success.
///
/// Production wiring: NpgsqlUiTopologyRepository overrides both methods.
/// Test stubs: override both methods in the test class.
/// </summary>
public class UiTopologyRepository
{
    protected readonly ILogger<UiTopologyRepository> _logger;
    protected readonly string _connectionString;

    public UiTopologyRepository(ILogger<UiTopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Lists bucket items with the given status (default 'bucketed').
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<IReadOnlyList<UiComponentBucketRecord>> ListBucketItemsAsync(
        string status = "bucketed",
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.ListBucketItemsAsync must be overridden by a production implementation.");
    }

    /// <summary>
    /// Atomically promotes a bucket item from 'bucketed' to 'promoted' by:
    ///   1. SELECT + UPDATE status bucketed->packaging (fail fast if not bucketed)
    ///   2. INSERT ui_component_registry, ui_component_package, ui_package_component_map,
    ///      ui_layout_registry, ui_wiring_registry, ui_topology_tensor
    ///   3. UPDATE status packaging->promoted (verify rows==1; fail if not)
    ///   All steps execute in a single DB connection + transaction.
    ///   On any failure the transaction is rolled back — no partial state in DB.
    ///
    /// Key derivation:
    ///   component_key = bucket.component_key (verbatim)
    ///   package_key   = "{routeKey}:{bucket.component_key}:pkg"
    ///   layout_key    = "{routeKey}:{bucket.component_key}:layout"
    ///   wiring_key    = "{routeKey}:{bucket.component_key}:wiring"
    ///
    /// Returns PackageGenerateCode.NotFound         when bucket item does not exist.
    /// Returns PackageGenerateCode.NotBucketed      when bucket item is not in 'bucketed' status.
    /// Returns PackageGenerateCode.ConstraintViolation when a unique key conflict occurs.
    /// Returns PackageGenerateCode.PromotionFailed  when final promoted update returns 0 rows.
    /// Returns PackageGenerateCode.DbUnavailable    when DB connection/transaction fails.
    /// Returns PackageGenerateCode.Success          with all issued IDs on success.
    ///
    /// Production: overridden by NpgsqlUiTopologyRepository.
    /// </summary>
    public virtual Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId,
        string routeKey,
        CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "UiTopologyRepository.PromoteBucketItemAsync must be overridden by a production implementation.");
    }
}
