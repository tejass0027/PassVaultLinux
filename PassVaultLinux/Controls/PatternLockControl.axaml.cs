using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace PassVaultLinux.Controls;

/// <summary>
/// Classic 3x3 Android-style pattern lock, ported to Avalonia pointer events. Reports the dot
/// sequence (indices 0-8, left-to-right top-to-bottom) via <see cref="PatternCompleted"/> once
/// the pointer is released. Set <see cref="ShowError"/> briefly (e.g. ~400ms) to flash the
/// last-drawn pattern red after a wrong attempt.
/// </summary>
public partial class PatternLockControl : UserControl
{
    public event Action<List<int>>? PatternCompleted;

    private readonly List<int> _selected = new();
    private bool _isDragging;
    private Point _currentPoint;
    private bool _showError;

    public static readonly StyledProperty<bool> ShowErrorProperty =
        AvaloniaProperty.Register<PatternLockControl, bool>(nameof(ShowError));

    public bool ShowError
    {
        get => GetValue(ShowErrorProperty);
        set => SetValue(ShowErrorProperty, value);
    }

    public PatternLockControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();

        DrawCanvas.PointerPressed += Canvas_PointerPressed;
        DrawCanvas.PointerMoved += Canvas_PointerMoved;
        DrawCanvas.PointerReleased += Canvas_PointerReleased;

        PropertyChanged += (_, e) =>
        {
            if (e.Property == ShowErrorProperty)
            {
                _showError = ShowError;
                Redraw();
            }
        };
    }

    private List<Point> DotCenters()
    {
        double side = Math.Min(Bounds.Width, Bounds.Height);
        double margin = side * 0.18;
        double step = (side - 2 * margin) / 2.0;
        double offsetX = (Bounds.Width - side) / 2.0;
        double offsetY = (Bounds.Height - side) / 2.0;
        var centers = new List<Point>();
        for (int i = 0; i < 9; i++)
        {
            int row = i / 3;
            int col = i % 3;
            centers.Add(new Point(offsetX + margin + col * step, offsetY + margin + row * step));
        }
        return centers;
    }

    private int? NearestDotIndex(Point position)
    {
        var centers = DotCenters();
        double side = Math.Min(Bounds.Width, Bounds.Height);
        double touchRadius = side * 0.16;
        int? closestIndex = null;
        double closestDistance = double.MaxValue;
        for (int i = 0; i < centers.Count; i++)
        {
            double dx = position.X - centers[i].X;
            double dy = position.Y - centers[i].Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < touchRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(DrawCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _selected.Clear();
        var pos = e.GetPosition(DrawCanvas);
        var idx = NearestDotIndex(pos);
        if (idx != null)
        {
            _selected.Add(idx.Value);
        }
        _currentPoint = pos;
        _isDragging = true;
        e.Pointer.Capture(DrawCanvas);
        Redraw();
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }
        var pos = e.GetPosition(DrawCanvas);
        _currentPoint = pos;
        var idx = NearestDotIndex(pos);
        if (idx != null && !_selected.Contains(idx.Value))
        {
            _selected.Add(idx.Value);
        }
        Redraw();
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }
        _isDragging = false;
        e.Pointer.Capture(null);
        var finished = new List<int>(_selected);
        _selected.Clear();
        Redraw();
        if (finished.Count > 0)
        {
            PatternCompleted?.Invoke(finished);
        }
    }

    private void Redraw()
    {
        DrawCanvas.Children.Clear();
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var centers = DotCenters();
        var accentBrush = (IBrush?)TryFindResource("PrimaryBrush") ?? Brushes.MediumPurple;
        var dotBrush = (IBrush?)TryFindResource("OutlineBrush") ?? Brushes.Gray;
        var errorBrush = Brushes.Crimson;
        var activeBrush = _showError ? errorBrush : accentBrush;

        double side = Math.Min(Bounds.Width, Bounds.Height);
        double dotRadius = side * 0.035;
        double ringRadius = dotRadius * 2.2;

        for (int i = 0; i < _selected.Count - 1; i++)
        {
            DrawLine(centers[_selected[i]], centers[_selected[i + 1]], activeBrush);
        }
        if (_isDragging && _selected.Count > 0)
        {
            DrawLine(centers[_selected[^1]], _currentPoint, activeBrush);
        }

        for (int i = 0; i < centers.Count; i++)
        {
            bool isSelected = _selected.Contains(i);
            if (isSelected)
            {
                DrawCircle(centers[i], ringRadius, WithAlpha(activeBrush, 0.18));
            }
            DrawCircle(centers[i], dotRadius, isSelected ? activeBrush : dotBrush);
        }
    }

    private void DrawLine(Point start, Point end, IBrush brush)
    {
        var line = new Line
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = brush,
            StrokeThickness = 6,
            StrokeLineCap = PenLineCap.Round
        };
        DrawCanvas.Children.Add(line);
    }

    private void DrawCircle(Point center, double radius, IBrush brush)
    {
        var ellipse = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = brush };
        Canvas.SetLeft(ellipse, center.X - radius);
        Canvas.SetTop(ellipse, center.Y - radius);
        DrawCanvas.Children.Add(ellipse);
    }

    private static IBrush WithAlpha(IBrush brush, double alpha)
    {
        if (brush is SolidColorBrush solid)
        {
            var c = solid.Color;
            return new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B));
        }
        return brush;
    }

    private object? TryFindResource(string key) =>
        this.TryFindResource(key, out var value) ? value : null;
}
