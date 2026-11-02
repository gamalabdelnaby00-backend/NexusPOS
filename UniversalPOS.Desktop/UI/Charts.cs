using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace UniversalPOS.Desktop.UI;

/// <summary>Smooth area/line chart (labels along X, amounts along Y). Drawn directly, no external chart library.</summary>
public sealed class AreaLineChart : FrameworkElement
{
    private IReadOnlyList<(string Label, double Value)> _points = Array.Empty<(string, double)>();

    public AreaLineChart()
    {
        FlowDirection = FlowDirection.LeftToRight;   // axes read left to right like the mockup
        MinHeight = 200;
    }

    public void SetPoints(IReadOnlyList<(string Label, double Value)> points)
    {
        _points = points;
        InvalidateVisual();
    }

    private FormattedText Format(string text, double size, Brush brush) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static double NiceCeiling(double max)
    {
        if (max <= 0) return 1;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(max)));
        double n = max / pow;
        double nice = n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10;
        return nice * pow;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (_points.Count == 0 || w < 120 || h < 90) return;

        double left = 58, right = 14, top = 12, bottom = 30;
        var plot = new Rect(left, top, w - left - right, h - top - bottom);
        double yMax = NiceCeiling(_points.Max(p => p.Value));
        var gridPen = new Pen(UiKit.B("#E6EDF6"), 1);
        var axisBrush = UiKit.B("#4B5F7C");
        const int steps = 4;

        for (int i = 0; i <= steps; i++)
        {
            double y = plot.Bottom - plot.Height * i / steps;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Format((yMax * i / steps).ToString("N0", CultureInfo.InvariantCulture), 11, axisBrush);
            dc.DrawText(label, new Point(plot.Left - 8 - label.Width, y - label.Height / 2));
        }

        int n = _points.Count;
        double Xat(int i) => n == 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * i / (n - 1);
        double Yat(double v) => plot.Bottom - plot.Height * Math.Min(v, yMax) / yMax;
        var pts = _points.Select((p, i) => new Point(Xat(i), Yat(p.Value))).ToList();

        for (int i = 0; i < n; i++)
        {
            dc.DrawLine(gridPen, new Point(pts[i].X, plot.Top), new Point(pts[i].X, plot.Bottom));
            var label = Format(_points[i].Label, 11, axisBrush);
            double x = Math.Min(Math.Max(pts[i].X - label.Width / 2, 0), w - label.Width);
            dc.DrawText(label, new Point(x, plot.Bottom + 8));
        }

        Point P(int i) => pts[Math.Max(0, Math.Min(n - 1, i))];
        Point C1(int i) => new(P(i).X + (P(i + 1).X - P(i - 1).X) / 6, P(i).Y + (P(i + 1).Y - P(i - 1).Y) / 6);
        Point C2(int i) => new(P(i + 1).X - (P(i + 2).X - P(i).X) / 6, P(i + 1).Y - (P(i + 2).Y - P(i).Y) / 6);

        double Clamp(double y) => Math.Min(Math.Max(y, plot.Top), plot.Bottom);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            for (int i = 0; i < n - 1; i++)
            {
                var c1 = C1(i); var c2 = C2(i);
                ctx.BezierTo(new Point(c1.X, Clamp(c1.Y)), new Point(c2.X, Clamp(c2.Y)), pts[i + 1], true, true);
            }
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, plot.Bottom), true, true);
            ctx.LineTo(pts[0], false, false);
            for (int i = 0; i < n - 1; i++)
            {
                var c1 = C1(i); var c2 = C2(i);
                ctx.BezierTo(new Point(c1.X, Clamp(c1.Y)), new Point(c2.X, Clamp(c2.Y)), pts[i + 1], false, true);
            }
            ctx.LineTo(new Point(pts[n - 1].X, plot.Bottom), false, false);
        }
        area.Freeze();

        var fill = new LinearGradientBrush(Color.FromArgb(150, 120, 190, 245), Color.FromArgb(20, 120, 190, 245), 90);
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(UiKit.B("#1478E0"), 2) { LineJoin = PenLineJoin.Round }, line);
        foreach (var p in pts)
            dc.DrawEllipse(UiKit.B("#1478E0"), new Pen(Brushes.White, 1.5), p, 4, 4);
    }
}

/// <summary>Pie chart with the percentage written inside each slice. The legend is built by the page.</summary>
public sealed class PieChart : FrameworkElement
{
    public static readonly string[] Palette = { "#1E88F2", "#F5A524", "#8E6CE0", "#3DBE8B", "#EF5B5B", "#64748B" };
    private IReadOnlyList<(string Label, double Value)> _slices = Array.Empty<(string, double)>();

    public PieChart() { MinWidth = 150; MinHeight = 150; }

    public void SetSlices(IReadOnlyList<(string Label, double Value)> slices)
    {
        _slices = slices;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double total = _slices.Sum(s => Math.Max(0, s.Value));
        double size = Math.Min(ActualWidth, ActualHeight);
        if (total <= 0 || size < 40) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = size / 2 - 2;
        var white = new Pen(Brushes.White, 2);
        double angle = -90;

        for (int i = 0; i < _slices.Count; i++)
        {
            double value = Math.Max(0, _slices[i].Value);
            if (value <= 0) continue;
            double sweep = value / total * 360;
            var brush = UiKit.B(Palette[i % Palette.Length]);

            if (sweep >= 359.9)
            {
                dc.DrawEllipse(brush, white, center, r, r);
            }
            else
            {
                Point At(double deg) => new(center.X + r * Math.Cos(deg * Math.PI / 180), center.Y + r * Math.Sin(deg * Math.PI / 180));
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(center, true, true);
                    ctx.LineTo(At(angle), true, false);
                    ctx.ArcTo(At(angle + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(brush, white, geo);
            }

            double pct = value / total * 100;
            if (pct >= 7)
            {
                double mid = (angle + sweep / 2) * Math.PI / 180;
                var text = new FormattedText($"{pct:0}%", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 12,
                    Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var pos = new Point(center.X + r * 0.62 * Math.Cos(mid) - text.Width / 2, center.Y + r * 0.62 * Math.Sin(mid) - text.Height / 2);
                dc.DrawText(text, pos);
            }
            angle += sweep;
        }
    }
}
