using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AWS;

namespace dotnet_sample_app.Infrastructure.Vault
{
    public static class VaultClientFactory
    {
        public static IVaultClient Create()
        {
            var vaultAddress = Environment.GetEnvironmentVariable("VAULT_ADDR");
            var role = Environment.GetEnvironmentVariable("VAULT_ROLE");

            if (string.IsNullOrWhiteSpace(vaultAddress))
                throw new Exception("VAULT_ADDR environment variable is not set");

            if (string.IsNullOrWhiteSpace(role))
                throw new Exception("VAULT_ROLE environment variable is not set");

            // Prefer token-based auth for newer VaultSharp versions
            var token = Environment.GetEnvironmentVariable("VAULT_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                var tokenAuth = new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo(token);
                var settings = new VaultClientSettings(vaultAddress, tokenAuth);
                Console.WriteLine("Using token-based Vault auth.");
                return new VaultClient(settings);
            }

            // Try to create an AWS IAM auth method via reflection.
            var awsAuth = CreateAwsAuthMethod(role, region: "us-east-2");
            if (awsAuth != null)
            {
                var settings = CreateSettings(vaultAddress, awsAuth);
                Console.WriteLine($"Using AWS IAM Vault auth with type: {awsAuth.GetType().FullName}");
                return new VaultClient(settings);
            }

            throw new NotSupportedException("No supported Vault auth method found. Provide VAULT_TOKEN or use a VaultSharp version that supports AWS IAM.");

            // Local helpers
            object? CreateAwsAuthMethod(string role, string region)
            {
                if (string.IsNullOrWhiteSpace(role))
                    return null;

                string[] awsTypeCandidates = new[] {
                    "VaultSharp.V1.AuthMethods.AWS.AWSIAMAuthMethodInfo",
                    "VaultSharp.V1.AuthMethods.AWS.AWSAuthMethodInfo",
                    "VaultSharp.V1.AuthMethods.AWS.AwsAuthMethodInfo"
                };

                foreach (var typeName in awsTypeCandidates)
                {
                    var t = Type.GetType(typeName + ", VaultSharp") ?? Type.GetType(typeName);
                    if (t == null)
                        continue;

                    // Prefer ctor (string role, string region)
                    var ctor = t.GetConstructor(new[] { typeof(string), typeof(string) });
                    if (ctor != null)
                        return ctor.Invoke(new object[] { role, region });

                    // Fallback to ctor (string role)
                    ctor = t.GetConstructor(new[] { typeof(string) });
                    if (ctor != null)
                        return ctor.Invoke(new object[] { role });
                }

                return null;
            }

            VaultClientSettings CreateSettings(string address, object authMethod)
            {
                var authInterface = typeof(VaultSharp.V1.AuthMethods.IAuthMethodInfo);

                // Look for ctor(Vault address, IAuthMethodInfo)
                var settingsCtor = typeof(VaultClientSettings).GetConstructor(new[] { typeof(string), authInterface });
                if (settingsCtor != null && authInterface.IsInstanceOfType(authMethod))
                    return (VaultClientSettings)settingsCtor.Invoke(new object[] { address, authMethod });

                // Try constructor with the concrete auth type
                settingsCtor = typeof(VaultClientSettings).GetConstructor(new[] { typeof(string), authMethod.GetType() });
                if (settingsCtor != null)
                    return (VaultClientSettings)settingsCtor.Invoke(new object[] { address, authMethod });

                // Fallback to Activator; this will throw if no matching ctor is found
                return (VaultClientSettings)Activator.CreateInstance(typeof(VaultClientSettings), address, authMethod)!;
            }
        }
    }
}
