using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ResolveLite.Infrastructure.Storage;

/// <summary>
/// READINESS check: "can this replica reach and use Blob Storage right now?"
/// Used by /health/ready only. Never by liveness (Chunk 2: a storage outage must
/// stop traffic to the replica, not restart it in a loop).
/// </summary>
public sealed class BlobStorageHealthCheck(
    BlobServiceClient serviceClient,
    IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var containerName = options.Value.Container;
        try
        {
            var exists = await serviceClient
                .GetBlobContainerClient(containerName)
                .ExistsAsync(cancellationToken);

            return exists.Value
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Blob container '{containerName}' was not found.");
        }
        catch (Exception ex)
        {
            // Covers network failures AND 403s (missing data-plane role).
            return HealthCheckResult.Unhealthy("Blob Storage is unreachable or access was denied.", ex);
        }
    }
}
