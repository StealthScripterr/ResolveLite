namespace ResolveLite.Core.Documents;

/// <summary>One uploaded FAQ file and where it is in its lifecycle.</summary>
public sealed record KnowledgeDocument(
    Guid Id,
    string OriginalFileName,
    KnowledgeCategory Category,
    DocumentStatus Status,
    int Version,
    string BlobName,
    DateTimeOffset UploadedAt,
    string? FailureReason = null);
