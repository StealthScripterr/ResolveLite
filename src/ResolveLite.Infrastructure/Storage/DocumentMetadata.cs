using System.Buffers.Text;
using System.Globalization;
using System.Text;
using ResolveLite.Core.Documents;

namespace ResolveLite.Infrastructure.Storage;

/// <summary>
/// Maps a <see cref="KnowledgeDocument"/> to blob metadata and back.
/// Metadata travels as HTTP headers, so:
///   • keys must be valid C# identifiers (we use lowercase),
///   • values must be header-safe ASCII → free text (file names) is Base64url-encoded.
/// </summary>
internal static class DocumentMetadata
{
    private const string DocumentId = "documentid";
    private const string FileName = "filename";
    private const string Category = "category";
    private const string Status = "status";
    private const string Version = "version";
    private const string UploadedAt = "uploadedat";
    private const string FailureReason = "failurereason";

    public static Dictionary<string, string> ToMetadata(KnowledgeDocument document)
    {
        var metadata = new Dictionary<string, string>
        {
            [DocumentId] = document.Id.ToString("N"),
            [FileName] = Encode(document.OriginalFileName),
            [Category] = document.Category.ToString(),
            [Status] = document.Status.ToString(),
            [Version] = document.Version.ToString(CultureInfo.InvariantCulture),
            [UploadedAt] = document.UploadedAt.ToString("O", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrEmpty(document.FailureReason))
        {
            metadata[FailureReason] = Encode(document.FailureReason);
        }

        return metadata;
    }

    /// <summary>Returns null for blobs that are not ResolveLite documents (or are malformed).</summary>
    public static KnowledgeDocument? FromMetadata(string blobName, IDictionary<string, string>? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var m = new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);

        if (!m.TryGetValue(DocumentId, out var idText) || !Guid.TryParseExact(idText, "N", out var id) ||
            !m.TryGetValue(FileName, out var encodedName) ||
            !m.TryGetValue(Category, out var categoryText) || !Enum.TryParse<KnowledgeCategory>(categoryText, out var category) ||
            !m.TryGetValue(Status, out var statusText) || !Enum.TryParse<DocumentStatus>(statusText, out var status) ||
            !m.TryGetValue(Version, out var versionText) ||
            !int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ||
            !m.TryGetValue(UploadedAt, out var uploadedText) ||
            !DateTimeOffset.TryParse(uploadedText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var uploadedAt))
        {
            return null;
        }

        var failureReason = m.TryGetValue(FailureReason, out var encodedReason) ? Decode(encodedReason) : null;

        return new KnowledgeDocument(id, Decode(encodedName), category, status, version, blobName, uploadedAt, failureReason);
    }

    private static string Encode(string text) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));

    private static string Decode(string value) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
}
