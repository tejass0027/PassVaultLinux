using PassVaultLinux.Crypto;

namespace PassVaultLinux.Auth;

/// <summary>
/// The hidden vault's own pattern: a second pattern, separate from the main login pattern, that
/// wraps a separate DEK. Neither pattern can unlock the other vault.
/// </summary>
public class HiddenVaultAuthManager
{
    private readonly AuthPrefs _authPrefs;

    public HiddenVaultAuthManager(AuthPrefs authPrefs)
    {
        _authPrefs = authPrefs;
    }

    public bool HasHiddenVault() => _authPrefs.HiddenPatternSalt() != null && _authPrefs.HiddenPatternWrappedDek() != null;

    public void SetPattern(List<int> pattern, byte[] dek)
    {
        if (pattern.Count < PatternAuthManager.MinPatternLength)
        {
            throw new ArgumentException($"Pattern must connect at least {PatternAuthManager.MinPatternLength} dots");
        }
        var salt = KeyDerivation.NewSalt();
        var wrapped = DekWrapper.Wrap(dek, PatternToSecret(pattern), salt);
        _authPrefs.SaveHiddenPatternWrap(salt, wrapped);
    }

    public byte[]? TryUnlock(List<int> pattern)
    {
        var salt = _authPrefs.HiddenPatternSalt();
        var wrapped = _authPrefs.HiddenPatternWrappedDek();
        if (salt == null || wrapped == null)
        {
            return null;
        }
        return DekWrapper.TryUnwrap(wrapped, PatternToSecret(pattern), salt);
    }

    private static string PatternToSecret(List<int> pattern) => string.Join(",", pattern);
}
