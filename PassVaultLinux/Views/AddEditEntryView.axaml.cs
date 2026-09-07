using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

public partial class AddEditEntryView : UserControl
{
    private readonly Credential? _existing;
    private readonly Func<Credential, Task> _onSave;
    private readonly Action _onBack;

    public AddEditEntryView(Credential? existing, Func<Credential, Task> onSave, Action onBack)
    {
        InitializeComponent();
        _existing = existing;
        _onSave = onSave;
        _onBack = onBack;

        TitleBarText.Text = existing == null ? "Add password" : "Edit password";
        TitleBox.Text = existing?.Title ?? "";
        UsernameBox.Text = existing?.Username ?? "";
        PasswordBox.Text = existing?.Password ?? "";
        UrlBox.Text = existing?.Url ?? "";
        NotesBox.Text = existing?.Notes ?? "";

        PasswordBox.TextChanged += (_, _) => UpdateStrength();
        UpdateStrength();
    }

    private void UpdateStrength()
    {
        var password = PasswordBox.Text;
        int score = StrengthScore(password);
        StrengthBar.Value = score;

        if (string.IsNullOrEmpty(password))
        {
            StrengthLabel.Text = "";
            StrengthBar.Foreground = (IBrush)this.FindResource("OutlineBrush")!;
        }
        else if (score <= 2)
        {
            StrengthLabel.Text = "Weak";
            StrengthLabel.Foreground = Brushes.Crimson;
            StrengthBar.Foreground = Brushes.Crimson;
        }
        else if (score <= 4)
        {
            StrengthLabel.Text = "Okay";
            StrengthLabel.Foreground = Brushes.DarkOrange;
            StrengthBar.Foreground = Brushes.DarkOrange;
        }
        else
        {
            StrengthLabel.Text = "Strong";
            StrengthLabel.Foreground = (IBrush)this.FindResource("SuccessBrush")!;
            StrengthBar.Foreground = (IBrush)this.FindResource("SuccessBrush")!;
        }
    }

    private static int StrengthScore(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }
        int score = 0;
        if (password.Length >= 8)
        {
            score++;
        }
        if (password.Length >= 12)
        {
            score++;
        }
        if (password.Any(char.IsUpper))
        {
            score++;
        }
        if (password.Any(char.IsLower))
        {
            score++;
        }
        if (password.Any(char.IsDigit))
        {
            score++;
        }
        if (password.Any(c => !char.IsLetterOrDigit(c)))
        {
            score++;
        }
        return score;
    }

    private async void Generate_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
        {
            return;
        }
        var dialog = new PasswordGeneratorWindow();
        var result = await dialog.ShowDialog<string?>(owner);
        if (result != null)
        {
            PasswordBox.Text = result;
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Text))
        {
            ErrorText.IsVisible = true;
            return;
        }
        ErrorText.IsVisible = false;

        var credential = new Credential
        {
            Id = _existing?.Id ?? Guid.NewGuid().ToString(),
            Title = TitleBox.Text,
            Username = UsernameBox.Text ?? "",
            Password = PasswordBox.Text,
            Url = UrlBox.Text ?? "",
            Notes = NotesBox.Text ?? "",
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await _onSave(credential);
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();
}
