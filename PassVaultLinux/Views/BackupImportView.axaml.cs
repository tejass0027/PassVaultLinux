using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PassVaultLinux.Controls;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

public partial class BackupImportView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onDone;
    private readonly Action _onCancel;

    public BackupImportView(AppState appState, Action onDone, Action onCancel)
    {
        InitializeComponent();
        _appState = appState;
        _onDone = onDone;
        _onCancel = onCancel;
    }

    private async void ChooseFile_Click(object? sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Text;
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Enter the backup password first");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose backup file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PassVault backup (*.pvbk)") { Patterns = new[] { "*.pvbk" } },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0)
        {
            return;
        }
        var file = files[0];

        ErrorText.IsVisible = false;
        ChooseFileButton.IsEnabled = false;
        Progress.IsVisible = true;

        List<Credential>? imported = null;
        try
        {
            await using var stream = await file.OpenReadAsync();
            imported = await Task.Run(() => BackupManager.Import(stream, password));
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't read that file: {ex.Message}");
        }
        finally
        {
            ChooseFileButton.IsEnabled = true;
            Progress.IsVisible = false;
        }

        if (imported == null)
        {
            ShowError("Wrong password, or this isn't a valid PassVault backup file.");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
        {
            return;
        }
        bool confirmed = await ConfirmDialog.ShowAsync(
            owner,
            "Replace current vault?",
            $"This backup contains {imported.Count} saved password(s). Importing will replace everything currently in your vault on this computer.");
        if (confirmed)
        {
            await _appState.VaultRepository.ReplaceAllAsync(imported);
            _onDone();
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => _onCancel();
}
