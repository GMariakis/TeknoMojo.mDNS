using System;
using Claunia.PropertyList;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Pairing Format Test ===\n");

        // Test data matching SRP test values
        byte[] publicKey = new byte[256]; // 256 bytes for 2048-bit group
        byte[] proof = new byte[64]; // SHA-512 = 64 bytes

        // Fill with test pattern
        for (int i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)(i % 256);
        for (int i = 0; i < proof.Length; i++) proof[i] = (byte)((i + 1) % 256);

        Console.WriteLine($"Public Key length: {publicKey.Length} bytes");
        Console.WriteLine($"Proof length: {proof.Length} bytes\n");

        // Create M3 plist (what we send)
        var m3Plist = new NSDictionary
        {
            { "pk", new NSData(publicKey) },
            { "proof", new NSData(proof) }
        };

        byte[] m3Binary = BinaryPropertyListWriter.WriteToArray(m3Plist);
        Console.WriteLine($"M3 plist size: {m3Binary.Length} bytes");
        Console.WriteLine($"M3 plist hex: {BitConverter.ToString(m3Binary).Replace("-", "").Substring(0, 100)}...\n");

        // Verify we can read it back
        var parsed = (NSDictionary)PropertyListParser.Parse(m3Binary);
        var pkData = (NSData)parsed["pk"];
        var proofData = (NSData)parsed["proof"];

        Console.WriteLine($"Parsed pk length: {pkData.Bytes.Length} bytes");
        Console.WriteLine($"Parsed proof length: {proofData.Bytes.Length} bytes");

        bool pkMatch = CompareBytes(publicKey, pkData.Bytes);
        bool proofMatch = CompareBytes(proof, proofData.Bytes);

        Console.WriteLine($"\nPK roundtrip: {(pkMatch ? "✓ SUCCESS" : "✗ FAILED")}");
        Console.WriteLine($"Proof roundtrip: {(proofMatch ? "✓ SUCCESS" : "✗ FAILED")}");

        // Show plist structure
        Console.WriteLine("\n=== Binary Plist Structure ===");
        Console.WriteLine($"Header: {BitConverter.ToString(m3Binary, 0, Math.Min(8, m3Binary.Length))}");
        Console.WriteLine($"Total size: {m3Binary.Length} bytes");
    }

    static bool CompareBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
