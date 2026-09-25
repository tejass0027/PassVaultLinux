using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

/// <summary>The hidden vault's own password list, fed from its separately-encrypted store.</summary>
public partial class HiddenPasswordsView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onAdd;
    private readonly Action<Credential> _onOpen;
    private readonly Action _onBack;

    public HiddenPasswordsView(AppState appState, Action onAdd, Action<Credential> onOpen, Action onBack)
    {
        InitializeComponent();
        _appState = appState;
        _onAdd = onAdd;
        _onOpen = onOpen;
        _onBack = onBack;

        _appState.HiddenVaultRepository.CredentialsChanged += RefreshList;
        RefreshList();
    }

    private void RefreshList()
    {
        var items = _appState.HiddenVaultRepository.Credentials
            .OrderBy(c => c.Title.ToLowerInvariant())
            .Select(c => new CredentialListItem(c))
            .ToList();
        CredentialsList.ItemsSource = items;
        EmptyText.IsVisible = items.Count == 0;
    }

    private void CredentialsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CredentialsList.SelectedItem is CredentialListItem item)
        {
            CredentialsList.SelectedItem = null;
            _onOpen(item.Credential);
        }
    }

    private void Add_Click(object? sender, RoutedEventArgs e) => _onAdd();

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private class CredentialListItem
    {
        public Credential Credential { get; }
        public string Title => string.IsNullOrEmpty(Credential.Title) ? "(untitled)" : Credential.Title;
        public string Username => Credential.Username;

        public CredentialListItem(Credential credential)
        {
            Credential = credential;
        }
    }
}
