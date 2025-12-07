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

            // Try KV v2 first (most common). If that fails, fall back to KV v1.
            try
            {
                var secret = await client.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(secretPath.Replace("secret/data/", ""), mountPoint: "secret");

                return secret?.Data?.Data != null
                    ? new Dictionary<string, object>(secret.Data.Data)
                    : new Dictionary<string, object>();
            }
            catch (Exception v2Ex)
            {
                Console.WriteLine($"KV v2 read failed ({v2Ex.Message.Trim()}); attempting KV v1 fallback.");

                // Try KV v1 path
                try
                {
                    var secretV1 = await client.V1.Secrets.KeyValue.V1
                        .ReadSecretAsync(secretPath.Replace("secret/data/", ""), mountPoint: "secret");

                    return secretV1?.Data != null
                        ? new Dictionary<string, object>(secretV1.Data)
                        : new Dictionary<string, object>();
                }
                catch (Exception v1Ex)
                {
                    // Re-throw a clearer message for callers
                    throw new Exception($"Failed to read secret at '{secretPath}' using KV v2 and KV v1. v2: {v2Ex.Message}; v1: {v1Ex.Message}");
                }
            }
        }

        // Demo helper: fetch a single value by path and key (e.g. path="demo/db", key="username")
        public static async Task<string?> GetSecretValueAsync(string path, string key)
        {
            var client = VaultClientFactory.Create();

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("path must be provided", nameof(path));

            // Try KV v2 first
            try
            {
                var secret = await client.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path.Replace("secret/data/", ""), mountPoint: "secret");

                if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value) && value != null)
                    return value.ToString();
            }
            catch (Exception v2Ex)
            {
                Console.WriteLine($"KV v2 read failed for '{path}' ({v2Ex.Message.Trim()}); attempting KV v1 fallback.");
            }

            // Fallback to KV v1
            try
            {
                var secretV1 = await client.V1.Secrets.KeyValue.V1
                    .ReadSecretAsync(path.Replace("secret/data/", ""), mountPoint: "secret");

                if (secretV1?.Data != null && secretV1.Data.TryGetValue(key, out var v1Value) && v1Value != null)
                    return v1Value.ToString();
            }
            catch (Exception v1Ex)
            {
                Console.WriteLine($"KV v1 read failed for '{path}' ({v1Ex.Message.Trim()}).");
            }

            return null;
        }
    }
}
