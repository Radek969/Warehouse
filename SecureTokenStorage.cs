using System.Security.Cryptography;
using System.Text;

public static class SecureTokenStorage
{
    private const string TokenFile = "token.enc";

    public static void Save(string token)
    {
        byte[] data = Encoding.UTF8.GetBytes(token);

        byte[] encrypted = ProtectedData.Protect(
            data,
            null,
            DataProtectionScope.CurrentUser);

        File.WriteAllBytes(TokenFile, encrypted);
    }

    public static string Load()
    {
        byte[] encrypted =
            File.ReadAllBytes(TokenFile);

        byte[] data = ProtectedData.Unprotect(
            encrypted,
            null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(data);
    }
}