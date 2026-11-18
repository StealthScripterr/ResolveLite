using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ResolveLite.Api.IntegrationTests;

public class DocumentEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string RefundPolicy =
        "# Refund policy\nCancelled subscriptions can be refunded within 14 days of the last payment.\n";

    [Fact]
    public async Task Upload_valid_markdown_returns_201_with_Pending_status()
    {
        var client = await CreateClientWithXsrfTokenAsync();

        var response = await client.PostAsync("/api/documents",
            Form(Encoding.UTF8.GetBytes(RefundPolicy), "refund-policy.md", "Refunds"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal("Refunds", body.GetProperty("category").GetString());
        Assert.Equal("refund-policy.md", body.GetProperty("fileName").GetString());
        Assert.False(body.TryGetProperty("blobName", out _));   // storage layout must not leak
    }

    [Fact]
    public async Task Upload_pdf_returns_400_validation_problem()
    {
        var client = await CreateClientWithXsrfTokenAsync();

        var response = await client.PostAsync("/api/documents",
            Form(Encoding.UTF8.GetBytes("%PDF-1.7"), "policy.pdf", "Refunds"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("file", out _));
    }

    [Fact]
    public async Task Upload_non_utf8_content_returns_400()
    {
        var client = await CreateClientWithXsrfTokenAsync();

        var response = await client.PostAsync("/api/documents",
            Form([0xC3, 0x28, 0xFF], "broken.txt", "Delivery"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_without_antiforgery_token_is_rejected()
    {
        var client = factory.CreateClient();   // no XSRF header

        var response = await client.PostAsync("/api/documents",
            Form(Encoding.UTF8.GetBytes(RefundPolicy), "refund-policy.md", "Refunds"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploaded_document_appears_in_list_filtered_by_category()
    {
        var client = await CreateClientWithXsrfTokenAsync();
        var created = await client.PostAsync("/api/documents",
            Form(Encoding.UTF8.GetBytes("Parcels arrive in 3–5 days."), "delivery.txt", "Delivery"));
        created.EnsureSuccessStatusCode();

        var delivery = await client.GetFromJsonAsync<JsonElement>("/api/documents?category=Delivery");

        Assert.Contains(delivery.EnumerateArray(),
            d => d.GetProperty("fileName").GetString() == "delivery.txt");
        Assert.All(delivery.EnumerateArray(),
            d => Assert.Equal("Delivery", d.GetProperty("category").GetString()));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Does what Angular does at startup: GET the token endpoint, read the
    /// XSRF-TOKEN cookie, and send it back as the X-XSRF-TOKEN header.
    /// The antiforgery cookie itself is kept by the client's cookie container.
    /// </summary>
    private async Task<HttpClient> CreateClientWithXsrfTokenAsync()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();

        var xsrfCookie = response.Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';', 2)[0])
            .First(pair => pair.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN",
            Uri.UnescapeDataString(xsrfCookie["XSRF-TOKEN=".Length..]));
        return client;
    }

    private static MultipartFormDataContent Form(byte[] content, string fileName, string category)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(category), "category" }
        };
    }
}
