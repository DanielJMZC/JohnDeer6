using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BarChart : DashboardChartBase
{
    public struct Bar { public string Label; public float Value; public Color Color; }
    public bool Horizontal { get; set; }
    private readonly List<Bar> bars = new();
    private readonly VisualElement labels = new VisualElement();
    protected override bool HasData => bars.Count > 0;
    protected override float PlotLeft => Horizontal ? 118 : 48;

    public BarChart()
    {
        ShowYLabels = false;
        ShowYGrid = false;
        labels.style.position = Position.Absolute;
        labels.StretchToParentSize();
        labels.pickingMode = PickingMode.Ignore;
        Canvas.Add(labels);
        Canvas.RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());
    }

    public void SetData(IReadOnlyList<Bar> values)
    {
        bars.Clear();
        for (int i = 0; i < values.Count; i++) bars.Add(values[i]);
        Refresh();
        RebuildLabels();
    }

    protected override void GetBounds(out float xMin, out float xMax, out float yMin, out float yMax)
    {
        float max = 1;
        foreach (Bar bar in bars) max = Mathf.Max(max, bar.Value);
        xMin = 0; yMin = 0;
        if (Horizontal) { xMax = max; yMax = Mathf.Max(1, bars.Count); }
        else { xMax = Mathf.Max(1, bars.Count); yMax = max; }
    }

    protected override void DrawData(Painter2D painter, float xMin, float xMax, float yMin, float yMax)
    {
        float slot = (Horizontal ? Plot.height : Plot.width) / Mathf.Max(1, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Bar bar = bars[i];
            if (Horizontal)
            {
                painter.fillColor = new Color(.88f, .9f, .87f);
                FillRect(painter, new Rect(Plot.x, Plot.y + i * slot + slot * .22f, Plot.width, slot * .56f));
            }
            painter.fillColor = bar.Color;
            Rect rect = Horizontal
                ? new Rect(Plot.x, Plot.y + i * slot + slot * .22f, Mathf.Max(2, (bar.Value - xMin) / (xMax - xMin) * Plot.width), slot * .56f)
                : new Rect(Plot.x + i * slot + slot * .15f, Map(0, bar.Value, xMin, xMax, yMin, yMax).y, slot * .7f, (bar.Value - yMin) / (yMax - yMin) * Plot.height);
            FillRect(painter, rect);
        }
    }

    private void RebuildLabels()
    {
        labels.Clear();
        if (!Horizontal || bars.Count == 0 || Plot.height <= 0) return;
        float slot = Plot.height / bars.Count;
        for (int i = 0; i < bars.Count; i++)
        {
            var name = new Label(bars[i].Label);
            name.style.position = Position.Absolute;
            name.style.left = 2;
            name.style.top = Plot.y + i * slot;
            name.style.width = PlotLeft - 8;
            name.style.height = slot;
            name.style.fontSize = 10;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            labels.Add(name);
            var value = new Label($"{bars[i].Value:0.#}%");
            value.style.position = Position.Absolute;
            value.style.right = 4;
            value.style.top = Plot.y + i * slot;
            value.style.width = 48;
            value.style.height = slot;
            value.style.fontSize = 10;
            value.style.color = new Color(.12f, .2f, .13f);
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            labels.Add(value);
        }
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
