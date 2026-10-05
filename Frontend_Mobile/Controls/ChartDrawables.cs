using Microsoft.Maui.Graphics;

namespace Frontend_Mobile.Controls;

/// <summary>
/// ReportsPage's three charts, drawn with Microsoft.Maui.Graphics - this project's closest
/// equivalent to MudBlazor's MudChart, since no charting package is referenced. Deliberately
/// minimal (no gridlines beyond a baseline, no animation): matching MudChart's three chart
/// types (Line / StackedBar / Bar) mattered more here than matching its polish.
/// Reference: Microsoft, "Draw graphical objects - .NET MAUI." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/maui/user-interface/graphics/draw
/// Reference: MudBlazor, "Charts - MudBlazor." Accessed: Oct. 04, 2026. [Online]. Available: https://mudblazor.com/components/chart
/// </summary>
internal static class ChartMath
{
    public const float LeftPad = 30, BottomPad = 20, TopPad = 10, RightPad = 8;
    public static readonly Color Axis = Color.FromArgb("#E7E4D8");
    public static readonly Color Muted = Color.FromArgb("#75806F");

    public static RectF PlotArea(RectF bounds) => new(
        bounds.X + LeftPad, bounds.Y + TopPad,
        MathF.Max(1, bounds.Width - LeftPad - RightPad),
        MathF.Max(1, bounds.Height - TopPad - BottomPad));

    public static void DrawBaseline(ICanvas canvas, RectF plot)
    {
        canvas.StrokeColor = Axis;
        canvas.StrokeSize = 1;
        canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }

    public static void DrawCategoryLabel(ICanvas canvas, string text, float centerX, float slotWidth, float y)
    {
        canvas.FontColor = Muted;
        canvas.FontSize = 9;
        canvas.DrawString(text, centerX - slotWidth / 2, y, slotWidth, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
    }
}

/// <summary>Mirrors ChartType.Line: the trend card's single "Cumulative completed" series.</summary>
public sealed class LineChartDrawable : IDrawable
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<double> Values { get; init; } = [];
    public Color LineColor { get; init; } = Color.FromArgb("#2A5CD1");

    public void Draw(ICanvas canvas, RectF bounds)
    {
        canvas.SaveState();
        var plot = ChartMath.PlotArea(bounds);
        var max = Values.Count == 0 ? 1 : Math.Max(1, Values.Max());

        ChartMath.DrawBaseline(canvas, plot);
        canvas.FontColor = ChartMath.Muted;
        canvas.FontSize = 9;
        canvas.DrawString("0", bounds.X, plot.Bottom - 6, ChartMath.LeftPad - 4, 12, HorizontalAlignment.Right, VerticalAlignment.Center);
        canvas.DrawString(((int)max).ToString(), bounds.X, plot.Top - 2, ChartMath.LeftPad - 4, 12, HorizontalAlignment.Right, VerticalAlignment.Center);

        if (Values.Count == 0) { canvas.RestoreState(); return; }

        float StepX(int i) => Values.Count <= 1 ? plot.Left : plot.Left + plot.Width * i / (Values.Count - 1);
        float Y(double v) => (float)(plot.Bottom - plot.Height * (v / max));

        var path = new PathF();
        for (var i = 0; i < Values.Count; i++)
        {
            var x = StepX(i);
            var y = Y(Values[i]);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = 2;
        canvas.DrawPath(path);

        canvas.FillColor = LineColor;
        for (var i = 0; i < Values.Count; i++)
            canvas.FillCircle(StepX(i), Y(Values[i]), 2.5f);

        // Every bucket when there are few; otherwise thinned out, so labels don't overlap -
        // same idea as the web client's "every 7th" rule, adapted to a narrower chart.
        var step = Labels.Count <= 6 ? 1 : (int)Math.Ceiling(Labels.Count / 5.0);
        for (var i = 0; i < Labels.Count; i += step)
            ChartMath.DrawCategoryLabel(canvas, Labels[i], StepX(i), 44, plot.Bottom + 4);

        canvas.RestoreState();
    }
}

/// <summary>Mirrors ChartType.StackedBar: the completion card's Completed+Outstanding bars.</summary>
public sealed class StackedBarChartDrawable : IDrawable
{
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<(string Name, Color Color, IReadOnlyList<double> Values)> Series { get; init; } = [];

    public void Draw(ICanvas canvas, RectF bounds)
    {
        canvas.SaveState();
        var plot = ChartMath.PlotArea(bounds);
        var totals = Categories.Select((_, i) => Series.Sum(s => s.Values.ElementAtOrDefault(i))).ToList();
        var max = totals.Count == 0 ? 1 : Math.Max(1, totals.Max());

        ChartMath.DrawBaseline(canvas, plot);
        if (Categories.Count == 0) { canvas.RestoreState(); return; }

        var slot = plot.Width / Categories.Count;
        var barWidth = MathF.Min(40, slot * 0.55f);

        for (var i = 0; i < Categories.Count; i++)
        {
            var cx = plot.Left + slot * i + slot / 2;
            var yCursor = plot.Bottom;
            foreach (var s in Series)
            {
                var v = s.Values.ElementAtOrDefault(i);
                if (v <= 0) continue;
                var h = (float)(plot.Height * (v / max));
                canvas.FillColor = s.Color;
                canvas.FillRoundedRectangle(cx - barWidth / 2, yCursor - h, barWidth, h, 2);
                yCursor -= h;
            }
            ChartMath.DrawCategoryLabel(canvas, Categories[i], cx, slot, plot.Bottom + 4);
        }
        canvas.RestoreState();
    }
}

/// <summary>Mirrors ChartType.Bar (grouped): the workload card's Assigned/Completed/Overdue bars.</summary>
public sealed class GroupedBarChartDrawable : IDrawable
{
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<(string Name, Color Color, IReadOnlyList<double> Values)> Series { get; init; } = [];

    public void Draw(ICanvas canvas, RectF bounds)
    {
        canvas.SaveState();
        var plot = ChartMath.PlotArea(bounds);
        var max = Math.Max(1, Series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max());

        ChartMath.DrawBaseline(canvas, plot);
        if (Categories.Count == 0 || Series.Count == 0) { canvas.RestoreState(); return; }

        var slot = plot.Width / Categories.Count;
        var groupWidth = MathF.Min(48, slot * 0.7f);
        var barWidth = groupWidth / Series.Count;

        for (var i = 0; i < Categories.Count; i++)
        {
            var groupLeft = plot.Left + slot * i + (slot - groupWidth) / 2;
            for (var s = 0; s < Series.Count; s++)
            {
                var v = Series[s].Values.ElementAtOrDefault(i);
                var h = (float)(plot.Height * (v / max));
                canvas.FillColor = Series[s].Color;
                canvas.FillRoundedRectangle(groupLeft + barWidth * s, plot.Bottom - h, MathF.Max(1, barWidth - 2), h, 2);
            }
            ChartMath.DrawCategoryLabel(canvas, Categories[i], plot.Left + slot * i + slot / 2, slot, plot.Bottom + 4);
        }
        canvas.RestoreState();
    }
}
