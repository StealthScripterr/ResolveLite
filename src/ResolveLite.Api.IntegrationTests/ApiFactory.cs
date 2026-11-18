using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResolveLite.Core.Documents;

namespace ResolveLite.Api.IntegrationTests;

/// <summary>
/// Boots the real ResolveLite pipeline in memory, with Azure swapped out:
///   • IDocumentRepository → InMemoryDocumentRepository
///   • the Blob Storage readiness check → removed
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public InMemoryDocumentRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Storage:BlobEndpoint", "https://unit-test.blob.core.windows.net/");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentRepository>();
            services.AddSingleton<IDocumentRepository>(Repository);

            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var blobCheck = options.Registrations.FirstOrDefault(r => r.Name == "blob-storage");
                if (blobCheck is not null)
                {
                    options.Registrations.Remove(blobCheck);
                }
            });
        });
    }
}
