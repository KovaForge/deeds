using System.Security.Cryptography;
using System.Text;

public static class CryptoHelper
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static bool TryGetEncryptionKey(out byte[] key, out string? error)
    {
        key = Array.Empty<byte>();
        error = null;

        var raw = Environment.GetEnvironmentVariable("AI_KEY_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "AI key encryption is not configured.";
            return false;
        }

        try
        {
            key = Convert.FromBase64String(raw.Trim());
        }
        catch (FormatException)
        {
            error = "AI key encryption key must be base64 encoded.";
            return false;
        }

        if (key.Length != KeySize)
        {
            error = $"AI key encryption key must be {KeySize} bytes.";
            return false;
        }

        return true;
    }

    public static EncryptedPayload Encrypt(string plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plaintextBytes.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

        return new EncryptedPayload(
            Convert.ToBase64String(cipherBytes),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag));
    }

    public static string Decrypt(EncryptedPayload payload, byte[] key)
    {
        var cipherBytes = Convert.FromBase64String(payload.CipherText);
        var nonce = Convert.FromBase64String(payload.Nonce);
        var tag = Convert.FromBase64String(payload.Tag);
        var plaintextBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}

public sealed record EncryptedPayload(string CipherText, string Nonce, string Tag);
