namespace ResolveLite.Core.Documents;

/// <summary>
/// WHAT the app needs from document storage. Core never knows HOW.
/// Phase 1 implementation: Blob Storage + metadata (Infrastructure, sub-chunk 6.3).
/// Phase 3 implementation: PostgreSQL. The API layer never changes.
/// </summary>
public interface IDocumentRepository
{
    Task<KnowledgeDocument> CreateAsync(
        Stream content,
        string originalFileName,
        KnowledgeCategory category,
        CancellationToken cancellationToken);

    Task<KnowledgeDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

    IAsyncEnumerable<KnowledgeDocument> ListAsync(
        KnowledgeCategory? category,
        CancellationToken cancellationToken);
}
