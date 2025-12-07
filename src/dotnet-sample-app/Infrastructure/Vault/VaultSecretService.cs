using VaultSharp;

namespace dotnet_sample_app.Infrastructure.Vault
{
    public class VaultSecretService
    {
        public static async Task<Dictionary<string, object>> GetSecretsAsync()
        {
            var client = VaultClientFactory.Create();

            var secretPath = Environment.GetEnvironmentVariable("VAULT_SECRET_PATH");

            if (string.IsNullOrWhiteSpace(secretPath))
                throw new Exception("VAULT_SECRET_PATH environment variable is not set");

            var secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(secretPath.Replace("secret/data/", ""), mountPoint: "secret");

            return new Dictionary<string, object>(secret.Data.Data);
        }

        // Demo helper: fetch a single value by path and key (e.g. path="demo/db", key="username")
        public static async Task<string?> GetSecretValueAsync(string path, string key)
        {
            var client = VaultClientFactory.Create();

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("path must be provided", nameof(path));

            var secret = await client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path.Replace("secret/data/", ""), mountPoint: "secret");

            if (secret?.Data?.Data == null)
                return null;

            if (!secret.Data.Data.TryGetValue(key, out var value) || value == null)
                return null;

            return value.ToString();
        }
    }
}
