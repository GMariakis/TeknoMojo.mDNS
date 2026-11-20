using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace TeknoMojo.mDNS.AirPlay.Security;

/// <summary>
/// Ed25519 key generation and signing helper.
/// Educational: Ed25519 is a high-speed, high-security digital signature scheme.
/// Apple uses it for device identity verification in HomeKit pairing.
/// </summary>
public static class Ed25519Helper
{
    /// <summary>
    /// Generates a new Ed25519 key pair.
    /// </summary>
    /// <returns>Tuple of (privateKey, publicKey) as byte arrays.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        var keyPairGenerator = new Ed25519KeyPairGenerator();
        keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var privateKey = ((Ed25519PrivateKeyParameters)keyPair.Private).GetEncoded();
        var publicKey = ((Ed25519PublicKeyParameters)keyPair.Public).GetEncoded();

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Signs data using Ed25519.
    /// </summary>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <param name="message">The message to sign.</param>
    /// <returns>The 64-byte signature.</returns>
    public static byte[] Sign(byte[] privateKey, byte[] message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(privateKey, 0));
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }

    /// <summary>
    /// Verifies an Ed25519 signature.
    /// </summary>
    /// <param name="publicKey">The 32-byte public key.</param>
    /// <param name="message">The original message.</param>
    /// <param name="signature">The 64-byte signature to verify.</param>
    /// <returns>True if the signature is valid.</returns>
    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }
}

/// <summary>
/// Curve25519 (X25519) key exchange helper.
/// Educational: Curve25519 is used for Elliptic Curve Diffie-Hellman key exchange.
/// It allows two parties to derive a shared secret over an insecure channel.
/// </summary>
public static class Curve25519Helper
{
    /// <summary>
    /// Generates a new Curve25519 key pair for key exchange.
    /// </summary>
    /// <returns>Tuple of (privateKey, publicKey) as 32-byte arrays.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        var keyPairGenerator = new X25519KeyPairGenerator();
        keyPairGenerator.Init(new X25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var privateKey = ((X25519PrivateKeyParameters)keyPair.Private).GetEncoded();
        var publicKey = ((X25519PublicKeyParameters)keyPair.Public).GetEncoded();

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Computes the shared secret using ECDH.
    /// </summary>
    /// <param name="ourPrivateKey">Our 32-byte private key.</param>
    /// <param name="theirPublicKey">Their 32-byte public key.</param>
    /// <returns>The 32-byte shared secret.</returns>
    public static byte[] ComputeSharedSecret(byte[] ourPrivateKey, byte[] theirPublicKey)
    {
        var agreement = new X25519Agreement();
        agreement.Init(new X25519PrivateKeyParameters(ourPrivateKey, 0));

        var sharedSecret = new byte[agreement.AgreementSize];
        agreement.CalculateAgreement(new X25519PublicKeyParameters(theirPublicKey, 0), sharedSecret, 0);

        return sharedSecret;
    }
}

/// <summary>
/// ChaCha20-Poly1305 AEAD encryption helper.
/// Educational: ChaCha20-Poly1305 is an authenticated encryption algorithm.
/// It provides both confidentiality and integrity in a single operation.
/// Apple uses this for encrypting data during pairing.
/// </summary>
public static class ChaCha20Poly1305Helper
{
    /// <summary>
    /// Encrypts data using ChaCha20-Poly1305.
    /// </summary>
    /// <param name="key">The 32-byte key.</param>
    /// <param name="nonce">The 12-byte nonce (IV).</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="aad">Additional authenticated data (optional).</param>
    /// <returns>The ciphertext with 16-byte authentication tag appended.</returns>
    public static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[]? aad = null)
    {
        var cipher = new ChaCha20Poly1305();
        var parameters = new AeadParameters(new KeyParameter(key), 128, nonce, aad);
        cipher.Init(true, parameters);

        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, len);

        return output;
    }

    /// <summary>
    /// Decrypts data using ChaCha20-Poly1305.
    /// </summary>
    /// <param name="key">The 32-byte key.</param>
    /// <param name="nonce">The 12-byte nonce (IV).</param>
    /// <param name="ciphertext">The encrypted data with authentication tag.</param>
    /// <param name="aad">Additional authenticated data (optional).</param>
    /// <returns>The decrypted plaintext, or null if authentication fails.</returns>
    public static byte[]? Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[]? aad = null)
    {
        try
        {
            var cipher = new ChaCha20Poly1305();
            var parameters = new AeadParameters(new KeyParameter(key), 128, nonce, aad);
            cipher.Init(false, parameters);

            var output = new byte[cipher.GetOutputSize(ciphertext.Length)];
            var len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, output, 0);
            cipher.DoFinal(output, len);

            return output;
        }
        catch (InvalidCipherTextException)
        {
            return null; // Authentication failed
        }
    }
}

/// <summary>
/// HKDF (HMAC-based Key Derivation Function) helper.
/// Educational: HKDF is used to derive cryptographic keys from a shared secret.
/// It expands a short secret into longer key material in a secure way.
/// </summary>
public static class HkdfHelper
{
    /// <summary>
    /// Derives key material using HKDF-SHA512.
    /// </summary>
    /// <param name="inputKeyMaterial">The input key material (e.g., shared secret).</param>
    /// <param name="salt">Optional salt value.</param>
    /// <param name="info">Context/application-specific info.</param>
    /// <param name="length">Desired output length in bytes.</param>
    /// <returns>The derived key material.</returns>
    public static byte[] DeriveKey(byte[] inputKeyMaterial, byte[]? salt, byte[] info, int length)
    {
        var generator = new HkdfBytesGenerator(new Sha512Digest());

        var parameters = salt != null
            ? new HkdfParameters(inputKeyMaterial, salt, info)
            : new HkdfParameters(inputKeyMaterial, Array.Empty<byte>(), info);

        generator.Init(parameters);

        var output = new byte[length];
        generator.GenerateBytes(output, 0, length);

        return output;
    }

    /// <summary>
    /// Derives key material using HKDF-SHA512 with string info.
    /// </summary>
    public static byte[] DeriveKey(byte[] inputKeyMaterial, byte[]? salt, string info, int length)
    {
        return DeriveKey(inputKeyMaterial, salt, System.Text.Encoding.UTF8.GetBytes(info), length);
    }
}

/// <summary>
/// SHA-512 hashing helper.
/// </summary>
public static class Sha512Helper
{
    /// <summary>
    /// Computes SHA-512 hash of the input data.
    /// </summary>
    public static byte[] Hash(byte[] data)
    {
        var digest = new Sha512Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var output = new byte[digest.GetDigestSize()];
        digest.DoFinal(output, 0);
        return output;
    }

    /// <summary>
    /// Computes SHA-512 hash of concatenated data arrays.
    /// </summary>
    public static byte[] Hash(params byte[][] dataArrays)
    {
        var digest = new Sha512Digest();
        foreach (var data in dataArrays)
        {
            digest.BlockUpdate(data, 0, data.Length);
        }
        var output = new byte[digest.GetDigestSize()];
        digest.DoFinal(output, 0);
        return output;
    }
}
