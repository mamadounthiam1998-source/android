using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace GhostAndroid.Core.Security;

/// <summary>
/// Chiffrement authentifié AES-256-GCM par blocs, conçu pour des fichiers de toute
/// taille (sauvegardes). La clé est dérivée du mot de passe par PBKDF2-HMAC-SHA256.
///
/// Format du fichier :
///   "GHOSTAES"(8) | version(1)=1 | iterations(4, BE) | salt(16) | noncePrefix(4) | chunkSize(4, BE)
///   puis, répété jusqu'à EOF :  cipherLen(4, BE) | tag(16) | ciphertext(cipherLen)
///
/// Chaque bloc utilise un nonce de 12 octets = noncePrefix(4) || compteur(8, BE),
/// garantissant l'unicité du nonce pour une clé donnée (sel + préfixe aléatoires à
/// chaque chiffrement). Toute altération est détectée (échec d'authentification GCM).
/// </summary>
public sealed class GcmFileCrypto
{
    public const int KeySizeBytes = 32;        // AES-256
    public const int SaltSizeBytes = 16;
    public const int NoncePrefixBytes = 4;
    public const int NonceSizeBytes = 12;      // GCM standard
    public const int TagSizeBytes = 16;
    public const int DefaultChunkSize = 1 << 20; // 1 Mio
    public const int DefaultIterations = 210_000;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GHOSTAES");

    private readonly int _iterations;
    private readonly int _chunkSize;

    public GcmFileCrypto(int iterations = DefaultIterations, int chunkSize = DefaultChunkSize)
    {
        if (iterations < 10_000) throw new ArgumentOutOfRangeException(nameof(iterations));
        if (chunkSize < 1024) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        _iterations = iterations;
        _chunkSize = chunkSize;
    }

    public async Task EncryptAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Mot de passe vide.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixBytes);
        var key = DeriveKey(password, salt, _iterations);

        // En-tête
        await output.WriteAsync(Magic, ct).ConfigureAwait(false);
        output.WriteByte(1);
        await WriteInt32Async(output, _iterations, ct).ConfigureAwait(false);
        await output.WriteAsync(salt, ct).ConfigureAwait(false);
        await output.WriteAsync(noncePrefix, ct).ConfigureAwait(false);
        await WriteInt32Async(output, _chunkSize, ct).ConfigureAwait(false);

        using var aes = new AesGcm(key, TagSizeBytes);
        var plaintext = new byte[_chunkSize];
        var nonce = new byte[NonceSizeBytes];
        noncePrefix.CopyTo(nonce, 0);
        var tag = new byte[TagSizeBytes];
        long counter = 0;

        while (true)
        {
            var read = await ReadFullyAsync(input, plaintext, ct).ConfigureAwait(false);
            if (read == 0) break;

            BinaryPrimitives.WriteInt64BigEndian(nonce.AsSpan(NoncePrefixBytes), counter);
            var cipher = new byte[read];
            aes.Encrypt(nonce, plaintext.AsSpan(0, read), cipher, tag);

            await WriteInt32Async(output, read, ct).ConfigureAwait(false);
            await output.WriteAsync(tag, ct).ConfigureAwait(false);
            await output.WriteAsync(cipher, ct).ConfigureAwait(false);

            counter++;
            if (read < _chunkSize) break;
        }

        CryptographicOperations.ZeroMemory(key);
    }

    public async Task DecryptAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var magic = new byte[Magic.Length];
        if (await ReadFullyAsync(input, magic, ct).ConfigureAwait(false) != magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Fichier non reconnu (en-tête GHOSTAES absent).");

        var version = input.ReadByte();
        if (version != 1) throw new InvalidDataException($"Version de format non prise en charge : {version}.");

        var iterations = await ReadInt32Async(input, ct).ConfigureAwait(false);
        var salt = new byte[SaltSizeBytes];
        if (await ReadFullyAsync(input, salt, ct).ConfigureAwait(false) != SaltSizeBytes)
            throw new InvalidDataException("Sel tronqué.");
        var noncePrefix = new byte[NoncePrefixBytes];
        if (await ReadFullyAsync(input, noncePrefix, ct).ConfigureAwait(false) != NoncePrefixBytes)
            throw new InvalidDataException("Préfixe de nonce tronqué.");
        var chunkSize = await ReadInt32Async(input, ct).ConfigureAwait(false);
        if (chunkSize is < 1024 or > (1 << 27))
            throw new InvalidDataException("Taille de bloc invalide.");

        var key = DeriveKey(password, salt, iterations);
        using var aes = new AesGcm(key, TagSizeBytes);

        var nonce = new byte[NonceSizeBytes];
        noncePrefix.CopyTo(nonce, 0);
        var tag = new byte[TagSizeBytes];
        long counter = 0;

        while (true)
        {
            var lenBytes = new byte[4];
            var got = await ReadFullyAsync(input, lenBytes, ct).ConfigureAwait(false);
            if (got == 0) break; // fin propre
            if (got != 4) throw new InvalidDataException("Longueur de bloc tronquée.");

            var cipherLen = BinaryPrimitives.ReadInt32BigEndian(lenBytes);
            if (cipherLen is < 0 or > (1 << 27)) throw new InvalidDataException("Longueur de bloc invalide.");

            if (await ReadFullyAsync(input, tag, ct).ConfigureAwait(false) != TagSizeBytes)
                throw new InvalidDataException("Étiquette d'authentification tronquée.");

            var cipher = new byte[cipherLen];
            if (await ReadFullyAsync(input, cipher, ct).ConfigureAwait(false) != cipherLen)
                throw new InvalidDataException("Bloc chiffré tronqué.");

            BinaryPrimitives.WriteInt64BigEndian(nonce.AsSpan(NoncePrefixBytes), counter);
            var plain = new byte[cipherLen];
            try
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            catch (AuthenticationTagMismatchException)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new CryptographicException(
                    "Déchiffrement impossible : mot de passe incorrect ou fichier altéré.");
            }

            await output.WriteAsync(plain, 0, cipherLen, ct).ConfigureAwait(false);
            counter++;
        }

        CryptographicOperations.ZeroMemory(key);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySizeBytes);

    private static async Task WriteInt32Async(Stream s, int value, CancellationToken ct)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        await s.WriteAsync(buf, ct).ConfigureAwait(false);
    }

    private static async Task<int> ReadInt32Async(Stream s, CancellationToken ct)
    {
        var buf = new byte[4];
        if (await ReadFullyAsync(s, buf, ct).ConfigureAwait(false) != 4)
            throw new InvalidDataException("Entier 32 bits tronqué.");
        return BinaryPrimitives.ReadInt32BigEndian(buf);
    }

    /// <summary>Lit jusqu'à remplir le tampon ; renvoie le nombre d'octets réellement lus.</summary>
    private static async Task<int> ReadFullyAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await s.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
