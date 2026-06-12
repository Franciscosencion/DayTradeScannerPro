using System.Security.Cryptography;
using System.Text;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Security;

public class DpapiSecurityService : ISecurityService
{
    private static readonly byte[] Entropy = "TradeScanner_2026_Salt"u8.ToArray();

    public string Encrypt(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    public bool TryDecrypt(string cipherText, out string plainText)
    {
        try
        {
            plainText = Decrypt(cipherText);
            return true;
        }
        catch
        {
            plainText = string.Empty;
            return false;
        }
    }
}
