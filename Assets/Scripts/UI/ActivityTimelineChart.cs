using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ActivityTimelineChart : DashboardChartBase
{
    public struct Interval
    {
        public string Row;
        public string Activity;
        public float Start;
        public float End;
        public Color Color;
    }

    private readonly List<Interval> intervals = new();
    private readonly List<string> rows = new();
    private readonly VisualElement labels = new VisualElement();
    private readonly VisualElement legend = new VisualElement();
    protected override bool HasData => intervals.Count > 0;
    protected override float PlotLeft => 112;

    public ActivityTimelineChart()
    {
        ShowYLabels = false;
        ShowYGrid = false;
        YAxis.Title = "Vehicles";
        labels.style.position = Position.Absolute;
        labels.StretchToParentSize();
        labels.pickingMode = PickingMode.Ignore;
        Canvas.Add(labels);
        Canvas.RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());

        legend.style.flexDirection = FlexDirection.Row;
        legend.style.flexWrap = Wrap.Wrap;
        AddLegend("Harvesting", new Color(.2f, .62f, .3f));
        AddLegend("Moving", new Color(.18f, .48f, .78f));
        AddLegend("Unloading", new Color(.92f, .58f, .12f));
        AddLegend("Idle", new Color(.55f, .57f, .55f));
        Add(legend);
    }

    public void SetData(IReadOnlyList<Interval> values)
    {
        intervals.Clear();
        rows.Clear();
        for (int i = 0; i < values.Count; i++)
        {
            Interval item = values[i];
            if (item.End <= item.Start || string.IsNullOrEmpty(item.Row)) continue;
            intervals.Add(item);
            if (!rows.Contains(item.Row)) rows.Add(item.Row);
        }
        rows.Sort(StringComparer.Ordinal);
        YAxis.TickCount = Mathf.Max(2, rows.Count + 1);
        Refresh();
        RebuildLabels();
    }

    protected override void GetBounds(out float xMin, out float xMax, out float yMin, out float yMax)
    {
        xMin = float.PositiveInfinity;
        xMax = float.NegativeInfinity;
        foreach (Interval item in intervals)
        {
            xMin = Mathf.Min(xMin, item.Start);
            xMax = Mathf.Max(xMax, item.End);
        }
        yMin = 0;
        yMax = Mathf.Max(1, rows.Count);
    }

    protected override void DrawData(Painter2D painter, float xMin, float xMax, float yMin, float yMax)
    {
        float rowHeight = Plot.height / Mathf.Max(1, rows.Count);
        for (int row = 0; row < rows.Count; row++)
        {
            if ((row & 1) == 0)
            {
                painter.fillColor = new Color(.93f, .95f, .92f, .8f);
                FillRect(painter, new Rect(Plot.x, Plot.y + row * rowHeight, Plot.width, rowHeight));
            }
            painter.strokeColor = new Color(.82f, .85f, .81f);
            Segment(painter, new Vector2(Plot.x, Plot.y + (row + 1) * rowHeight), new Vector2(Plot.xMax, Plot.y + (row + 1) * rowHeight));
        }
        foreach (Interval item in intervals)
        {
            int row = rows.IndexOf(item.Row);
            float left = Map(item.Start, 0, xMin, xMax, yMin, yMax).x;
            float right = Map(item.End, 0, xMin, xMax, yMin, yMax).x;
            painter.fillColor = item.Color;
            FillRect(painter, new Rect(left, Plot.y + row * rowHeight + rowHeight * .2f, Mathf.Max(2, right - left), rowHeight * .6f));
        }
    }

    private void RebuildLabels()
    {
        labels.Clear();
        if (Plot.width <= 0 || Plot.height <= 0 || rows.Count == 0) return;
        float rowHeight = Plot.height / rows.Count;
        GetBounds(out float xMin, out float xMax, out _, out _);
        if (!float.IsFinite(xMin) || xMax <= xMin) { xMin = 0; xMax = 1; }
        for (int row = 0; row < rows.Count; row++)
        {
            var rowLabel = new Label(rows[row]);
            rowLabel.style.position = Position.Absolute;
            rowLabel.style.left = 2;
            rowLabel.style.top = Plot.y + row * rowHeight;
            rowLabel.style.width = PlotLeft - 8;
            rowLabel.style.height = rowHeight;
            rowLabel.style.fontSize = 11;
            rowLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rowLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            labels.Add(rowLabel);
        }
        foreach (Interval item in intervals)
        {
            float left = Plot.x + (item.Start - xMin) / (xMax - xMin) * Plot.width;
            float width = (item.End - item.Start) / (xMax - xMin) * Plot.width;
            if (width < 58) continue;
            int row = rows.IndexOf(item.Row);
            var activity = new Label(item.Activity);
            activity.style.position = Position.Absolute;
            activity.style.left = left + 4;
            activity.style.top = Plot.y + row * rowHeight + rowHeight * .2f;
            activity.style.width = width - 8;
            activity.style.height = rowHeight * .6f;
            activity.style.color = Color.white;
            activity.style.fontSize = 10;
            activity.style.unityTextAlign = TextAnchor.MiddleCenter;
            labels.Add(activity);
        }
    }

    private void AddLegend(string text, Color color)
    {
        var item = new Label($"■  {text}");
        item.style.color = color;
        item.style.fontSize = 11;
        item.style.marginRight = 12;
        legend.Add(item);
    }

    private static void FillRect(Painter2D painter, Rect rect)
    {
        painter.BeginPath();
        painter.MoveTo(rect.min);
        painter.LineTo(new Vector2(rect.xMax, rect.yMin));
        painter.LineTo(rect.max);
        painter.LineTo(new Vector2(rect.xMin, rect.yMax));
        painter.ClosePath();
        painter.Fill();
    }
}
