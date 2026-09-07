using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PassVaultLinux.Controls;

/// <summary>A text field masked like a password by default, with a Show/Hide toggle button.</summary>
public partial class PasswordRevealBox : UserControl
{
    private bool _isRevealed;

    public PasswordRevealBox()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => Box.Text ?? "";
        set => Box.Text = value;
    }

    public event EventHandler? TextChanged;

    private void Box_TextChanged(object? sender, TextChangedEventArgs e) => TextChanged?.Invoke(this, EventArgs.Empty);

    private void ToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        _isRevealed = !_isRevealed;
        Box.PasswordChar = _isRevealed ? '\0' : '•';
        ToggleButton.Content = _isRevealed ? "Hide" : "Show";
        Box.Focus();
        Box.CaretIndex = Box.Text?.Length ?? 0;
    }
}
