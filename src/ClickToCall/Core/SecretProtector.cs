using System.Security.Cryptography;
using System.Text;

namespace ClickToCall.Core;

/// <summary>Encrypts secrets with Windows DPAPI so only the current Windows user can read them.</summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ClickToCall");

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain))
            return "";

        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(data);
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
            return "";

        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Settings were copied from another user or machine
            return "";
        }
    }
}
