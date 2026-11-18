using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ResolveLite.Core.Documents;
using ResolveLite.Infrastructure.Identity;
using ResolveLite.Infrastructure.Storage;

namespace ResolveLite.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything Azure-specific. The API calls this ONE method and
    /// never needs to know which SDKs or credentials are involved.
    /// </summary>
    public static IServiceCollection AddResolveLiteInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Typed, validated settings. A missing endpoint fails at STARTUP (fail fast).
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Azure SDK clients: registered once, as singletons, sharing one credential.
        services.AddAzureClients(clients =>
        {
            clients.UseCredential(CredentialFactory.Create(environment, configuration));

            clients.AddClient<BlobServiceClient, BlobClientOptions>((clientOptions, credential, provider) =>
            {
                var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
                return new BlobServiceClient(new Uri(storage.BlobEndpoint), credential, clientOptions);
            });
        });

        services.AddSingleton<IDocumentRepository, BlobMetadataDocumentRepository>();

        return services;
    }
}
