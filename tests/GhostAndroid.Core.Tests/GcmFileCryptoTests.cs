using System.Security.Cryptography;
using System.Text;
using GhostAndroid.Core.Security;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class GcmFileCryptoTests
{
    // Peu d'itérations pour des tests rapides ; la production utilise 210 000.
    private static GcmFileCrypto NewCrypto(int chunk = 4096) => new(iterations: 10_000, chunkSize: chunk);

    private static async Task<byte[]> EncryptAsync(byte[] plain, string password, int chunk = 4096)
    {
        var crypto = NewCrypto(chunk);
        using var input = new MemoryStream(plain);
        using var output = new MemoryStream();
        await crypto.EncryptAsync(input, output, password);
        return output.ToArray();
    }

    private static async Task<byte[]> DecryptAsync(byte[] cipher, string password, int chunk = 4096)
    {
        var crypto = NewCrypto(chunk);
        using var input = new MemoryStream(cipher);
        using var output = new MemoryStream();
        await crypto.DecryptAsync(input, output, password);
        return output.ToArray();
    }

    [Fact]
    public async Task RoundTrip_SmallPayload()
    {
        var plain = Encoding.UTF8.GetBytes("Données sensibles GHOST ANDROID 👻");
        var cipher = await EncryptAsync(plain, "motDePasse!");
        var decrypted = await DecryptAsync(cipher, "motDePasse!");
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public async Task RoundTrip_MultipleChunks()
    {
        var plain = RandomNumberGenerator.GetBytes(4096 * 5 + 123); // force plusieurs blocs + reste
        var cipher = await EncryptAsync(plain, "p@ss", chunk: 4096);
        var decrypted = await DecryptAsync(cipher, "p@ss", chunk: 4096);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public async Task RoundTrip_ExactChunkBoundary()
    {
        var plain = RandomNumberGenerator.GetBytes(4096 * 2); // multiple exact de la taille de bloc
        var cipher = await EncryptAsync(plain, "x", chunk: 4096);
        var decrypted = await DecryptAsync(cipher, "x", chunk: 4096);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public async Task RoundTrip_EmptyPayload()
    {
        var cipher = await EncryptAsync(Array.Empty<byte>(), "pw");
        var decrypted = await DecryptAsync(cipher, "pw");
        Assert.Empty(decrypted);
    }

    [Fact]
    public async Task WrongPassword_Throws()
    {
        var cipher = await EncryptAsync(Encoding.UTF8.GetBytes("secret"), "bonMotDePasse");
        await Assert.ThrowsAsync<CryptographicException>(() => DecryptAsync(cipher, "mauvaisMotDePasse"));
    }

    [Fact]
    public async Task TamperedCiphertext_Throws()
    {
        var cipher = await EncryptAsync(Encoding.UTF8.GetBytes("intégrité"), "pw");
        cipher[^1] ^= 0xFF; // altère le dernier octet
        await Assert.ThrowsAsync<CryptographicException>(() => DecryptAsync(cipher, "pw"));
    }

    [Fact]
    public async Task CipherText_IsNotPlaintext_AndHasHeader()
    {
        var plain = Encoding.UTF8.GetBytes("ne doit pas apparaître en clair");
        var cipher = await EncryptAsync(plain, "pw");
        Assert.StartsWith("GHOSTAES", Encoding.ASCII.GetString(cipher, 0, 8));
        // Le texte clair ne doit pas se retrouver tel quel dans le chiffré.
        Assert.DoesNotContain(Encoding.ASCII.GetString(Encoding.UTF8.GetBytes("apparaître")),
            Encoding.ASCII.GetString(cipher));
    }

    [Fact]
    public async Task EmptyPassword_Rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => EncryptAsync(new byte[] { 1 }, ""));
    }
}
