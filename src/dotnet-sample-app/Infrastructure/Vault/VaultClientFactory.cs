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

            // If a role is provided, prefer AWS IAM auth first.
            if (!string.IsNullOrWhiteSpace(role))
            {
                // Diagnostic: list VaultSharp-related assemblies and available AWS auth types
                try
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetName().Name?.StartsWith("VaultSharp") == true)
                        .ToArray();

                    foreach (var a in assemblies)
                    {
                        Console.WriteLine($"[VaultDiag] Assembly: {a.GetName().Name} {a.GetName().Version}");
                        try
                        {
                            var awsTypes = a.GetTypes().Where(t => t.Namespace != null && t.Namespace.Contains("AuthMethods") && t.Name.Contains("AWS")).ToArray();
                            foreach (var at in awsTypes)
                                Console.WriteLine($"[VaultDiag] Type: {at.FullName}");
                        }
                        catch { /* ignore type load issues for diagnostic */ }
                    }
                }
                catch (Exception diagEx)
                {
                    Console.WriteLine($"[VaultDiag] Failed to enumerate VaultSharp assemblies: {diagEx.Message}");
                }

                // Try a flexible runtime scan for any AWS auth types present in loaded assemblies.
                var awsAuth = FindAndCreateAwsAuth(role, region: Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-2");
                if (awsAuth != null)
                {
                    try
                    {
                        var settings = CreateSettings(vaultAddress, awsAuth);
                        Console.WriteLine($"Using AWS IAM Vault auth with type: {awsAuth.GetType().FullName}");
                        return new VaultClient(settings);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create VaultClientSettings with AWS auth type {awsAuth.GetType().FullName}: {ex.Message}");
                    }
                }

                // If role was provided but we couldn't construct AWS auth, log and continue to token fallback
                Console.WriteLine("VAULT_ROLE provided but no usable AWS auth type found in loaded VaultSharp assemblies; falling back to token if present.");
            }

            // Token fallback: use VAULT_TOKEN if provided
            var token = Environment.GetEnvironmentVariable("VAULT_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                var tokenAuth = new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo(token);
                var settings = new VaultClientSettings(vaultAddress, tokenAuth);
                Console.WriteLine("Using token-based Vault auth.");
                return new VaultClient(settings);
            }

            throw new NotSupportedException("No supported Vault auth method found. Provide VAULT_ROLE (with a VaultSharp that supports AWS IAM) or VAULT_TOKEN.");

            // Local helpers

            // New: flexible discovery that scans loaded assemblies for any type implementing IAuthMethodInfo
            object? FindAndCreateAwsAuth(string role, string region)
            {
                if (string.IsNullOrWhiteSpace(role))
                    return null;

                var authInterface = typeof(VaultSharp.V1.AuthMethods.IAuthMethodInfo);

                // Gather candidate types from loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var candidates = new List<Type>();
                var skippedAbstractOrInterface = new List<Type>();
                foreach (var a in assemblies)
                {
                    Type[] types = Array.Empty<Type>();
                    try
                    {
                        types = a.GetTypes();
                    }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        if (!authInterface.IsAssignableFrom(t)) continue;
                        // Skip interfaces and abstract base types for direct instantiation
                        if (t.IsInterface || t.IsAbstract)
                        {
                            skippedAbstractOrInterface.Add(t);
                            continue;
                        }
                        var name = (t.Name ?? string.Empty).ToLowerInvariant();
                        var ns = (t.Namespace ?? string.Empty).ToLowerInvariant();
                        // Heuristics: type name or namespace contains "aws" or "awsiam"
                        if (name.Contains("aws") || name.Contains("awsiam") || ns.Contains("authmethods.aws"))
                            candidates.Add(t);
                    }
                }

                if (!candidates.Any() && skippedAbstractOrInterface.Any())
                {
                    Console.WriteLine($"[VaultDiag] Found {skippedAbstractOrInterface.Count} abstract/interface AWS-related types. Will attempt to locate concrete implementations.");
                    // Search for concrete subclasses/implementations of the skipped types
                    foreach (var baseType in skippedAbstractOrInterface)
                    {
                        foreach (var a in assemblies)
                        {
                            Type[] types = Array.Empty<Type>();
                            try { types = a.GetTypes(); } catch { continue; }
                            foreach (var t in types)
                            {
                                if (t == null) continue;
                                if (t.IsAbstract || t.IsInterface) continue;
                                if (!baseType.IsAssignableFrom(t)) continue;
                                // add if not already present
                                if (!candidates.Contains(t)) candidates.Add(t);
                            }
                        }
                    }
                }

                if (!candidates.Any())
                {
                    Console.WriteLine("[VaultDiag] No candidate AWS auth types found via flexible scan.");
                    return null;
                }

                Console.WriteLine($"[VaultDiag] Found {candidates.Count} AWS auth candidate types");

                // Try to instantiate each candidate with multiple strategies
                foreach (var t in candidates)
                {
                    try
                    {
                        Console.WriteLine($"[VaultDiag] Trying to construct AWS auth type: {t.FullName}");

                        // 1) Try ctor (string role, string region)
                        var ctor = t.GetConstructor(new[] { typeof(string), typeof(string) });
                        if (ctor != null)
                        {
                            var inst = ctor.Invoke(new object[] { role, region });
                            if (authInterface.IsInstanceOfType(inst)) return inst;
                        }

                        // 2) Try ctor (string role)
                        ctor = t.GetConstructor(new[] { typeof(string) });
                        if (ctor != null)
                        {
                            var inst = ctor.Invoke(new object[] { role });
                            if (authInterface.IsInstanceOfType(inst)) return inst;
                        }

                        // 3) Try parameterless ctor then set properties 'Role' and 'Region' if present
                        ctor = t.GetConstructor(Type.EmptyTypes);
                        if (ctor != null)
                        {
                            var inst = ctor.Invoke(Array.Empty<object>());
                            var roleProp = t.GetProperty("Role") ?? t.GetProperty("role") ?? t.GetProperty("AWSRole") ?? t.GetProperty("awsRole");
                            var regionProp = t.GetProperty("Region") ?? t.GetProperty("region") ?? t.GetProperty("AWSRegion") ?? t.GetProperty("awsRegion");
                            if (roleProp != null && roleProp.CanWrite)
                                roleProp.SetValue(inst, role);
                            if (regionProp != null && regionProp.CanWrite)
                                regionProp.SetValue(inst, region);

                            if (authInterface.IsInstanceOfType(inst)) return inst;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[VaultDiag] Failed to construct candidate {t.FullName}: {ex.Message}");
                    }
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
