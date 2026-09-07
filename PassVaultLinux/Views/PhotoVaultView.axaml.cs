using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PassVaultLinux.Data;

namespace PassVaultLinux.Views;

public partial class PhotoVaultView : UserControl
{
    private readonly AppState _appState;
    private readonly Action _onBack;

    public PhotoVaultView(AppState appState, Action onBack)
    {
        InitializeComponent();
        _appState = appState;
        _onBack = onBack;

        _appState.PhotoVaultRepository.PhotosChanged += () => _ = RefreshAsync();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var photos = _appState.PhotoVaultRepository.Photos.ToList();
        PhotosPanel.Children.Clear();
        PhotosPanel.Children.Add(BuildAddTile());

        foreach (var photo in photos)
        {
            var bytes = await _appState.PhotoVaultRepository.LoadPhotoBytesAsync(photo.Id);
            if (bytes == null)
            {
                continue;
            }
            PhotosPanel.Children.Add(BuildPhotoTile(photo, DecodeImage(bytes)));
        }
    }

    private static Bitmap DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private Border BuildAddTile()
    {
        var border = new Border
        {
            Width = 110,
            Height = 110,
            Margin = new Thickness(4),
            Background = (IBrush)this.FindResource("SurfaceVariantBrush")!,
            BorderBrush = (IBrush)this.FindResource("OutlineBrush")!,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        border.Child = new TextBlock
        {
            Text = "+",
            FontSize = 36,
            Foreground = (IBrush)this.FindResource("TextSecondaryBrush")!,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.PointerReleased += (sender, e) => AddPhoto_Click(sender, e);
        return border;
    }

    private Border BuildPhotoTile(VaultPhoto photo, Bitmap thumbnail)
    {
        var border = new Border
        {
            Width = 110,
            Height = 110,
            Margin = new Thickness(4),
            Background = (IBrush)this.FindResource("SurfaceVariantBrush")!,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = new PhotoItem(photo, thumbnail)
        };
        border.Child = new Image { Source = thumbnail, Stretch = Stretch.UniformToFill };
        border.PointerReleased += Thumbnail_PointerReleased;
        return border;
    }

    private async void AddPhoto_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a photo",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0)
        {
            return;
        }

        LoadingBar.IsVisible = true;
        try
        {
            byte[] bytes;
            await using (var stream = await files[0].OpenReadAsync())
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                bytes = memoryStream.ToArray();
            }
            await _appState.PhotoVaultRepository.AddPhotoAsync("", bytes);
        }
        catch (Exception ex)
        {
            var owner = topLevel as Window;
            if (owner != null)
            {
                await Controls.ConfirmDialog.ShowAsync(owner, "Error", $"Couldn't add that photo: {ex.Message}", destructive: false);
            }
        }
        finally
        {
            LoadingBar.IsVisible = false;
        }
    }

    private void Thumbnail_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: PhotoItem item })
        {
            ShowViewer(item);
        }
    }

    private void ShowViewer(PhotoItem item)
    {
        var window = new Window
        {
            Title = "Photo",
            Width = 500,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new Image { Source = item.Thumbnail, Stretch = Stretch.Uniform, MaxHeight = 500 });

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var closeButton = new Button { Content = "Close", Classes = { "secondary" }, Margin = new Thickness(0, 0, 8, 0) };
        closeButton.Click += (_, _) => window.Close();
        var deleteButton = new Button { Content = "Delete", Classes = { "secondary" }, Foreground = (IBrush)this.FindResource("ErrorBrush")! };
        deleteButton.Click += async (_, _) =>
        {
            window.Close();
            await _appState.PhotoVaultRepository.DeletePhotoAsync(item.Photo.Id);
        };
        buttonRow.Children.Add(closeButton);
        buttonRow.Children.Add(deleteButton);
        panel.Children.Add(buttonRow);

        window.Content = panel;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null)
        {
            window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private class PhotoItem
    {
        public VaultPhoto Photo { get; }
        public Bitmap Thumbnail { get; }

        public PhotoItem(VaultPhoto photo, Bitmap thumbnail)
        {
            Photo = photo;
            Thumbnail = thumbnail;
        }
    }
}
