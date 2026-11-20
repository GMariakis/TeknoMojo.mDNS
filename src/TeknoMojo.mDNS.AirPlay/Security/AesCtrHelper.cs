using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace TeknoMojo.mDNS.AirPlay.Security;

/// <summary>
/// Helper for AES CTR 128 encryption.
/// Used in AirPlay Legacy Pairing pair-verify per UxPlay specification.
/// Note: AES CTR is symmetric - encrypt and decrypt are the same operation.
/// </summary>
public static class AesCtrHelper
{
    /// <summary>
    /// Encrypts data using AES CTR 128.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce (counter)</param>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Encrypted data (same length as input)</returns>
    public static byte[] Encrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        return Process(key, iv, plaintext);
    }

    /// <summary>
    /// Decrypts data using AES CTR 128.
    /// Note: CTR mode is symmetric, so this is the same as Encrypt.
    /// </summary>
    /// <param name="key">16-byte AES key</param>
    /// <param name="iv">16-byte IV/nonce (counter)</param>
    /// <param name="ciphertext">Data to decrypt</param>
    /// <returns>Decrypted data (same length as input)</returns>
    public static byte[] Decrypt(byte[] key, byte[] iv, byte[] ciphertext)
    {
        return Process(key, iv, ciphertext);
    }

    /// <summary>
    /// Processes multiple blocks of data with the same cipher instance (counter continues).
    /// Used in pair-verify where we decrypt server signature then encrypt client signature.
    /// </summary>
    public static (byte[] block1, byte[] block2) ProcessTwoBlocks(byte[] key, byte[] iv, byte[] data1, byte[] data2)
    {
        var cipher = new SicBlockCipher(new AesEngine());
        var parameters = new ParametersWithIV(new KeyParameter(key), iv);
        cipher.Init(true, parameters);

        // Process first block
        var output1 = ProcessWithCipher(cipher, data1);
        
        // Process second block (counter continues from first block)
        var output2 = ProcessWithCipher(cipher, data2);

        return (output1, output2);
    }

    private static byte[] Process(byte[] key, byte[] iv, byte[] data)
    {
        var cipher = new SicBlockCipher(new AesEngine());
        var parameters = new ParametersWithIV(new KeyParameter(key), iv);
        cipher.Init(true, parameters);

        return ProcessWithCipher(cipher, data);
    }

    private static byte[] ProcessWithCipher(SicBlockCipher cipher, byte[] data)
    {
        var output = new byte[data.Length];
        var blockSize = cipher.GetBlockSize();

        var offset = 0;
        while (offset < data.Length)
        {
            var remaining = data.Length - offset;
            if (remaining >= blockSize)
            {
                cipher.ProcessBlock(data, offset, output, offset);
                offset += blockSize;
            }
            else
            {
                // Handle partial final block
                var lastBlock = new byte[blockSize];
                var outputBlock = new byte[blockSize];
                Array.Copy(data, offset, lastBlock, 0, remaining);
                cipher.ProcessBlock(lastBlock, 0, outputBlock, 0);
                Array.Copy(outputBlock, 0, output, offset, remaining);
                offset += remaining;
            }
        }

        return output;
    }
}
