using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using PassVaultLinux.Auth;
using PassVaultLinux.Crypto;
using PassVaultLinux.Data;

namespace PassVaultLinux;

/// <summary>
/// Single shared holder for auth/vault state across the whole app - mirrors the Android app's
/// VaultViewModel.kt and the Windows app's AppState.cs. Owns the encrypted-prefs store, the
/// pattern/security-question managers, both repositories, and the in-memory DEK for the
/// current session.
/// </summary>
public class AppState
{
    public AuthPrefs AuthPrefs { get; }
    public PatternAuthManager PatternAuth { get; }
    public SecurityQuestionManager SecurityQuestions { get; }
    public VaultRepository VaultRepository { get; }
    public PhotoVaultRepository PhotoVaultRepository { get; }
    public HiddenVaultAuthManager HiddenVaultAuth { get; }
    public HiddenNotesRepository HiddenNotesRepository { get; }
    public VaultRepository HiddenVaultRepository { get; }
    public PhotoVaultRepository HiddenPhotoVaultRepository { get; }

    public event Action<bool>? IsUnlockedChanged;
    private bool _isUnlocked;
    public bool IsUnlocked
    {
        get => _isUnlocked;
        private set { _isUnlocked = value; IsUnlockedChanged?.Invoke(value); }
    }

    public event Action<bool>? IsHiddenVaultUnlockedChanged;
    private bool _isHiddenVaultUnlocked;
    public bool IsHiddenVaultUnlocked
    {
        get => _isHiddenVaultUnlocked;
        private set { _isHiddenVaultUnlocked = value; IsHiddenVaultUnlockedChanged?.Invoke(value); }
    }

    public bool HasHiddenVault => HiddenVaultAuth.HasHiddenVault();

    public event Action<int>? FailedAttemptsSinceLastLoginChanged;
    private int _failedAttemptsSinceLastLogin;
    public int FailedAttemptsSinceLastLogin
    {
        get => _failedAttemptsSinceLastLogin;
        private set { _failedAttemptsSinceLastLogin = value; FailedAttemptsSinceLastLoginChanged?.Invoke(value); }
    }

    // Only held transiently in memory while stepping through onboarding. Wiped as soon as
    // onboarding finishes (or the process exits, since it's never persisted).
    private byte[]? _onboardingDek;

    public bool IsOnboarded => AuthPrefs.OnboardingComplete;

    public AppState()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = string.IsNullOrEmpty(dataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : dataHome;
        var appDataDir = Path.Combine(baseDir, "PassVault");
        Directory.CreateDirectory(appDataDir);

        AuthPrefs = new AuthPrefs(appDataDir);
        PatternAuth = new PatternAuthManager(AuthPrefs);
        SecurityQuestions = new SecurityQuestionManager(AuthPrefs);
        VaultRepository = new VaultRepository(appDataDir);
        PhotoVaultRepository = new PhotoVaultRepository(appDataDir);
        HiddenVaultAuth = new HiddenVaultAuthManager(AuthPrefs);
        HiddenNotesRepository = new HiddenNotesRepository(appDataDir);
        HiddenVaultRepository = new VaultRepository(appDataDir, "hidden_vault.dat");
        HiddenPhotoVaultRepository = new PhotoVaultRepository(appDataDir, "hidden_photos_index.dat", "hidden_photos");

        ApplyTheme(CurrentThemeMode());
    }

    // --- Onboarding ---
    // Pattern/security-question setup and verification all run PBKDF2 (210k iterations,
    // intentionally slow to resist brute-forcing) on a background thread via Task.Run inside
    // the managers/DekWrapper's callers below, so the UI never freezes while it computes.

    public async Task BeginOnboardingWithPatternAsync(List<int> pattern)
    {
        var dek = CryptoManager.GenerateRandomKey();
        _onboardingDek = dek;
        await Task.Run(() => PatternAuth.SetPattern(pattern, dek));
    }

    public async Task FinishOnboardingAsync(List<string> questions, List<string> answers)
    {
        var dek = _onboardingDek ?? throw new InvalidOperationException("Pattern must be set before security questions");
        await Task.Run(() => SecurityQuestions.SetQuestions(questions, answers, dek));
        AuthPrefs.OnboardingComplete = true;
        await VaultRepository.UnlockAsync(dek);
        await PhotoVaultRepository.UnlockAsync(dek);
        IsUnlocked = true;
        _onboardingDek = null;
    }

    // --- Login ---

    public enum PatternLoginResult { MainVault, HiddenVault, WrongPattern }

    /// <summary>
    /// Tries the pattern against the main vault first, then the hidden vault, so a correct
    /// hidden-vault pattern is never logged as a failed main-vault attempt (and is recorded
    /// nowhere, keeping the hidden vault invisible even in the login activity log).
    /// </summary>
    public async Task<PatternLoginResult> AttemptPatternLoginAsync(List<int> pattern)
    {
        var mainDek = await Task.Run(() => PatternAuth.TryUnlock(pattern));
        if (mainDek != null)
        {
            FailedAttemptsSinceLastLogin = FailedAttemptsSinceLastRecordedSuccess();
            AuthPrefs.RecordLoginEvent(LoginEventType.Pattern, success: true);
            await VaultRepository.UnlockAsync(mainDek);
            await PhotoVaultRepository.UnlockAsync(mainDek);
            IsUnlocked = true;
            return PatternLoginResult.MainVault;
        }

        var hiddenDek = await Task.Run(() => HiddenVaultAuth.TryUnlock(pattern));
        if (hiddenDek != null)
        {
            await HiddenNotesRepository.UnlockAsync(hiddenDek);
            await HiddenVaultRepository.UnlockAsync(hiddenDek);
            await HiddenPhotoVaultRepository.UnlockAsync(hiddenDek);
            IsHiddenVaultUnlocked = true;
            return PatternLoginResult.HiddenVault;
        }

        AuthPrefs.RecordLoginEvent(LoginEventType.Pattern, success: false);
        return PatternLoginResult.WrongPattern;
    }

    public void LockHiddenVault()
    {
        HiddenPhotoVaultRepository.Lock();
        HiddenVaultRepository.Lock();
        HiddenNotesRepository.Lock();
        IsHiddenVaultUnlocked = false;
    }

    // Held only while changing the hidden pattern from Settings, so the existing hidden DEK is
    // re-wrapped instead of replaced (which would orphan everything stored in the hidden vault).
    private byte[]? _pendingHiddenVaultDek;

    public async Task<bool> BeginHiddenVaultPatternChangeAsync(List<int> currentPattern)
    {
        var dek = await Task.Run(() => HiddenVaultAuth.TryUnlock(currentPattern));
        if (dek == null)
        {
            return false;
        }
        _pendingHiddenVaultDek = dek;
        return true;
    }

    /// <summary>Rejects a pattern identical to the main login pattern, since the two must be distinguishable.</summary>
    public async Task<bool> SetupHiddenVaultAsync(List<int> pattern)
    {
        var collidesWithMainPattern = await Task.Run(() => PatternAuth.TryUnlock(pattern) != null);
        if (collidesWithMainPattern)
        {
            return false;
        }

        var dek = _pendingHiddenVaultDek ?? CryptoManager.GenerateRandomKey();
        _pendingHiddenVaultDek = null;
        await Task.Run(() => HiddenVaultAuth.SetPattern(pattern, dek));
        return true;
    }

    public void RemoveHiddenVault()
    {
        LockHiddenVault();
        AuthPrefs.ClearHiddenVault();
        HiddenNotesRepository.DeleteVaultFile();
        HiddenVaultRepository.DeleteVaultFile();
        HiddenPhotoVaultRepository.DeleteAll();
    }

    public async Task<bool> VerifySecurityAnswersAsync(List<string> answers) =>
        await Task.Run(() => SecurityQuestions.TryUnlock(answers) != null);

    /// <summary>Recovery path: verifies security answers and, if correct, sets a brand new pattern.</summary>
    public async Task<bool> RecoverWithSecurityAnswersAsync(List<string> answers, List<int> newPattern)
    {
        var dek = await Task.Run(() =>
        {
            var unwrapped = SecurityQuestions.TryUnlock(answers);
            if (unwrapped == null)
            {
                return null;
            }
            PatternAuth.SetPattern(newPattern, unwrapped);
            return unwrapped;
        });
        if (dek == null)
        {
            return false;
        }
        FailedAttemptsSinceLastLogin = FailedAttemptsSinceLastRecordedSuccess();
        AuthPrefs.RecordLoginEvent(LoginEventType.Recovery, success: true);
        await VaultRepository.UnlockAsync(dek);
        await PhotoVaultRepository.UnlockAsync(dek);
        IsUnlocked = true;
        return true;
    }

    public void DismissFailedAttemptsBanner() => FailedAttemptsSinceLastLogin = 0;

    public List<LoginEvent> GetLoginEvents() => AuthPrefs.LoginEvents();

    private int FailedAttemptsSinceLastRecordedSuccess()
    {
        int count = 0;
        foreach (var evt in AuthPrefs.LoginEvents())
        {
            if (evt.Success)
            {
                break;
            }
            count++;
        }
        return count;
    }

    public void Lock()
    {
        VaultRepository.Lock();
        PhotoVaultRepository.Lock();
        IsUnlocked = false;
    }

    // --- Settings ---

    public void SetAutoLockSeconds(int seconds) => AuthPrefs.AutoLockSeconds = seconds;

    public async Task ChangePatternAsync(List<int> newPattern)
    {
        var dek = VaultRepository.CurrentDek() ?? throw new InvalidOperationException("Vault must be unlocked to change pattern");
        await Task.Run(() => PatternAuth.SetPattern(newPattern, dek));
    }

    public async Task ChangeSecurityQuestionsAsync(List<string> questions, List<string> answers)
    {
        var dek = VaultRepository.CurrentDek() ?? throw new InvalidOperationException("Vault must be unlocked to change security questions");
        await Task.Run(() => SecurityQuestions.SetQuestions(questions, answers, dek));
    }

    public void EraseEverything()
    {
        Lock();
        AuthPrefs.ClearAll();
        VaultRepository.DeleteVaultFile();
        PhotoVaultRepository.DeleteAll();
    }

    // --- Theme ---

    public ThemeMode CurrentThemeMode() =>
        Enum.TryParse<ThemeMode>(AuthPrefs.ThemeMode, out var mode) ? mode : ThemeMode.System;

    public void SetThemeMode(ThemeMode mode)
    {
        AuthPrefs.ThemeMode = mode.ToString();
        ApplyTheme(mode);
    }

    private void ApplyTheme(ThemeMode mode)
    {
        bool dark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => IsSystemInDarkMode()
        };
        var uri = dark
            ? "avares://PassVaultLinux/Themes/DarkTheme.axaml"
            : "avares://PassVaultLinux/Themes/LightTheme.axaml";
        var dict = (IResourceProvider)AvaloniaXamlLoader.Load(new Uri(uri));
        var app = Application.Current!;
        if (app.Resources.MergedDictionaries.Count > 0)
        {
            app.Resources.MergedDictionaries[0] = dict;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(dict);
        }
    }

    private static bool IsSystemInDarkMode()
    {
        try
        {
            var variant = Application.Current?.PlatformSettings?.GetColorValues()?.ThemeVariant;
            return variant == PlatformThemeVariant.Dark;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
