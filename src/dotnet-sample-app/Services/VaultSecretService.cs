using System;
using System.Threading.Tasks;
using VaultSharp;
using dotnet_sample_app.Infrastructure.Vault;

namespace dotnet_sample_app.Services
{
    public class VaultSecretService
    {
        private readonly IVaultClient _vaultClient;

        public VaultSecretService()
        {
            // Use the central factory so we support multiple VaultSharp versions via reflection
            _vaultClient = VaultClientFactory.Create();
        }

        public async Task<string?> GetSecretAsync(string path, string key)
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path);

            if (secret?.Data?.Data == null)
                return null;

            if (!secret.Data.Data.TryGetValue(key, out var value) || value == null)
                return null;

            return value.ToString();
        }
    }
}
