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
    }
}
