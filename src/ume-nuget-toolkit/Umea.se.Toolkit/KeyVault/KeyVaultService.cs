using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Umea.se.Toolkit.KeyVault.Exceptions;

namespace Umea.se.Toolkit.KeyVault;

internal static class KeyVaultService
{
    private static SecretClient? _secretClient;
    private static SecretClient SecretClient => _secretClient ?? throw new KeyVaultConnectionException($"Key Vault is not connected! Please call {nameof(KeyVaultService)}.{nameof(ConnectToKeyVault)} first.");

    internal static bool IsConnected => _secretClient is not null;

    internal static void ConnectToKeyVault(string keyVaultUrl)
    {
        DefaultAzureCredential credential = GetDefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    }

    internal static string GetSecret(string secretName)
    {
        KeyVaultSecret secret = SecretClient.GetSecret(secretName);
        return secret.Value;
    }

    internal static X509Certificate2 GetCertificate(string certificateName)
    {
        string base64Pfx = GetSecret(certificateName);
        byte[] pfxBytes = Convert.FromBase64String(base64Pfx);
        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            string.Empty,
            X509KeyStorageFlags.MachineKeySet);

        return certificate;
    }

    private static DefaultAzureCredential GetDefaultAzureCredential()
    {
        DefaultAzureCredentialOptions credentialOptions = new();

        credentialOptions.Retry.MaxRetries = 0;

        credentialOptions.ExcludeEnvironmentCredential = true;
        credentialOptions.ExcludeInteractiveBrowserCredential = true;
        credentialOptions.ExcludeVisualStudioCodeCredential = true;
        credentialOptions.ExcludeAzurePowerShellCredential = true;

        credentialOptions.ExcludeManagedIdentityCredential = false;
        credentialOptions.ExcludeAzureCliCredential = false;
        credentialOptions.ExcludeVisualStudioCredential = false;

        return new DefaultAzureCredential(credentialOptions);
    }
}

