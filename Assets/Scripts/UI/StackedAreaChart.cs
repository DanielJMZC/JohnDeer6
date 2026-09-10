using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class StackedAreaChart : DashboardChartBase
{
    public sealed class Series
    {
        public string Name;
        public Color Color;
        public readonly List<Vector2> Points = new();
    }
    private readonly List<Series> series = new();
    private readonly VisualElement legend = new VisualElement();
    protected override bool HasData => series.Count > 0 && series[0].Points.Count > 0;

    public StackedAreaChart()
    {
        legend.style.flexDirection = FlexDirection.Row;
        legend.style.flexWrap = Wrap.Wrap;
        Add(legend);
    }

    public Series AddSeries(string name, Color color)
    {
        var result = new Series { Name = name, Color = color };
        series.Add(result);
        var label = new Label($"■  {name}");
        label.style.color = color;
        label.style.fontSize = 11;
        label.style.marginRight = 12;
        legend.Add(label);
        return result;
    }

    public void SetData(Series target, IReadOnlyList<Vector2> points)
    {
        target.Points.Clear();
        for (int i = 0; i < points.Count; i++) target.Points.Add(points[i]);
        Refresh();
    }

    protected override void GetBounds(out float xMin, out float xMax, out float yMin, out float yMax)
    {
        xMin = float.PositiveInfinity; xMax = float.NegativeInfinity; yMin = 0; yMax = 1;
        int count = 0;
        foreach (Series item in series) count = Mathf.Max(count, item.Points.Count);
        for (int i = 0; i < count; i++)
        {
            float total = 0;
            foreach (Series item in series)
                if (i < item.Points.Count) { xMin = Mathf.Min(xMin, item.Points[i].x); xMax = Mathf.Max(xMax, item.Points[i].x); total += Mathf.Max(0, item.Points[i].y); }
            yMax = Mathf.Max(yMax, total);
        }
    }

    protected override void DrawData(Painter2D painter, float xMin, float xMax, float yMin, float yMax)
    {
        int count = series[0].Points.Count;
        var lower = new float[count];
        foreach (Series item in series)
        {
            int usable = Mathf.Min(count, item.Points.Count);
            if (usable < 2) continue;
            painter.fillColor = new Color(item.Color.r, item.Color.g, item.Color.b, .72f);
            painter.BeginPath();
            painter.MoveTo(Map(item.Points[0].x, lower[0], xMin, xMax, yMin, yMax));
            for (int i = 0; i < usable; i++)
                painter.LineTo(Map(item.Points[i].x, lower[i] + Mathf.Max(0, item.Points[i].y), xMin, xMax, yMin, yMax));
            for (int i = usable - 1; i >= 0; i--)
                painter.LineTo(Map(item.Points[i].x, lower[i], xMin, xMax, yMin, yMax));
            painter.ClosePath();
            painter.Fill();
            for (int i = 0; i < usable; i++) lower[i] += Mathf.Max(0, item.Points[i].y);
        }
    }
}
