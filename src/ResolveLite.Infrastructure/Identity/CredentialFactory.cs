using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ResolveLite.Infrastructure.Identity;

/// <summary>
/// Deterministic credentials (Chunk 1 decision):
///   • Development → YOUR Entra sign-in (Azure CLI, then Visual Studio).
///   • Anywhere else → exactly ONE user-assigned managed identity, chosen by AZURE_CLIENT_ID.
/// No secrets, no guessing, no slow probing of credentials that cannot work.
/// </summary>
public static class CredentialFactory
{
    public static TokenCredential Create(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new VisualStudioCredential());
        }

        var clientId = configuration["AZURE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "AZURE_CLIENT_ID is not configured. Set it to the client ID of the " +
                "user-assigned managed identity attached to this app.");
        }

        return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
    }
}
