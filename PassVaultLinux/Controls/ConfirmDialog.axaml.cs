using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PassVaultLinux.Controls;

/// <summary>A minimal Yes/Cancel confirmation dialog, since Avalonia has no built-in MessageBox.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner, string title, string message, bool destructive = true)
    {
        var dialog = new ConfirmDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        if (destructive)
        {
            dialog.YesButton.Foreground = (IBrush)dialog.FindResource("ErrorBrush")!;
        }
        return await dialog.ShowDialog<bool>(owner);
    }

    private void Yes_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void No_Click(object? sender, RoutedEventArgs e) => Close(false);
}
