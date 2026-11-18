using System.ComponentModel.DataAnnotations;

namespace ResolveLite.Infrastructure.Storage;

/// <summary>
/// Bound from the "Storage" configuration section.
/// Contains ENDPOINTS and limits only, never keys or connection strings.
/// </summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>e.g. https://stresolvelitedevcin001.blob.core.windows.net/</summary>
    [Required, Url]
    public string BlobEndpoint { get; init; } = default!;

    [Required]
    public string Container { get; init; } = "knowledge";

    [Range(1, 10)]
    public int MaxUploadMb { get; init; } = 1;

    public long MaxUploadBytes => MaxUploadMb * 1024L * 1024L;
}
