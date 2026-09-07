using System.Linq;
using System.Text;
using PassVaultLinux.Crypto;

namespace PassVaultLinux.Data;

/// <summary>
/// Manual, user-initiated encrypted backup of the vault contents, using the exact same file
/// format as the Android and Windows apps so a .pvbk file exported from any of them imports on
/// the others: "PVBK" magic + 1 version byte + 16-byte salt + AES-GCM ciphertext of the
/// credentials JSON, encrypted with a key derived from a backup password the user chooses at
/// export time - completely independent of the pattern/security-question system.
/// </summary>
public static class BackupManager
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PVBK");
    private const byte Version = 1;

    public static void Export(Stream output, List<Credential> credentials, string backupPassword)
    {
        var salt = KeyDerivation.NewSalt();
        var key = KeyDerivation.DeriveKey(backupPassword, salt);
        try
        {
            var json = Credential.ListToJson(credentials);
            var encrypted = CryptoManager.Encrypt(Encoding.UTF8.GetBytes(json), key);

            output.Write(Magic, 0, Magic.Length);
            output.WriteByte(Version);
            output.Write(salt, 0, salt.Length);
            output.Write(encrypted, 0, encrypted.Length);
            output.Flush();
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
        }
    }

    /// <summary>Returns the decrypted credential list, or null if <paramref name="backupPassword"/> is wrong or the file is invalid.</summary>
    public static List<Credential>? Import(Stream input, string backupPassword)
    {
        using var memoryStream = new MemoryStream();
        input.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        int headerSize = Magic.Length + 1 + KeyDerivation.SaltSizeBytes;
        if (bytes.Length <= headerSize)
        {
            return null;
        }

        var magic = bytes[..Magic.Length];
        if (!magic.SequenceEqual(Magic))
        {
            return null;
        }

        var salt = bytes[(Magic.Length + 1)..headerSize];
        var encrypted = bytes[headerSize..];

        var key = KeyDerivation.DeriveKey(backupPassword, salt);
        try
        {
            var plaintext = CryptoManager.Decrypt(encrypted, key);
            return Credential.ListFromJson(Encoding.UTF8.GetString(plaintext));
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
        }
    }
}
