using System.Text.Unicode;

namespace ResolveLite.Core.Documents;

/// <summary>
/// Business rules for an uploaded FAQ file. Pure functions: no I/O, no Azure,
/// so they are trivially unit-testable.
/// </summary>
public static class UploadRules
{
    /// <summary>Default upload limit: 1 MB (FAQs are small text files).</summary>
    public const long DefaultMaxBytes = 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md" };

    /// <summary>Returns every problem found; an empty list means the file is acceptable.</summary>
    public static IReadOnlyList<string> Validate(string fileName, long length, long maxBytes = DefaultMaxBytes)
    {
        var errors = new List<string>();

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            errors.Add("Only .txt and .md files are accepted.");
        }

        if (length <= 0)
        {
            errors.Add("The file is empty.");
        }
        else if (length > maxBytes)
        {
            errors.Add($"The file exceeds the {maxBytes / 1024} KB limit.");
        }

        return errors;
    }

    /// <summary>True when the bytes are well-formed UTF-8 text.</summary>
    public static bool IsValidUtf8(ReadOnlySpan<byte> content) => Utf8.IsValid(content);
}
