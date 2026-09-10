using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MultiLineChart : DashboardChartBase
{
    public sealed class Series
    {
        public string Name;
        public Color Color;
        public readonly List<Vector2> Points = new();
    }

    private readonly List<Series> series = new();
    private readonly VisualElement legend = new VisualElement();
    protected override bool HasData => series.Exists(item => item.Points.Count > 0);

    public MultiLineChart()
    {
        legend.style.flexDirection = FlexDirection.Row;
        legend.style.flexWrap = Wrap.Wrap;
        Add(legend);
    }

    public Series AddSeries(string name, Color color)
    {
        var item = new Series { Name = name, Color = color };
        series.Add(item);
        AddLegendItem(name, color);
        Refresh();
        return item;
    }

    public void SetData(Series target, IReadOnlyList<Vector2> points)
    {
        target.Points.Clear();
        for (int i = 0; i < points.Count; i++) target.Points.Add(points[i]);
        Refresh();
    }

    public void ClearSeries() { series.Clear(); legend.Clear(); Refresh(); }

    private void AddLegendItem(string name, Color color)
    {
        var label = new Label($"●  {name}");
        label.style.color = color;
        label.style.fontSize = 11;
        label.style.marginRight = 12;
        legend.Add(label);
    }

    protected override void GetBounds(out float xMin, out float xMax, out float yMin, out float yMax)
    {
        xMin = yMin = float.PositiveInfinity;
        xMax = yMax = float.NegativeInfinity;
        foreach (Series item in series)
        foreach (Vector2 point in item.Points)
        {
            xMin = Mathf.Min(xMin, point.x); xMax = Mathf.Max(xMax, point.x);
            yMin = Mathf.Min(yMin, point.y); yMax = Mathf.Max(yMax, point.y);
        }
    }

    protected override void DrawData(Painter2D painter, float xMin, float xMax, float yMin, float yMax)
    {
        painter.lineWidth = 2;
        foreach (Series item in series)
        {
            painter.strokeColor = item.Color;
            for (int i = 1; i < item.Points.Count; i++)
                Segment(painter, Map(item.Points[i - 1].x, item.Points[i - 1].y, xMin, xMax, yMin, yMax), Map(item.Points[i].x, item.Points[i].y, xMin, xMax, yMin, yMax));
        }
    }
}
