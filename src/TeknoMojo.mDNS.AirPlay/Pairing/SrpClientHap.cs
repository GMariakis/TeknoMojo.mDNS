using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace TeknoMojo.mDNS.AirPlay.Pairing;

/// <summary>
/// Apple AirPlay "Legacy Pairing" SRP-6a implementation.
/// Based on UxPlay specification: https://github.com/FDH2/UxPlay/wiki/crypto
/// 
/// Uses SRP-6a with:
/// - Prime N = NG_2048 (256 bytes)
/// - Group g = 2
/// - Hash = SHA1 (20-byte digest)
/// - Modified K = K1 || K2 where K1 = H(S || {0,0,0,0}), K2 = H(S || {0,0,0,1})
/// </summary>
public class SrpClientHap
{
    // RFC 5054 NG_2048 prime (256 bytes)
    private static readonly BigInteger N = new BigInteger(
        "AC6BDB41324A9A9BF166DE5E1389582FAF72B6651987EE07FC319294" +
        "3DB56050A37329CBB4A099ED8193E0757767A13DD52312AB4B03310D" +
        "CD7F48A9DA04FD50E8083969EDB767B0CF6095179A163AB3661A05FB" +
        "D5FAAAE82918A9962F0B93B855F97993EC975EEAA80D740ADBF4FF74" +
        "7359D041D5C33EA71D281E446B14773BCA97B43A23FB801676BD207A" +
        "436C6481F1D2B9078717461A5B9D32E688F87748544523B524B0D57D" +
        "5EA77A2775D2ECFA032CFBDBF52FB3786160279004E57AE6AF874E73" +
        "03CE53299CCC041C7BC308D82A5698F3A8D0C38271AE35F8E9DBFBB6" +
        "94B5C803D89F7AE435DE236D525F54759B65E372FCD68EF20FA7111F" +
        "9E4AFF73",
        16);

    private static readonly BigInteger g = BigInteger.ValueOf(2);
    private const int N_BYTES = 256; // 2048 bits

    private readonly SecureRandom _random;
    private BigInteger? _a;
    private BigInteger? _A;
    private BigInteger? _B;
    private byte[]? _salt;
    private byte[]? _K; // 40-byte session key (K1 || K2)
    private byte[]? _M1; // Client proof
    private byte[]? _M2; // Server proof (expected)
    private string? _I; // username (DeviceID)
    private string? _P; // password (PIN)

    public byte[] PublicKey => PadTo(_A?.ToByteArrayUnsigned() ?? throw new InvalidOperationException("Not initialized"), N_BYTES);
    public byte[] SessionKey => _K ?? throw new InvalidOperationException("Session key not computed");
    public byte[] ClientProof => _M1 ?? throw new InvalidOperationException("Proof not computed");

    public SrpClientHap()
    {
        _random = new SecureRandom();
    }

    public void StartAuthentication(string username, string password)
    {
        _I = username;
        _P = password;

        // Generate random 32-byte ephemeral secret a
        _a = new BigInteger(256, _random);
        while (_a.CompareTo(N) >= 0)
        {
            _a = new BigInteger(256, _random);
        }

        // A = g^a mod N
        _A = g.ModPow(_a, N);
    }

    public void ProcessChallenge(byte[] salt, byte[] serverPublicKey)
    {
        if (_a == null || _A == null || _I == null || _P == null)
            throw new InvalidOperationException("Call StartAuthentication first");

        _salt = salt;
        _B = new BigInteger(1, serverPublicKey);

        // Verify B % N != 0
        if (_B.Mod(N).Equals(BigInteger.Zero))
            throw new InvalidOperationException("Invalid server public key");

        // SRP-6a calculations per UxPlay spec

        // k = H(N || PAD(g)) - but make sure PAD(g) is NOT used in calculating H(N)^H(g)
        var k = new BigInteger(1, H(Concat(
            PadTo(N.ToByteArrayUnsigned(), N_BYTES),
            PadTo(g.ToByteArrayUnsigned(), N_BYTES)
        )));

        // u = H(PAD(A) || PAD(B))
        var APadded = PadTo(_A.ToByteArrayUnsigned(), N_BYTES);
        var BPadded = PadTo(_B.ToByteArrayUnsigned(), N_BYTES);
        var u = new BigInteger(1, H(Concat(APadded, BPadded)));

        if (u.Equals(BigInteger.Zero))
            throw new InvalidOperationException("Invalid u value");

        // x = H(salt || H(Username || ":" || Password))
        var identityHash = H(System.Text.Encoding.UTF8.GetBytes($"{_I}:{_P}"));
        var x = new BigInteger(1, H(Concat(salt, identityHash)));

        // S = (B - k * g^x) ^ (a + u * x) mod N
        var gx = g.ModPow(x, N);
        var kgx = k.Multiply(gx).Mod(N);
        var base_ = _B.Subtract(kgx).Mod(N);
        if (base_.SignValue < 0)
            base_ = base_.Add(N);

        var exp = _a.Add(u.Multiply(x));
        var S = base_.ModPow(exp, N);
        var SPadded = PadTo(S.ToByteArrayUnsigned(), N_BYTES);

        // Apple's modified K: K = K1 || K2
        // K1 = H(S || {0,0,0,0})
        // K2 = H(S || {0,0,0,1})
        var K1 = H(Concat(SPadded, new byte[] { 0, 0, 0, 0 }));
        var K2 = H(Concat(SPadded, new byte[] { 0, 0, 0, 1 }));
        _K = Concat(K1, K2); // 40 bytes total (20 + 20)

        // M1 = H(H(N) XOR H(g) || H(I) || salt || PAD(A) || PAD(B) || K)
        // Note: H(N) uses PAD(N), but H(g) uses unpadded g!
        var hN = H(PadTo(N.ToByteArrayUnsigned(), N_BYTES));
        var hg = H(g.ToByteArrayUnsigned()); // NOT padded!
        var hNxorHg = new byte[20]; // SHA1 digest is 20 bytes
        for (int i = 0; i < 20; i++)
            hNxorHg[i] = (byte)(hN[i] ^ hg[i]);

        var hI = H(System.Text.Encoding.UTF8.GetBytes(_I));

        _M1 = H(Concat(hNxorHg, hI, salt, APadded, BPadded, _K));

        // M2 = H(A || M1 || K)
        _M2 = H(Concat(APadded, _M1, _K));
    }

    public bool VerifyServerProof(byte[] serverProof)
    {
        if (_M2 == null)
            throw new InvalidOperationException("Challenge not processed");

        return ConstantTimeEquals(_M2, serverProof);
    }

    private static byte[] H(byte[] data)
    {
        // Use SHA1 as specified in UxPlay documentation
        var digest = new Sha1Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var output = new byte[digest.GetDigestSize()]; // 20 bytes
        digest.DoFinal(output, 0);
        return output;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(a => a.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var array in arrays)
        {
            Array.Copy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }
        return result;
    }

    private static byte[] PadTo(byte[] data, int length)
    {
        if (data.Length >= length)
            return data;

        var result = new byte[length];
        Array.Copy(data, 0, result, length - data.Length, data.Length);
        return result;
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }
}
