using System.Security.Cryptography;

namespace TeknoMojo.mDNS.AirPlay.Security;

/// <summary>
/// Handles encryption for AirPlay audio streaming.
/// Educational: AirPlay uses a hybrid encryption scheme:
/// 1. RSA (asymmetric) for key exchange - encrypts the AES session key
/// 2. AES-128 (symmetric) for audio data - fast encryption of audio samples
/// This combines the security of RSA with the performance of AES.
/// </summary>
public static class AirPlayEncryption
{
    /// <summary>
    /// Generates a new AES session key and encrypts it with the device's RSA public key.
    /// Educational: This implements the key exchange process:
    /// 1. Generate random 128-bit AES key and IV
    /// 2. Encrypt AES key with device's RSA public key using OAEP padding
    /// 3. Return both encrypted key and IV (IV is sent unencrypted)
    /// </summary>
    /// <param name="devicePublicKeyBase64">
    /// The device's RSA public key from TXT record 'pk' field (base64-encoded).
    /// </param>
    /// <returns>Tuple of (encrypted AES key, AES IV) both base64-encoded.</returns>
    public static (string encryptedAesKey, string aesIv) GenerateEncryptedSession(string devicePublicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(devicePublicKeyBase64))
            throw new ArgumentException("Device public key cannot be empty.", nameof(devicePublicKeyBase64));

        // Educational: Generate random 128-bit (16 bytes) AES key and IV
        using var aes = Aes.Create();
        aes.KeySize = 128; // AirPlay uses AES-128
        aes.GenerateKey();
        aes.GenerateIV();

        var aesKey = aes.Key;   // 16 bytes
        var aesIv = aes.IV;     // 16 bytes

        // Educational: Decode the device's RSA public key
        var publicKeyBytes = Convert.FromBase64String(devicePublicKeyBase64);

        // Educational: Import the RSA public key
        // AirPlay public keys are typically in PKCS#1 RSAPublicKey format
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(publicKeyBytes, out _);

        // Educational: Encrypt the AES key with RSA using OAEP padding
        // OAEP (Optimal Asymmetric Encryption Padding) provides security against attacks
        var encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA1);

        // Educational: Return both as base64 strings for transmission in SDP
        return (
            Convert.ToBase64String(encryptedAesKey),
            Convert.ToBase64String(aesIv)
        );
    }

    /// <summary>
    /// Encrypts audio data using AES-128-CBC.
    /// Educational: AES in CBC (Cipher Block Chaining) mode:
    /// - Each 16-byte block is XORed with the previous ciphertext block before encryption
    /// - The IV (Initialization Vector) is used for the first block
    /// - Provides better security than ECB mode
    /// </summary>
    /// <param name="audioData">The raw audio data to encrypt.</param>
    /// <param name="aesKey">The 128-bit (16-byte) AES key.</param>
    /// <param name="aesIv">The 128-bit (16-byte) AES IV.</param>
    /// <returns>Encrypted audio data.</returns>
    public static byte[] EncryptAudioData(byte[] audioData, byte[] aesKey, byte[] aesIv)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be empty.", nameof(audioData));

        if (aesKey == null || aesKey.Length != 16)
            throw new ArgumentException("AES key must be 16 bytes (128 bits).", nameof(aesKey));

        if (aesIv == null || aesIv.Length != 16)
            throw new ArgumentException("AES IV must be 16 bytes (128 bits).", nameof(aesIv));

        // Educational: Create AES cipher in CBC mode with PKCS7 padding
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = aesIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7; // Pads the last block to 16 bytes

        // Educational: Encrypt the data
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(audioData, 0, audioData.Length);
    }

    /// <summary>
    /// Decrypts audio data using AES-128-CBC.
    /// Educational: This is the reverse of encryption, typically not needed
    /// on the client side but useful for testing or receiving audio.
    /// </summary>
    /// <param name="encryptedData">The encrypted audio data.</param>
    /// <param name="aesKey">The 128-bit (16-byte) AES key.</param>
    /// <param name="aesIv">The 128-bit (16-byte) AES IV.</param>
    /// <returns>Decrypted audio data.</returns>
    public static byte[] DecryptAudioData(byte[] encryptedData, byte[] aesKey, byte[] aesIv)
    {
        if (encryptedData == null || encryptedData.Length == 0)
            throw new ArgumentException("Encrypted data cannot be empty.", nameof(encryptedData));

        if (aesKey == null || aesKey.Length != 16)
            throw new ArgumentException("AES key must be 16 bytes (128 bits).", nameof(aesKey));

        if (aesIv == null || aesIv.Length != 16)
            throw new ArgumentException("AES IV must be 16 bytes (128 bits).", nameof(aesIv));

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = aesIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }

    /// <summary>
    /// Generates a random AES key and IV without RSA encryption.
    /// Educational: For testing or devices that don't require encryption.
    /// </summary>
    /// <returns>Tuple of (AES key, AES IV) both as byte arrays.</returns>
    public static (byte[] aesKey, byte[] aesIv) GenerateAesSession()
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.GenerateKey();
        aes.GenerateIV();

        return (aes.Key, aes.IV);
    }

    /// <summary>
    /// Validates that a public key string is properly formatted.
    /// Educational: AirPlay public keys should be base64-encoded RSA public keys.
    /// </summary>
    /// <param name="publicKeyBase64">The base64-encoded public key.</param>
    /// <returns>True if the key appears valid.</returns>
    public static bool ValidatePublicKey(string publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64))
            return false;

        try
        {
            var keyBytes = Convert.FromBase64String(publicKeyBase64);
            if (keyBytes.Length < 32) // RSA keys are at least 256 bits
                return false;

            // Try to import it as an RSA key
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(keyBytes, out _);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
