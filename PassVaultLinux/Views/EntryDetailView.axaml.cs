using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PassVaultLinux.Controls;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

public partial class EntryDetailView : UserControl
{
    private readonly VaultRepository _repository;
    private readonly Credential _credential;
    private readonly Action _onBack;
    private readonly Action _onEdit;
    private readonly Action _onDeleted;
    private bool _passwordVisible;
    private DispatcherTimer? _clipboardClearTimer;
    private string? _expectedClipboardValue;

    public EntryDetailView(AppState appState, Credential credential, Action onBack, Action onEdit, Action onDeleted, VaultRepository? repository = null)
    {
        InitializeComponent();
        _repository = repository ?? appState.VaultRepository;
        _credential = credential;
        _onBack = onBack;
        _onEdit = onEdit;
        _onDeleted = onDeleted;

        TitleText.Text = string.IsNullOrEmpty(credential.Title) ? "(untitled)" : credential.Title;
        UsernameText.Text = credential.Username;
        UpdatePasswordDisplay();

        if (!string.IsNullOrEmpty(credential.Url))
        {
            UrlPanel.IsVisible = true;
            UrlText.Text = credential.Url;
        }
        if (!string.IsNullOrEmpty(credential.Notes))
        {
            NotesPanel.IsVisible = true;
            NotesText.Text = credential.Notes;
        }
    }

    private void UpdatePasswordDisplay()
    {
        PasswordText.Text = _passwordVisible
            ? _credential.Password
            : new string('•', Math.Min(_credential.Password.Length, 16));
        ToggleShowButton.Content = _passwordVisible ? "Hide" : "Show";
    }

    private void ToggleShow_Click(object? sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        UpdatePasswordDisplay();
    }

    private void CopyUsername_Click(object? sender, RoutedEventArgs e) => CopyWithAutoClear(_credential.Username);

    private void CopyPassword_Click(object? sender, RoutedEventArgs e) => CopyWithAutoClear(_credential.Password);

    private void CopyUrl_Click(object? sender, RoutedEventArgs e) => CopyWithAutoClear(_credential.Url);

    private async void CopyWithAutoClear(string value)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            return;
        }
        await clipboard.SetTextAsync(value);
        _expectedClipboardValue = value;

        _clipboardClearTimer?.Stop();
        _clipboardClearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clipboardClearTimer.Tick += async (_, _) =>
        {
            _clipboardClearTimer!.Stop();
            try
            {
                var current = await clipboard.TryGetTextAsync();
                if (current == _expectedClipboardValue)
                {
                    await clipboard.ClearAsync();
                }
            }
            catch (Exception)
            {
                // Clipboard can be inaccessible depending on the desktop environment; not
                // worth surfacing an error for this.
            }
        };
        _clipboardClearTimer.Start();
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private void Edit_Click(object? sender, RoutedEventArgs e) => _onEdit();

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
        {
            return;
        }
        bool confirmed = await ConfirmDialog.ShowAsync(owner, "Delete this entry?", $"\"{TitleText.Text}\" will be permanently deleted.");
        if (confirmed)
        {
            await _repository.DeleteAsync(_credential.Id);
            _onDeleted();
        }
    }
}
