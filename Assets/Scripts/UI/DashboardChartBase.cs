using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class DashboardChartBase : VisualElement
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
    public bool ShowXGrid { get; set; } = true;
    public bool ShowYGrid { get; set; } = true;
    public bool ShowXLabels { get; set; } = true;
    public bool ShowYLabels { get; set; } = true;
    public string Title { get => title.text; set => title.text = value; }
    protected readonly VisualElement Canvas = new VisualElement();
    protected Rect Plot;
    protected readonly Label Empty = new Label("Waiting for data");
    private readonly Label title = new Label();
    private readonly Label xTitle = new Label();
    private readonly Label yTitle = new Label();
    private readonly VisualElement ticks = new VisualElement();

    protected DashboardChartBase()
    {
        style.flexGrow = 1;
        style.minHeight = 160;
        style.minWidth = 0;
        style.paddingLeft = style.paddingRight = 8;
        style.paddingTop = style.paddingBottom = 6;
        style.backgroundColor = new Color(.975f, .98f, .97f);
        title.style.fontSize = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(.12f, .22f, .14f);
        Add(title);
        yTitle.style.fontSize = 11;
        Add(yTitle);
        Canvas.style.flexGrow = 1;
        Canvas.style.minHeight = 90;
        Canvas.style.overflow = Overflow.Hidden;
        Add(Canvas);
        ticks.style.position = Position.Absolute;
        ticks.StretchToParentSize();
        ticks.pickingMode = PickingMode.Ignore;
        Canvas.Add(ticks);
        Empty.style.position = Position.Absolute;
        Empty.style.left = 56;
        Empty.style.top = 12;
        Empty.style.fontSize = 11;
        Canvas.Add(Empty);
        xTitle.style.fontSize = 11;
        xTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        Add(xTitle);
        Canvas.generateVisualContent += Draw;
        Canvas.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
    }

    public void Refresh()
    {
        xTitle.text = XAxis.Title;
        yTitle.text = YAxis.Title;
        Plot = new Rect(PlotLeft, 8, Mathf.Max(0, Canvas.contentRect.width - PlotLeft - 16), Mathf.Max(0, Canvas.contentRect.height - 30));
        ticks.Clear();
        GetBounds(out float xMin, out float xMax, out float yMin, out float yMax);
        NormalizeBounds(XAxis, ref xMin, ref xMax);
        NormalizeBounds(YAxis, ref yMin, ref yMax);
        if (Plot.width > 0 && Plot.height > 0)
        {
            if (ShowXLabels) AddTicks(XAxis, true, xMin, xMax);
            if (ShowYLabels) AddTicks(YAxis, false, yMin, yMax);
        }
        Empty.style.display = HasData ? DisplayStyle.None : DisplayStyle.Flex;
        Canvas.MarkDirtyRepaint();
    }

    protected abstract bool HasData { get; }
    protected virtual float PlotLeft => 48;
    protected abstract void GetBounds(out float xMin, out float xMax, out float yMin, out float yMax);
    protected abstract void DrawData(Painter2D painter, float xMin, float xMax, float yMin, float yMax);

    private void Draw(MeshGenerationContext context)
    {
        if (Plot.width <= 0 || Plot.height <= 0) return;
        GetBounds(out float xMin, out float xMax, out float yMin, out float yMax);
        NormalizeBounds(XAxis, ref xMin, ref xMax);
        NormalizeBounds(YAxis, ref yMin, ref yMax);
        Painter2D painter = context.painter2D;
        painter.lineWidth = 1;
        painter.strokeColor = new Color(.86f, .88f, .85f);
        if (ShowXGrid) Grid(painter, XAxis.TickCount, true);
        if (ShowYGrid) Grid(painter, YAxis.TickCount, false);
        painter.strokeColor = new Color(.42f, .46f, .42f);
        Segment(painter, new Vector2(Plot.x, Plot.y), new Vector2(Plot.x, Plot.yMax));
        Segment(painter, new Vector2(Plot.x, Plot.yMax), new Vector2(Plot.xMax, Plot.yMax));
        if (HasData) DrawData(painter, xMin, xMax, yMin, yMax);
    }

    protected Vector2 Map(float x, float y, float xMin, float xMax, float yMin, float yMax) => new(
        Plot.x + (x - xMin) / (xMax - xMin) * Plot.width,
        Plot.yMax - (y - yMin) / (yMax - yMin) * Plot.height);

    protected static void Segment(Painter2D painter, Vector2 a, Vector2 b)
    {
        painter.BeginPath();
        painter.MoveTo(a);
        painter.LineTo(b);
        painter.Stroke();
    }

    private void Grid(Painter2D painter, int requested, bool vertical)
    {
        int count = Mathf.Clamp(requested, 2, 10);
        for (int i = 0; i < count; i++)
        {
            float f = i / (float)(count - 1);
            if (vertical) Segment(painter, new Vector2(Mathf.Lerp(Plot.x, Plot.xMax, f), Plot.y), new Vector2(Mathf.Lerp(Plot.x, Plot.xMax, f), Plot.yMax));
            else Segment(painter, new Vector2(Plot.x, Mathf.Lerp(Plot.y, Plot.yMax, f)), new Vector2(Plot.xMax, Mathf.Lerp(Plot.y, Plot.yMax, f)));
        }
    }

    private void AddTicks(Axis axis, bool horizontal, float min, float max)
    {
        int count = Mathf.Clamp(axis.TickCount, 2, 10);
        for (int i = 0; i < count; i++)
        {
            float f = i / (float)(count - 1);
            var label = new Label(axis.Format(Mathf.Lerp(min, max, f)));
            label.style.position = Position.Absolute;
            label.style.fontSize = 10;
            label.style.width = horizontal ? 62 : 44;
            label.style.height = 18;
            label.style.unityTextAlign = horizontal ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
            label.style.left = horizontal ? Plot.x + f * Plot.width - 31 : 0;
            label.style.top = horizontal ? Plot.yMax + 1 : Plot.yMax - f * Plot.height - 9;
            ticks.Add(label);
        }
    }

    private static void NormalizeBounds(Axis axis, ref float min, ref float max)
    {
        if (axis.Min.HasValue) min = axis.Min.Value;
        if (axis.Max.HasValue) max = axis.Max.Value;
        if (!float.IsFinite(min) || !float.IsFinite(max)) { min = 0; max = 1; }
        if (max <= min) max = min + Mathf.Max(1, Mathf.Abs(min) * .1f);
    }
}
