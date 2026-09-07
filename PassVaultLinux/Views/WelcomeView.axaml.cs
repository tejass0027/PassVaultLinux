using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PassVaultLinux.Views;

public partial class WelcomeView : UserControl
{
    private readonly Action _onGetStarted;

    public WelcomeView(Action onGetStarted)
    {
        InitializeComponent();
        _onGetStarted = onGetStarted;
    }

    private void GetStarted_Click(object? sender, RoutedEventArgs e) => _onGetStarted();
}
