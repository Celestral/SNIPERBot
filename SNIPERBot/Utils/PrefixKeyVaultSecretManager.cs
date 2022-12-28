using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace SNIPERBot.Utils;

public class PrefixKeyVaultSecretManager : KeyVaultSecretManager
{
    private readonly string _prefix;

    public PrefixKeyVaultSecretManager(string prefix) => _prefix = $"{prefix}-";

    public override bool Load(SecretProperties properties) => properties.Name.StartsWith(_prefix) && properties.Enabled.HasValue &&
                                                               properties.Enabled.Value &&
                                                              (properties.ExpiresOn.HasValue &&
                                                                properties.ExpiresOn.Value > DateTimeOffset.Now || !properties.ExpiresOn.HasValue);

    public override string GetKey(KeyVaultSecret secret) => secret.Name[_prefix.Length..].Replace("--", ConfigurationPath.KeyDelimiter);
}