using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System.Collections.Generic;

namespace YSMViewer.Views;

public sealed class RadialMenu : Control
{
    private List<RadialMenuItem>? _items;
    private int _hoveredIndex = -1;
    private bool _isOpen;

    public static readonly DirectProperty<RadialMenu, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<RadialMenu, bool>(nameof(IsOpen), o => o._isOpen, (o, v) => o._isOpen = v);

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(Radius), 80);

    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<RadialMenu, double>(nameof(InnerRadius), 24);

    public bool IsOpen
    {
        get => _isOpen;
        set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public double InnerRadius
    {
        get => GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    public event EventHandler<int>? ItemClicked;

    public void SetItems(List<RadialMenuItem> items)
    {
        _items = items;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_items is null || _items.Count == 0) return;

        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var radius = Radius;
        var inner = InnerRadius;
        var sliceAngle = 2.0 * Math.PI / _items.Count;
        var startAngle = -Math.PI / 2;

        for (int i = 0; i < _items.Count; i++)
        {
            var a1 = startAngle + i * sliceAngle;
            var a2 = a1 + sliceAngle;
            var isHovered = i == _hoveredIndex;

            var bgBrush = isHovered
                ? new SolidColorBrush(Color.FromArgb(220, 74, 144, 217))
                : new SolidColorBrush(Color.FromArgb(180, 15, 52, 96));

            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 74, 144, 217)), 1.5);

            DrawSlice(context, cx, cy, inner, radius, a1, a2, bgBrush, borderPen);

            var midAngle = (a1 + a2) / 2;
            var labelRadius = (inner + radius) / 2;
            var lx = cx + labelRadius * Math.Cos(midAngle);
            var ly = cy + labelRadius * Math.Sin(midAngle);

            var fontSize = isHovered ? 13.0 : 11.0;
            var textBrush = new SolidColorBrush(Colors.White);
            var ft = new FormattedText(
                _items[i].Label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI, Arial"),
                fontSize,
                textBrush);

            var approxW = fontSize * _items[i].Label.Length * 0.55;
            var approxH = fontSize * 1.2;
            context.DrawText(ft, new Point(lx - approxW / 2, ly - approxH / 2));
        }

        var centerBrush = new SolidColorBrush(Color.FromArgb(200, 233, 69, 96));
        context.DrawEllipse(centerBrush, null, new Point(cx, cy), inner, inner);

        var closeFt = new FormattedText(
            IsOpen ? "\u2715" : "\u2630",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI, Arial"),
            16,
            new SolidColorBrush(Colors.White));

        context.DrawText(closeFt, new Point(cx - 6, cy - 8));
    }

    private void DrawSlice(DrawingContext context, double cx, double cy, double inner, double outer, double a1, double a2, IBrush fill, IPen stroke)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var segments = Math.Max(8, (int)((a2 - a1) / (Math.PI / 16)));

            ctx.BeginFigure(new Point(cx + inner * Math.Cos(a1), cy + inner * Math.Sin(a1)), true);
            ctx.LineTo(new Point(cx + outer * Math.Cos(a1), cy + outer * Math.Sin(a1)));
            for (int i = 1; i <= segments; i++)
            {
                var a = a1 + (a2 - a1) * i / segments;
                ctx.LineTo(new Point(cx + outer * Math.Cos(a), cy + outer * Math.Sin(a)));
            }
            ctx.LineTo(new Point(cx + inner * Math.Cos(a2), cy + inner * Math.Sin(a2)));
            for (int i = segments; i >= 0; i--)
            {
                var a = a1 + (a2 - a1) * i / segments;
                ctx.LineTo(new Point(cx + inner * Math.Cos(a), cy + inner * Math.Sin(a)));
            }
            ctx.EndFigure(true);
        }
        context.DrawGeometry(fill, stroke, geo);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var dx = pos.X - cx;
        var dy = pos.Y - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist <= InnerRadius)
        {
            IsOpen = !IsOpen;
            _hoveredIndex = -1;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!IsOpen || _items is null || _items.Count == 0) return;

        var angle = Math.Atan2(dy, dx);
        if (angle < -Math.PI / 2) angle += 2 * Math.PI;
        var startAngle = -Math.PI / 2;
        var adjustedAngle = angle - startAngle;
        if (adjustedAngle < 0) adjustedAngle += 2 * Math.PI;
        var sliceAngle = 2.0 * Math.PI / _items.Count;
        var idx = (int)(adjustedAngle / sliceAngle);

        if (idx >= 0 && idx < _items.Count)
        {
            _items[idx].ToggleAction?.Invoke();
            ItemClicked?.Invoke(this, idx);
            IsOpen = false;
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsOpen || _items is null || _items.Count == 0) return;

        var pos = e.GetPosition(this);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var dx = pos.X - cx;
        var dy = pos.Y - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist <= InnerRadius || dist > Radius)
        {
            if (_hoveredIndex != -1) { _hoveredIndex = -1; InvalidateVisual(); }
            return;
        }

        var angle = Math.Atan2(dy, dx);
        if (angle < -Math.PI / 2) angle += 2 * Math.PI;
        var startAngle = -Math.PI / 2;
        var adjustedAngle = angle - startAngle;
        if (adjustedAngle < 0) adjustedAngle += 2 * Math.PI;
        var sliceAngle = 2.0 * Math.PI / _items.Count;
        var idx = (int)(adjustedAngle / sliceAngle);

        if (idx != _hoveredIndex)
        {
            _hoveredIndex = idx;
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sz = Radius * 2 + 20;
        return new Size(sz, sz);
    }
}

public sealed class RadialMenuItem
{
    public string Label { get; init; } = "";
    public Action? ToggleAction { get; init; }
    public bool IsOn { get; init; }
}