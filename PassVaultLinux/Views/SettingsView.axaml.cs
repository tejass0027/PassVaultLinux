using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PassVaultLinux.Controls;

namespace PassVaultLinux.Views;

public partial class SettingsView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onBack;
    private readonly Action _onChangePattern;
    private readonly Action _onChangeSecurityQuestions;
    private readonly Action _onExportBackup;
    private readonly Action _onImportBackup;
    private readonly Action _onOpenLoginActivity;
    private readonly Action _onErased;

    private readonly List<ThemeOption> _themeOptions = new()
    {
        new ThemeOption(ThemeMode.System, "System default"),
        new ThemeOption(ThemeMode.Light, "Light"),
        new ThemeOption(ThemeMode.Dark, "Dark")
    };

    private readonly List<AutoLockOption> _autoLockOptions = new()
    {
        new AutoLockOption(0, "Immediately"),
        new AutoLockOption(30, "After 30 seconds"),
        new AutoLockOption(60, "After 1 minute"),
        new AutoLockOption(300, "After 5 minutes")
    };

    private bool _isInitializing = true;

    public SettingsView(
        AppState appState,
        Action onBack,
        Action onChangePattern,
        Action onChangeSecurityQuestions,
        Action onExportBackup,
        Action onImportBackup,
        Action onOpenLoginActivity,
        Action onErased)
    {
        InitializeComponent();
        _appState = appState;
        _onBack = onBack;
        _onChangePattern = onChangePattern;
        _onChangeSecurityQuestions = onChangeSecurityQuestions;
        _onExportBackup = onExportBackup;
        _onImportBackup = onImportBackup;
        _onOpenLoginActivity = onOpenLoginActivity;
        _onErased = onErased;

        ThemeCombo.ItemsSource = _themeOptions;
        ThemeCombo.SelectedIndex = Math.Max(0, _themeOptions.FindIndex(o => o.Mode == _appState.CurrentThemeMode()));

        AutoLockCombo.ItemsSource = _autoLockOptions;
        var currentSeconds = _appState.AuthPrefs.AutoLockSeconds;
        var idx = _autoLockOptions.FindIndex(o => o.Seconds == currentSeconds);
        AutoLockCombo.SelectedIndex = idx >= 0 ? idx : 1;

        _isInitializing = false;
    }

    private void ThemeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || ThemeCombo.SelectedItem is not ThemeOption selected)
        {
            return;
        }
        _appState.SetThemeMode(selected.Mode);
    }

    private void AutoLockCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || AutoLockCombo.SelectedItem is not AutoLockOption selected)
        {
            return;
        }
        _appState.SetAutoLockSeconds(selected.Seconds);
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private void ChangePattern_Click(object? sender, RoutedEventArgs e) => _onChangePattern();

    private void ChangeSecurityQuestions_Click(object? sender, RoutedEventArgs e) => _onChangeSecurityQuestions();

    private void ExportBackup_Click(object? sender, RoutedEventArgs e) => _onExportBackup();

    private void ImportBackup_Click(object? sender, RoutedEventArgs e) => _onImportBackup();

    private void LoginActivity_Click(object? sender, RoutedEventArgs e) => _onOpenLoginActivity();

    private async void Erase_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
        {
            return;
        }
        bool confirmed = await ConfirmDialog.ShowAsync(
            owner,
            "Erase all data?",
            "This permanently deletes every saved password, your pattern, and your security questions from this computer. This cannot be undone.");
        if (confirmed)
        {
            _appState.EraseEverything();
            _onErased();
        }
    }

    private class ThemeOption
    {
        public ThemeMode Mode { get; }
        private string Label { get; }

        public ThemeOption(ThemeMode mode, string label)
        {
            Mode = mode;
            Label = label;
        }

        public override string ToString() => Label;
    }

    private class AutoLockOption
    {
        public int Seconds { get; }
        private string Label { get; }

        public AutoLockOption(int seconds, string label)
        {
            Seconds = seconds;
            Label = label;
        }

        public override string ToString() => Label;
    }
}
