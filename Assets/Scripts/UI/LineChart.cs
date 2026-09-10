using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>A reusable single-series chart. All titles and tick labels belong to this element.</summary>
public class LineChart : VisualElement
{
    public sealed class Axis
    {
        public string Title = "";
        public float? Min;
        public float? Max;
        public int TickCount = 4;
        public Func<float, string> Format = value => value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public Axis XAxis { get; } = new Axis();
    public Axis YAxis { get; } = new Axis();
    public Color LineColor { get; set; } = new Color(0.18f, 0.55f, 0.34f);
    public int MaxPoints { get; set; } = 2000;
    public string Title { get => title.text; set => title.text = value; }
    private readonly List<Vector2> points = new List<Vector2>();
    private readonly Label title = new Label();
    private readonly Label xTitle = new Label();
    private readonly Label yTitle = new Label();
    private readonly Label empty = new Label("Waiting for data");
    private readonly VisualElement canvas = new VisualElement();
    private readonly VisualElement ticks = new VisualElement();
    private float xMin, xMax, yMin, yMax;
    private Rect plot;

    public LineChart()
    {
        style.flexGrow = 1;
        style.minHeight = 140;
        style.minWidth = 0;
        style.marginBottom = 8;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 6;
        style.backgroundColor = new Color(0.975f, 0.98f, 0.97f);
        style.borderBottomWidth = 1;
        style.borderBottomColor = new Color(0.82f, 0.85f, 0.81f);
        title.style.fontSize = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.12f, 0.22f, 0.14f);
        title.style.paddingBottom = 5;
        title.style.borderBottomWidth = 1;
        title.style.borderBottomColor = new Color(0.88f, 0.90f, 0.87f);
        Add(title);
        yTitle.style.fontSize = 11;
        Add(yTitle);
        canvas.style.flexGrow = 1;
        canvas.style.minHeight = 80;
        canvas.style.overflow = Overflow.Hidden;
        Add(canvas);
        ticks.style.position = Position.Absolute;
        ticks.StretchToParentSize();
        ticks.pickingMode = PickingMode.Ignore;
        canvas.Add(ticks);
        empty.style.position = Position.Absolute;
        empty.style.left = 60;
        empty.style.top = 12;
        empty.style.fontSize = 11;
        canvas.Add(empty);
        xTitle.style.fontSize = 11;
        xTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        Add(xTitle);
        canvas.generateVisualContent += Draw;
        canvas.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
    }

    public void AddPoint(float x, float y)
    {
        if (!Finite(x) || !Finite(y)) return;
        if (points.Count > 0 && x < points[points.Count - 1].x)
            throw new ArgumentException("Chart X values must be nondecreasing.");
        if (points.Count > 0 && x == points[points.Count - 1].x)
            points[points.Count - 1] = new Vector2(x, y);
        else
            points.Add(new Vector2(x, y));
        int excess = points.Count - Math.Max(2, MaxPoints);
        if (excess > 0) points.RemoveRange(0, excess);
        Refresh();
    }

    public void ClearData()
    {
        points.Clear();
        Refresh();
    }

    public void SetData(IReadOnlyList<Vector2> values)
    {
        points.Clear();
        int start = Mathf.Max(0, values.Count - Mathf.Max(2, MaxPoints));
        for (int i = start; i < values.Count; i++)
        {
            Vector2 value = values[i];
            if (!Finite(value.x) || !Finite(value.y)) continue;
            if (points.Count > 0 && value.x < points[points.Count - 1].x)
                throw new ArgumentException("Chart X values must be nondecreasing.");
            if (points.Count > 0 && value.x == points[points.Count - 1].x)
                points[points.Count - 1] = value;
            else
                points.Add(value);
        }
        Refresh();
    }

    // Call after changing axis configuration or line color.
    public void Refresh()
    {
        xTitle.text = XAxis.Title;
        yTitle.text = YAxis.Title;
        Bounds(XAxis, true, out xMin, out xMax);
        Bounds(YAxis, false, out yMin, out yMax);
        plot = new Rect(48, 8, Mathf.Max(0, canvas.contentRect.width - 66), Mathf.Max(0, canvas.contentRect.height - 32));
        ticks.Clear();
        empty.style.display = points.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (plot.width > 0 && plot.height > 0)
        {
            AddTicks(XAxis, true, xMin, xMax);
            AddTicks(YAxis, false, yMin, yMax);
        }
        canvas.MarkDirtyRepaint();
    }

    private void Bounds(Axis axis, bool horizontal, out float min, out float max)
    {
        min = float.PositiveInfinity;
        max = float.NegativeInfinity;
        foreach (Vector2 point in points)
        {
            float value = horizontal ? point.x : point.y;
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
        }
        if (points.Count == 0) { min = 0; max = 1; }
        min = axis.Min ?? min;
        max = axis.Max ?? max;
        if (!Finite(min) || !Finite(max)) { min = 0; max = 1; }
        if (max <= min)
        {
            float padding = Mathf.Max(1, Mathf.Abs(min) * 0.1f);
            if (axis.Max.HasValue && !axis.Min.HasValue) min = max - padding;
            else max = min + padding;
        }
    }

    private int TickCount(Axis axis, bool horizontal)
    {
        int available = Mathf.FloorToInt((horizontal ? plot.width : plot.height) / (horizontal ? 65 : 28)) + 1;
        return Mathf.Clamp(axis.TickCount, 2, Mathf.Max(2, Mathf.Min(10, available)));
    }

    private void AddTicks(Axis axis, bool horizontal, float min, float max)
    {
        int count = TickCount(axis, horizontal);
        for (int i = 0; i < count; i++)
        {
            float fraction = i / (float)(count - 1);
            var label = new Label(axis.Format(Mathf.Lerp(min, max, fraction)));
            label.style.position = Position.Absolute;
            label.style.fontSize = 10;
            label.style.width = horizontal ? 64 : 44;
            label.style.height = 18;
            label.style.unityTextAlign = horizontal ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
            label.style.left = horizontal ? plot.x + fraction * plot.width - 32 : 0;
            label.style.top = horizontal ? plot.yMax + 2 : plot.yMax - fraction * plot.height - 9;
            ticks.Add(label);
        }
    }

    private Vector2 Map(Vector2 point) => new Vector2(
        plot.x + (point.x - xMin) / (xMax - xMin) * plot.width,
        plot.yMax - (point.y - yMin) / (yMax - yMin) * plot.height);

    private void Draw(MeshGenerationContext context)
    {
        if (plot.width <= 0 || plot.height <= 0) return;
        Painter2D painter = context.painter2D;
        painter.lineWidth = 1;
        painter.strokeColor = new Color(0.84f, 0.86f, 0.84f);
        int yCount = TickCount(YAxis, false);
        for (int i = 0; i < yCount; i++)
        {
            float y = plot.y + plot.height * i / (yCount - 1);
            Segment(painter, new Vector2(plot.x, y), new Vector2(plot.xMax, y));
        }
        painter.strokeColor = new Color(0.91f, 0.92f, 0.90f);
        int xCount = TickCount(XAxis, true);
        for (int i = 1; i < xCount - 1; i++)
        {
            float x = plot.x + plot.width * i / (xCount - 1);
            Segment(painter, new Vector2(x, plot.y), new Vector2(x, plot.yMax));
        }
        painter.strokeColor = Color.gray;
        Segment(painter, new Vector2(plot.x, plot.y), new Vector2(plot.x, plot.yMax));
        Segment(painter, new Vector2(plot.x, plot.yMax), new Vector2(plot.xMax, plot.yMax));
        painter.lineWidth = 2;
        painter.strokeColor = LineColor;
        // Clip each segment against the data rectangle, without pinning outliers to an axis.
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = Map(points[i - 1]), b = Map(points[i]);
            Vector2 d = b - a;
            float enter = 0, leave = 1;
            if (Clip(-d.x, a.x - plot.x, ref enter, ref leave) &&
                Clip(d.x, plot.xMax - a.x, ref enter, ref leave) &&
                Clip(-d.y, a.y - plot.y, ref enter, ref leave) &&
                Clip(d.y, plot.yMax - a.y, ref enter, ref leave))
                Segment(painter, a + enter * d, a + leave * d);
        }
        if (points.Count == 1)
        {
            Vector2 p = Map(points[0]);
            if (p.x >= plot.x && p.x <= plot.xMax && p.y >= plot.y && p.y <= plot.yMax)
                Segment(painter, p, p + new Vector2(2, 0));
        }
    }

    private static bool Clip(float p, float q, ref float enter, ref float leave)
    {
        if (p == 0) return q >= 0;
        float r = q / p;
        if (p < 0) enter = Mathf.Max(enter, r);
        else leave = Mathf.Min(leave, r);
        return enter <= leave;
    }

    private static void Segment(Painter2D painter, Vector2 a, Vector2 b)
    {
        painter.BeginPath();
        painter.MoveTo(a);
        painter.LineTo(b);
        painter.Stroke();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
