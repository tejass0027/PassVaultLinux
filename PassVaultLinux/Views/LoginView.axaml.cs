using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PassVaultLinux.Views;

public partial class LoginView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onLoginSuccess;
    private readonly Action _onForgotPattern;
    private readonly Action _onHiddenVaultUnlocked;
    private bool _isVerifyingPattern;

    public LoginView(AppState appState, Action onLoginSuccess, Action onForgotPattern, Action onHiddenVaultUnlocked)
    {
        InitializeComponent();
        _appState = appState;
        _onLoginSuccess = onLoginSuccess;
        _onForgotPattern = onForgotPattern;
        _onHiddenVaultUnlocked = onHiddenVaultUnlocked;
        PatternControl.PatternCompleted += OnPatternCompleted;

        StatusText.Text = "Draw your pattern";
    }

    private async void OnPatternCompleted(List<int> pattern)
    {
        if (_isVerifyingPattern)
        {
            return;
        }
        _isVerifyingPattern = true;
        StatusText.Text = "Verifying...";
        VerifyingProgress.IsVisible = true;

        var result = await _appState.AttemptPatternLoginAsync(pattern);

        VerifyingProgress.IsVisible = false;
        _isVerifyingPattern = false;
        switch (result)
        {
            case AppState.PatternLoginResult.MainVault:
                _onLoginSuccess();
                break;
            case AppState.PatternLoginResult.HiddenVault:
                _onHiddenVaultUnlocked();
                break;
            default:
                StatusText.Text = "Wrong pattern, try again";
                PatternControl.ShowError = true;
                await Task.Delay(500);
                PatternControl.ShowError = false;
                break;
        }
    }

    private void ForgotPattern_Click(object? sender, RoutedEventArgs e) => _onForgotPattern();
}
