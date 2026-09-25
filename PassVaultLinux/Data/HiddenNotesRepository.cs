using System.IO;
using System.Text;
using PassVaultLinux.Crypto;

namespace PassVaultLinux.Data;

/// <summary>The hidden vault's notes, encrypted with the hidden vault's own DEK.</summary>
public class HiddenNotesRepository
{
    private readonly string _notesFilePath;
    private byte[]? _dek;

    public event Action? NotesChanged;
    public List<HiddenNote> Notes { get; private set; } = new();

    public bool IsUnlocked => _dek != null;

    public HiddenNotesRepository(string appDataDir)
    {
        _notesFilePath = Path.Combine(appDataDir, "hidden_notes.dat");
    }

    public async Task UnlockAsync(byte[] dek)
    {
        _dek = dek;
        Notes = await Task.Run(() => LoadFromDisk(dek));
        NotesChanged?.Invoke();
    }

    public void Lock()
    {
        if (_dek != null)
        {
            Array.Clear(_dek, 0, _dek.Length);
        }
        _dek = null;
        Notes = new List<HiddenNote>();
        NotesChanged?.Invoke();
    }

    public async Task UpsertAsync(HiddenNote note)
    {
        var index = Notes.FindIndex(n => n.Id == note.Id);
        if (index >= 0)
        {
            Notes[index] = note;
        }
        else
        {
            Notes.Add(note);
        }
        NotesChanged?.Invoke();
        await PersistAsync();
    }

    public async Task DeleteAsync(string id)
    {
        Notes.RemoveAll(n => n.Id == id);
        NotesChanged?.Invoke();
        await PersistAsync();
    }

    public void DeleteVaultFile()
    {
        if (File.Exists(_notesFilePath))
        {
            File.Delete(_notesFilePath);
        }
    }

    private List<HiddenNote> LoadFromDisk(byte[] key)
    {
        if (!File.Exists(_notesFilePath) || new FileInfo(_notesFilePath).Length == 0)
        {
            return new List<HiddenNote>();
        }
        try
        {
            var plaintext = CryptoManager.Decrypt(File.ReadAllBytes(_notesFilePath), key);
            return HiddenNote.ListFromJson(Encoding.UTF8.GetString(plaintext));
        }
        catch (Exception)
        {
            return new List<HiddenNote>();
        }
    }

    private Task PersistAsync()
    {
        var key = _dek;
        if (key == null)
        {
            return Task.CompletedTask;
        }
        var snapshot = new List<HiddenNote>(Notes);
        return Task.Run(() =>
        {
            var json = HiddenNote.ListToJson(snapshot);
            File.WriteAllBytes(_notesFilePath, CryptoManager.Encrypt(Encoding.UTF8.GetBytes(json), key));
        });
    }
}
