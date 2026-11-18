using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ResolveLite.Core.Documents;
using ResolveLite.Infrastructure.Storage;

namespace ResolveLite.Api.Documents;

public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapPost("/", UploadAsync);           // form + IFormFile → antiforgery enforced automatically
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetAsync);

        return group;
    }

    /// <summary>POST /api/documents  (multipart/form-data: file, category)</summary>
    private static async Task<Results<Created<DocumentDto>, ValidationProblem>> UploadAsync(
        IFormFile file,
        [FromForm] KnowledgeCategory category,
        IDocumentRepository repository,
        IOptions<StorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        // Old browsers may send a full path; keep only the name.
        var fileName = Path.GetFileName(file.FileName);

        // 1) Cheap checks first: extension and size (no bytes read yet).
        var errors = UploadRules.Validate(fileName, file.Length, storageOptions.Value.MaxUploadBytes);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        // 2) Content check: the file is small (≤ limit), so buffering it is safe.
        using var buffer = new MemoryStream(capacity: (int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);

        if (!UploadRules.IsValidUtf8(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
        {
            return ValidationFailed(["The file must be UTF-8 encoded text."]);
        }

        // 3) Store it.
        buffer.Position = 0;
        var document = await repository.CreateAsync(buffer, fileName, category, cancellationToken);

        return TypedResults.Created($"/api/documents/{document.Id}", DocumentDto.From(document));
    }

    /// <summary>GET /api/documents?category=Refunds  (streams a JSON array)</summary>
    private static Ok<IAsyncEnumerable<DocumentDto>> List(
        KnowledgeCategory? category,
        IDocumentRepository repository,
        CancellationToken cancellationToken)
        => TypedResults.Ok(ToDtos(repository.ListAsync(category, cancellationToken)));

    /// <summary>GET /api/documents/{id}</summary>
    private static async Task<Results<Ok<DocumentDto>, NotFound>> GetAsync(
        Guid id,
        IDocumentRepository repository,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetAsync(id, cancellationToken);
        return document is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(DocumentDto.From(document));
    }

    private static ValidationProblem ValidationFailed(IEnumerable<string> errors)
        => TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = errors.ToArray()
        });

    private static async IAsyncEnumerable<DocumentDto> ToDtos(IAsyncEnumerable<KnowledgeDocument> documents)
    {
        await foreach (var document in documents)
        {
            yield return DocumentDto.From(document);
        }
    }
}
