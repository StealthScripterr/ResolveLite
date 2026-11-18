using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ResolveLite.Core.Documents;

namespace ResolveLite.Api.IntegrationTests;

/// <summary>
/// Test double for IDocumentRepository: lets us test the HTTP layer
/// (validation, antiforgery, status codes) with no Azure and no Docker.
/// This swap is only possible because the API depends on the INTERFACE.
/// </summary>
public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, (KnowledgeDocument Document, byte[] Content)> _items = new();

    public IReadOnlyCollection<KnowledgeDocument> Documents => _items.Values.Select(v => v.Document).ToList();

    public byte[] ContentOf(Guid id) => _items[id].Content;

    public async Task<KnowledgeDocument> CreateAsync(
        Stream content, string originalFileName, KnowledgeCategory category, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var id = Guid.NewGuid();
        var document = new KnowledgeDocument(
            id, originalFileName, category, DocumentStatus.Pending, 1,
            $"documents/{id:N}/v1{Path.GetExtension(originalFileName)}", DateTimeOffset.UtcNow);

        _items[id] = (document, buffer.ToArray());
        return document;
    }

    public Task<KnowledgeDocument?> GetAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult<KnowledgeDocument?>(_items.TryGetValue(id, out var item) ? item.Document : null);

    public async IAsyncEnumerable<KnowledgeDocument> ListAsync(
        KnowledgeCategory? category, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        foreach (var (document, _) in _items.Values)
        {
            if (category is null || document.Category == category)
            {
                yield return document;
            }
        }
    }
}
