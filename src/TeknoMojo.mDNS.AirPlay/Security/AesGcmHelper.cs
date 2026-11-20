using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace TeknoMojo.mDNS.AirPlay.Security;

/// <summary>
/// Helper for AES GCM 128 authenticated encryption.
/// Used in Apple AirPlay Legacy Pairing per UxPlay specification.
/// </summary>
public static class AesGcmHelper
{
    /// <summary>
    /// Encrypts data using AES GCM 128 with 16-byte authentication tag.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce</param>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Tuple of (encrypted data, 16-byte authentication tag)</returns>
    public static (byte[] ciphertext, byte[] authTag) EncryptWithTag(byte[] key, byte[] iv, byte[] plaintext)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), 128, iv); // 128-bit auth tag
        cipher.Init(true, parameters);

        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, len);

        // Split output into ciphertext and authTag (last 16 bytes is the tag)
        var ciphertext = new byte[plaintext.Length];
        var authTag = new byte[16];
        Array.Copy(output, 0, ciphertext, 0, plaintext.Length);
        Array.Copy(output, plaintext.Length, authTag, 0, 16);

        return (ciphertext, authTag);
    }

    /// <summary>
    /// Encrypts data using AES GCM 128 with 16-byte authentication tag appended.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce</param>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Encrypted data with 16-byte authentication tag appended</returns>
    public static byte[] Encrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), 128, iv); // 128-bit auth tag
        cipher.Init(true, parameters);

        var ciphertext = new byte[cipher.GetOutputSize(plaintext.Length)];
        var len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, ciphertext, 0);
        cipher.DoFinal(ciphertext, len);

        return ciphertext;
    }

    /// <summary>
    /// Decrypts data using AES GCM 128 with authentication verification.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce</param>
    /// <param name="ciphertext">Encrypted data with 16-byte authentication tag appended</param>
    /// <returns>Decrypted data, or null if authentication fails</returns>
    public static byte[]? Decrypt(byte[] key, byte[] iv, byte[] ciphertext)
    {
        try
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, iv); // 128-bit auth tag
            cipher.Init(false, parameters);

            var plaintext = new byte[cipher.GetOutputSize(ciphertext.Length)];
            var len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, plaintext, 0);
            cipher.DoFinal(plaintext, len);

            return plaintext;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypts data using AES GCM 128 with separate authTag.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce</param>
    /// <param name="ciphertext">Encrypted data (without auth tag)</param>
    /// <param name="authTag">16-byte authentication tag</param>
    /// <returns>Decrypted data, or null if authentication fails</returns>
    public static byte[]? DecryptWithTag(byte[] key, byte[] iv, byte[] ciphertext, byte[] authTag)
    {
        // Combine ciphertext and authTag for BouncyCastle
        var combined = new byte[ciphertext.Length + authTag.Length];
        Array.Copy(ciphertext, 0, combined, 0, ciphertext.Length);
        Array.Copy(authTag, 0, combined, ciphertext.Length, authTag.Length);

        return Decrypt(key, iv, combined);
    }
}
