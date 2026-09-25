using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PassVaultLinux.Controls;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

/// <summary>The hidden vault's secret notes: a simple list with an inline editor.</summary>
public partial class HiddenNotesView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onBack;
    private HiddenNote? _editing;

    public HiddenNotesView(AppState appState, Action onBack)
    {
        InitializeComponent();
        _appState = appState;
        _onBack = onBack;

        _appState.HiddenNotesRepository.NotesChanged += RefreshList;
        RefreshList();
    }

    private void RefreshList()
    {
        var items = _appState.HiddenNotesRepository.Notes
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteListItem(n))
            .ToList();
        NotesList.ItemsSource = items;
        EmptyText.IsVisible = items.Count == 0;
    }

    private void NotesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NotesList.SelectedItem is NoteListItem item)
        {
            NotesList.SelectedItem = null;
            ShowEditor(item.Note);
        }
    }

    private void Add_Click(object? sender, RoutedEventArgs e) => ShowEditor(null);

    private void ShowEditor(HiddenNote? note)
    {
        _editing = note;
        EditTitleBox.Text = note?.Title ?? "";
        EditContentBox.Text = note?.Content ?? "";
        ListPanel.IsVisible = false;
        EditorPanel.IsVisible = true;
    }

    private async void SaveNote_Click(object? sender, RoutedEventArgs e)
    {
        var note = new HiddenNote
        {
            Id = _editing?.Id ?? Guid.NewGuid().ToString(),
            Title = EditTitleBox.Text ?? "",
            Content = EditContentBox.Text ?? "",
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await _appState.HiddenNotesRepository.UpsertAsync(note);
        CloseEditor();
    }

    private void CancelEdit_Click(object? sender, RoutedEventArgs e) => CloseEditor();

    private void CloseEditor()
    {
        _editing = null;
        EditorPanel.IsVisible = false;
        ListPanel.IsVisible = true;
    }

    private async void DeleteNote_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: NoteListItem item } && TopLevel.GetTopLevel(this) is Window owner)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                "Delete this note?",
                $"\"{(string.IsNullOrEmpty(item.Note.Title) ? "(untitled)" : item.Note.Title)}\" will be permanently deleted.");
            if (confirmed)
            {
                await _appState.HiddenNotesRepository.DeleteAsync(item.Note.Id);
            }
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private class NoteListItem
    {
        public HiddenNote Note { get; }
        public string Title => string.IsNullOrEmpty(Note.Title) ? "(untitled)" : Note.Title;
        public string Preview => Note.Content;

        public NoteListItem(HiddenNote note)
        {
            Note = note;
        }
    }
}
