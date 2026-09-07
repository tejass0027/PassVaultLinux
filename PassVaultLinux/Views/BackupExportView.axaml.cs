using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

public partial class BackupExportView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onDone;
    private readonly Action _onCancel;

    public BackupExportView(AppState appState, Action onDone, Action onCancel)
    {
        InitializeComponent();
        _appState = appState;
        _onDone = onDone;
        _onCancel = onCancel;
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Text;
        var confirm = ConfirmPasswordBox.Text;

        if (password.Length < 6)
        {
            ShowError("Use a password of at least 6 characters");
            return;
        }
        if (password != confirm)
        {
            ShowError("Passwords don't match");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save backup",
            SuggestedFileName = "passvault-backup.pvbk",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PassVault backup (*.pvbk)") { Patterns = new[] { "*.pvbk" } },
                FilePickerFileTypes.All
            }
        });
        if (file == null)
        {
            return;
        }

        ErrorText.IsVisible = false;
        StatusText.IsVisible = false;
        SaveButton.IsEnabled = false;
        Progress.IsVisible = true;

        try
        {
            var credentials = _appState.VaultRepository.Credentials;
            await using (var stream = await file.OpenWriteAsync())
            {
                await Task.Run(() => BackupManager.Export(stream, credentials, password));
            }
            StatusText.Text = "Backup saved.";
            StatusText.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't write the backup file: {ex.Message}");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            Progress.IsVisible = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void Done_Click(object? sender, RoutedEventArgs e) => _onDone();

    private void Cancel_Click(object? sender, RoutedEventArgs e) => _onCancel();
}
