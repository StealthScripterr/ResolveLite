using ResolveLite.Core.Documents;

namespace ResolveLite.Api.Documents;

/// <summary>
/// What the API returns. Deliberately omits BlobName: storage layout is an
/// internal detail and must not leak into the public contract.
/// </summary>
public sealed record DocumentDto(
    Guid Id,
    string FileName,
    KnowledgeCategory Category,
    DocumentStatus Status,
    int Version,
    DateTimeOffset UploadedAt,
    string? FailureReason)
{
    public static DocumentDto From(KnowledgeDocument document) => new(
        document.Id,
        document.OriginalFileName,
        document.Category,
        document.Status,
        document.Version,
        document.UploadedAt,
        document.FailureReason);
}
