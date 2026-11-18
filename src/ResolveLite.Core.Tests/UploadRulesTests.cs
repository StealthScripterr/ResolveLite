using ResolveLite.Core.Documents;

namespace ResolveLite.Core.Tests;

public class UploadRulesTests
{
    [Theory]
    [InlineData("refunds.md")]
    [InlineData("account-access.txt")]
    [InlineData("FAQ.TXT")]                 // extension check is case-insensitive
    public void Accepts_text_and_markdown_files(string fileName)
    {
        Assert.Empty(UploadRules.Validate(fileName, length: 2_048));
    }

    [Theory]
    [InlineData("policy.pdf")]
    [InlineData("refunds.md.exe")]          // double-extension trick
    [InlineData("no-extension")]
    public void Rejects_other_file_types(string fileName)
    {
        Assert.NotEmpty(UploadRules.Validate(fileName, length: 2_048));
    }

    [Fact]
    public void Rejects_empty_files()
    {
        Assert.NotEmpty(UploadRules.Validate("refunds.md", length: 0));
    }

    [Fact]
    public void Rejects_files_over_the_limit()
    {
        Assert.NotEmpty(UploadRules.Validate("refunds.md", UploadRules.DefaultMaxBytes + 1));
    }

    [Fact]
    public void Accepts_utf8_text_with_accents()
    {
        Assert.True(UploadRules.IsValidUtf8("Rückerstattung innerhalb von 14 Tagen"u8));
    }

    [Fact]
    public void Rejects_bytes_that_are_not_utf8()
    {
        byte[] notUtf8 = [0xC3, 0x28];     // an invalid 2-byte sequence
        Assert.False(UploadRules.IsValidUtf8(notUtf8));
    }
}
