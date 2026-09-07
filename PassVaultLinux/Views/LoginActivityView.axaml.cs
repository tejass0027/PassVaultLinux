using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PassVaultLinux.Auth;

namespace PassVaultLinux.Views;

public partial class LoginActivityView : UserControl
{
    private readonly Action _onBack;

    public LoginActivityView(List<LoginEvent> events, Action onBack)
    {
        InitializeComponent();
        _onBack = onBack;

        if (events.Count == 0)
        {
            EmptyText.IsVisible = true;
        }
        else
        {
            EventsList.ItemsSource = events.Select(evt => new EventItem(evt)).ToList();
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => _onBack();

    private class EventItem
    {
        public string Label { get; }
        public string TimeText { get; }
        public IBrush IndicatorBrush { get; }
        public IBrush TextBrush { get; }

        public EventItem(LoginEvent evt)
        {
            string kind = evt.Type switch
            {
                LoginEventType.Pattern => "Pattern",
                LoginEventType.Recovery => "Security question recovery",
                _ => "Unknown"
            };
            Label = evt.Success ? $"{kind} unlock" : $"Incorrect {kind} attempt";
            TimeText = DateTimeOffset.FromUnixTimeMilliseconds(evt.Timestamp).ToLocalTime().ToString("MMM d, yyyy · h:mm tt");

            IndicatorBrush = evt.Success
                ? (IBrush)Application.Current!.FindResource("SuccessBrush")!
                : (IBrush)Application.Current!.FindResource("ErrorBrush")!;
            TextBrush = evt.Success
                ? (IBrush)Application.Current!.FindResource("TextPrimaryBrush")!
                : (IBrush)Application.Current!.FindResource("ErrorBrush")!;
        }
    }
}
