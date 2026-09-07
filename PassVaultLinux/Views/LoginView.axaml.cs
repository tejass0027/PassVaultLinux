using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PassVaultLinux.Views;

public partial class LoginView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onLoginSuccess;
    private readonly Action _onForgotPattern;
    private bool _isVerifyingPattern;

    public LoginView(AppState appState, Action onLoginSuccess, Action onForgotPattern)
    {
        InitializeComponent();
        _appState = appState;
        _onLoginSuccess = onLoginSuccess;
        _onForgotPattern = onForgotPattern;
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

        bool success = await _appState.TryLoginWithPatternAsync(pattern);

        VerifyingProgress.IsVisible = false;
        _isVerifyingPattern = false;
        if (success)
        {
            _onLoginSuccess();
        }
        else
        {
            StatusText.Text = "Wrong pattern, try again";
            PatternControl.ShowError = true;
            await Task.Delay(500);
            PatternControl.ShowError = false;
        }
    }

    private void ForgotPattern_Click(object? sender, RoutedEventArgs e) => _onForgotPattern();
}
