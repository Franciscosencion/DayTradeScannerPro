namespace TradeScanner.Core.Interfaces;

public interface ISecurityService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    bool TryDecrypt(string cipherText, out string plainText);
}
