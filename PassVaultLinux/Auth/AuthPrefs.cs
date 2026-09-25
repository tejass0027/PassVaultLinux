using System.IO;
using System.Text.Json;
using PassVaultLinux.Crypto;

namespace PassVaultLinux.Auth;

/// <summary>
/// Stores all non-secret vault metadata (salts, wrapped DEK copies, security question text,
/// settings, login activity) in a single JSON file, AES-256-GCM encrypted with a random key
/// that's generated once and kept in its own file with owner-only permissions (chmod 600) -
/// the Linux analogue of the Windows app's DPAPI-protected storage and the Android app's
/// Keystore-backed EncryptedSharedPreferences. Never stores the DEK or any plaintext vault data.
/// </summary>
public class AuthPrefs
{
    private const int MaxLoginEvents = 50;

    private readonly string _filePath;
    private readonly byte[] _machineKey;
    private AuthPrefsData _data;

    public AuthPrefs(string appDataDir)
    {
        _filePath = Path.Combine(appDataDir, "auth_prefs.dat");
        _machineKey = LoadOrCreateMachineKey(Path.Combine(appDataDir, "machine.key"));
        _data = Load();
    }

    public bool OnboardingComplete
    {
        get => _data.OnboardingComplete;
        set { _data.OnboardingComplete = value; Save(); }
    }

    public int AutoLockSeconds
    {
        get => _data.AutoLockSeconds;
        set { _data.AutoLockSeconds = value; Save(); }
    }

    public string ThemeMode
    {
        get => _data.ThemeMode;
        set { _data.ThemeMode = value; Save(); }
    }

    public void SavePatternWrap(byte[] salt, byte[] wrappedDek)
    {
        _data.PatternSalt = Convert.ToBase64String(salt);
        _data.PatternWrappedDek = Convert.ToBase64String(wrappedDek);
        Save();
    }

    public byte[]? PatternSalt() => _data.PatternSalt is null ? null : Convert.FromBase64String(_data.PatternSalt);
    public byte[]? PatternWrappedDek() => _data.PatternWrappedDek is null ? null : Convert.FromBase64String(_data.PatternWrappedDek);

    public void SaveSecurityWrap(byte[] salt, byte[] wrappedDek, List<string> questions)
    {
        _data.SecuritySalt = Convert.ToBase64String(salt);
        _data.SecurityWrappedDek = Convert.ToBase64String(wrappedDek);
        _data.SecurityQuestions = questions;
        Save();
    }

    public byte[]? SecuritySalt() => _data.SecuritySalt is null ? null : Convert.FromBase64String(_data.SecuritySalt);
    public byte[]? SecurityWrappedDek() => _data.SecurityWrappedDek is null ? null : Convert.FromBase64String(_data.SecurityWrappedDek);
    public List<string> SecurityQuestions() => _data.SecurityQuestions;

    /// <summary>A second pattern that unlocks a separate hidden vault with its own DEK; only reachable by drawing it on the login screen.</summary>
    public void SaveHiddenPatternWrap(byte[] salt, byte[] wrappedDek)
    {
        _data.HiddenPatternSalt = Convert.ToBase64String(salt);
        _data.HiddenPatternWrappedDek = Convert.ToBase64String(wrappedDek);
        Save();
    }

    public byte[]? HiddenPatternSalt() => _data.HiddenPatternSalt is null ? null : Convert.FromBase64String(_data.HiddenPatternSalt);
    public byte[]? HiddenPatternWrappedDek() => _data.HiddenPatternWrappedDek is null ? null : Convert.FromBase64String(_data.HiddenPatternWrappedDek);

    public void ClearHiddenVault()
    {
        _data.HiddenPatternSalt = null;
        _data.HiddenPatternWrappedDek = null;
        Save();
    }

    public void RecordLoginEvent(LoginEventType type, bool success)
    {
        _data.LoginEvents.Insert(0, new LoginEvent
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Type = type,
            Success = success
        });
        while (_data.LoginEvents.Count > MaxLoginEvents)
        {
            _data.LoginEvents.RemoveAt(_data.LoginEvents.Count - 1);
        }
        Save();
    }

    public List<LoginEvent> LoginEvents() => _data.LoginEvents;

    public void ClearAll()
    {
        _data = new AuthPrefsData();
        Save();
    }

    private static byte[] LoadOrCreateMachineKey(string keyPath)
    {
        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }
        var key = CryptoManager.GenerateRandomKey();
        File.WriteAllBytes(keyPath, key);
        try
        {
#pragma warning disable CA1416 // This app only ever runs on Linux; caught below for the build/dev machine anyway.
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#pragma warning restore CA1416
        }
        catch (PlatformNotSupportedException)
        {
            // Only relevant on non-Unix platforms, which this app doesn't target.
        }
        return key;
    }

    private AuthPrefsData Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AuthPrefsData();
            }
            var encrypted = File.ReadAllBytes(_filePath);
            var jsonBytes = CryptoManager.Decrypt(encrypted, _machineKey);
            var data = JsonSerializer.Deserialize<AuthPrefsData>(jsonBytes);
            return data ?? new AuthPrefsData();
        }
        catch (Exception)
        {
            return new AuthPrefsData();
        }
    }

    private void Save()
    {
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(_data);
        var encrypted = CryptoManager.Encrypt(jsonBytes, _machineKey);
        File.WriteAllBytes(_filePath, encrypted);
    }

    private class AuthPrefsData
    {
        public bool OnboardingComplete { get; set; }
        public int AutoLockSeconds { get; set; } = 30;
        public string ThemeMode { get; set; } = "System";
        public string? PatternSalt { get; set; }
        public string? PatternWrappedDek { get; set; }
        public string? SecuritySalt { get; set; }
        public string? SecurityWrappedDek { get; set; }
        public List<string> SecurityQuestions { get; set; } = new();
        public string? HiddenPatternSalt { get; set; }
        public string? HiddenPatternWrappedDek { get; set; }
        public List<LoginEvent> LoginEvents { get; set; } = new();
    }
}
