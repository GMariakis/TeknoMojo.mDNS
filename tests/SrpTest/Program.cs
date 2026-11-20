using System;
using System.Linq;
using SecureRemotePassword;
using TeknoMojo.mDNS.AirPlay.Pairing;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SRP-6a Comparison Test ===\n");

        // Use RFC 5054 2048-bit group with SHA-512
        var parameters = SrpParameters.Create2048<System.Security.Cryptography.SHA512>();

        var srpNetClient = new SecureRemotePassword.SrpClient(parameters);

        // Simulate HomeKit/AirPlay pairing
        string username = "Pair-Setup";
        string password = "1234"; // Test PIN

        Console.WriteLine($"Username: {username}");
        Console.WriteLine($"Password: {password}\n");

        // Generate salt and derive private key (normally done by server)
        var saltHex = srpNetClient.GenerateSalt();
        var saltBytes = HexToBytes(saltHex);
        Console.WriteLine($"Salt: {saltHex.Substring(0, 32)}... ({saltBytes.Length} bytes)\n");

        var privateKey = srpNetClient.DerivePrivateKey(saltHex, username, password);
        var verifier = srpNetClient.DeriveVerifier(privateKey);

        // Client generates ephemeral (use fixed value for comparison)
        var clientEphemeral = srpNetClient.GenerateEphemeral();
        var clientABytes = HexToBytes(clientEphemeral.Public);
        Console.WriteLine($"Client A: {clientEphemeral.Public.Substring(0, 32)}... ({clientABytes.Length} bytes)\n");

        // Simulate server generating B
        var server = new SrpServer(parameters);
        var serverEphemeral = server.GenerateEphemeral(verifier);
        var serverBBytes = HexToBytes(serverEphemeral.Public);
        Console.WriteLine($"Server B: {serverEphemeral.Public.Substring(0, 32)}... ({serverBBytes.Length} bytes)\n");

        // ===== srp.net Client Calculation =====
        Console.WriteLine("=== srp.net Client Calculation ===");
        var srpNetSession = srpNetClient.DeriveSession(clientEphemeral.Secret, serverEphemeral.Public, saltHex, username, privateKey);
        Console.WriteLine($"K:  {srpNetSession.Key}");
        Console.WriteLine($"M1: {srpNetSession.Proof}\n");

        // ===== Our SrpClient Calculation =====
        Console.WriteLine("=== Our SrpClient Calculation ===");
        var ourClient = new TeknoMojo.mDNS.AirPlay.Pairing.SrpClient();

        // Use the SAME ephemeral secret as srp.net for comparison
        ourClient.StartAuthenticationWithEphemeral(username, password, clientEphemeral.Secret);

        // Get our A (should match srp.net's A now)
        var ourA = ourClient.PublicKey;
        var ourAHex = BitConverter.ToString(ourA).Replace("-", "").ToLower();
        Console.WriteLine($"Our A: {ourAHex.Substring(0, 32)}... ({ourA.Length} bytes)");

        bool aMatch = ourAHex.Equals(clientEphemeral.Public, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"A match: {(aMatch ? "✓ YES" : "✗ NO")}\n");

        // Process challenge with SAME values from srp.net
        ourClient.ProcessChallenge(saltBytes, serverBBytes);

        var ourK = BitConverter.ToString(ourClient.SessionKey).Replace("-", "").ToLower();
        var ourM1 = BitConverter.ToString(ourClient.ClientProof).Replace("-", "").ToLower();

        Console.WriteLine($"K:  {ourK}");
        Console.WriteLine($"M1: {ourM1}\n");

        // ===== Comparison =====
        Console.WriteLine("=== Comparison ===");
        bool kMatch = ourK.Equals(srpNetSession.Key, StringComparison.OrdinalIgnoreCase);
        bool m1Match = ourM1.Equals(srpNetSession.Proof, StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"K match:  {(kMatch ? "✓ YES" : "✗ NO")}");
        Console.WriteLine($"M1 match: {(m1Match ? "✓ YES" : "✗ NO")}");

        if (!kMatch || !m1Match)
        {
            Console.WriteLine("\n⚠ MISMATCH DETECTED!");
            Console.WriteLine("\nExpected (srp.net):");
            Console.WriteLine($"  K:  {srpNetSession.Key}");
            Console.WriteLine($"  M1: {srpNetSession.Proof}");
            Console.WriteLine("\nActual (our implementation):");
            Console.WriteLine($"  K:  {ourK}");
            Console.WriteLine($"  M1: {ourM1}");
        }
        else
        {
            Console.WriteLine("\n✓ All calculations match!");
        }
    }

    static byte[] HexToBytes(string hex)
    {
        return Enumerable.Range(0, hex.Length / 2)
            .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
            .ToArray();
    }
}
