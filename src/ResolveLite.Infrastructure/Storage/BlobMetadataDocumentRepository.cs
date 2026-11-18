using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ResolveLite.Core.Documents;

namespace ResolveLite.Infrastructure.Storage;

/// <summary>
/// Phase 1 implementation of <see cref="IDocumentRepository"/>:
///   file content → block blob   documents/{id}/v{version}{ext}
///   document state → blob metadata (status, category, …)
/// Replaced by PostgreSQL in Phase 3 without touching the API layer.
/// </summary>
public sealed class BlobMetadataDocumentRepository(
    BlobServiceClient serviceClient,
    IOptions<StorageOptions> options,
    TimeProvider clock) : IDocumentRepository
{
    private const string DocumentsPrefix = "documents/";

    private BlobContainerClient Container => serviceClient.GetBlobContainerClient(options.Value.Container);

    public async Task<KnowledgeDocument> CreateAsync(
        Stream content,
        string originalFileName,
        KnowledgeCategory category,
        CancellationToken cancellationToken)
    {
        const int version = 1;
        var id = Guid.NewGuid();
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        // Server-generated name: the user's file name is DATA, never part of the path.
        var blobName = $"{DocumentsPrefix}{id:N}/v{version}{extension}";

        var document = new KnowledgeDocument(
            Id: id,
            OriginalFileName: originalFileName,
            Category: category,
            Status: DocumentStatus.Pending,
            Version: version,
            BlobName: blobName,
            UploadedAt: clock.GetUtcNow());

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = extension == ".md" ? "text/markdown; charset=utf-8" : "text/plain; charset=utf-8"
            },
            Metadata = DocumentMetadata.ToMetadata(document),

            // "Create only if it does not exist": never silently overwrite a blob.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        };

        await Container.GetBlobClient(blobName).UploadAsync(content, uploadOptions, cancellationToken);

        return document;
    }

    public async Task<KnowledgeDocument?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        // Phase 1 stores a single version per document under documents/{id}/
        await foreach (var item in Container.GetBlobsAsync(
                           traits: BlobTraits.Metadata,
                           states: BlobStates.All,
                           prefix: $"{DocumentsPrefix}{id:N}/",
                           cancellationToken: cancellationToken))
        {
            return DocumentMetadata.FromMetadata(item.Name, item.Metadata);
        }

        return null;
    }

    public async IAsyncEnumerable<KnowledgeDocument> ListAsync(
        KnowledgeCategory? category,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // BlobTraits.Metadata is REQUIRED: without it, item.Metadata comes back null.
        await foreach (var item in Container.GetBlobsAsync(
                           traits: BlobTraits.Metadata,
                            states: BlobStates.All,
                           prefix: DocumentsPrefix,
                           cancellationToken: cancellationToken))
        {
            var document = DocumentMetadata.FromMetadata(item.Name, item.Metadata);
            if (document is not null && (category is null || document.Category == category))
            {
                yield return document;
            }
        }
    }
}
