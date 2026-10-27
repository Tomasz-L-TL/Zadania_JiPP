using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Week08.Api.Options;

namespace Week08.Api.Diagnostics;

public sealed class WritableStorageHealthCheck(
    IOptions<DiagnosticsOptions> options, IHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var configured = options.Value.StorageDirectory;
        if (string.IsNullOrWhiteSpace(configured))
            return HealthCheckResult.Unhealthy("Storage dependency is unavailable.");

        var directory = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);

        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".probe-{Guid.NewGuid():N}");

            // CreateNew and DeleteOnClose prevent stale health-check files.
            await using var stream = new FileStream(
                probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await stream.WriteAsync(new byte[] { 42 }, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            return HealthCheckResult.Healthy("Storage dependency is writable.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                    or ArgumentException or NotSupportedException)
        {
            // No filesystem paths or exception descriptions are returned to clients.
            return HealthCheckResult.Unhealthy("Storage dependency is unavailable.");
        }
    }
}
