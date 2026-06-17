using System.Security.Cryptography;
using System.Text;

namespace Topolactor.Runtime;

/// <summary>
/// Consumer bundle step handler for file_storage_bundle operation_keys.
/// Registered via IExternalPortBundleStepHandler extension contract.
/// Only compute_checksum is handled here; domain record operations (record_export_job,
/// record_file_artifact, write_manifest_record, authorize_signed_download) execute via
/// execute_db_function operation_key through IExternalPortDbFunctionRepository.
/// </summary>
public sealed class FileStorageBundleStepHandler : IExternalPortBundleStepHandler
{
    private static readonly IReadOnlySet<string> _supportedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "compute_checksum"
    };

    public IReadOnlySet<string> SupportedOperationKeys => _supportedKeys;

    public Task ExecuteAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct = default) =>
        step.OperationKey switch
        {
            "compute_checksum" => ComputeChecksumAsync(step, context, ct),
            _ => throw new InvalidOperationException("EXTERNAL_PORT_POLICY_OPERATION_UNSUPPORTED")
        };

    private static Task ComputeChecksumAsync(ExternalPortPolicyStep step, ExternalPortExecutionContext context, CancellationToken ct)
    {
        var input = context.HttpResponse?.Body;
        if (string.IsNullOrEmpty(input))
            throw new InvalidOperationException("CHECKSUM_INPUT_REQUIRED");

        context.ChecksumValue = ComputeSha256(input);
        context.MarkExecuted(step.OperationKey);
        return Task.CompletedTask;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
