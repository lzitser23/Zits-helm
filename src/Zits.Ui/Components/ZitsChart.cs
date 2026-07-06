using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Zits.Ui;

/// <summary>
/// One series' presentation in a <see cref="ChartConfig"/>: a human <paramref name="Label"/>,
/// a CSS <paramref name="Color"/> (any colour expression: a token like
/// <c>var(--chart-1)</c>, an <c>hsl(...)</c>, a hex), and an optional inline-SVG
/// <see cref="Icon"/>. One per-key chart config entry.
/// </summary>
public sealed record ChartSeries(string? Label = null, string? Color = null)
{
    /// <summary>Optional inline-SVG icon rendered beside the label in the legend/tooltip.</summary>
    public RenderFragment? Icon { get; init; }
}

/// <summary>
/// The <c>ChartConfig</c>: a Key → <see cref="ChartSeries"/> map. A
/// <c>ZitsChartContainer</c> injects a <c>--color-&lt;key&gt;</c> CSS var per entry
/// (the ChartStyle behaviour) so descendants colour themselves with
/// <c>var(--color-&lt;key&gt;)</c>, and the tooltip/legend read it for labels + icons.
/// </summary>
public sealed class ChartConfig : Dictionary<string, ChartSeries>
{
    public ChartConfig() { }
    public ChartConfig(IDictionary<string, ChartSeries> source) : base(source) { }
}

/// <summary>
/// One row of chart data: a category <paramref name="Label"/> (the x-axis tick) and a
/// per-series-key → numeric value map. The convenience constructor takes tuples:
/// <c>new ChartDataPoint("Jan", ("desktop", 186), ("mobile", 80))</c>.
/// </summary>
public sealed record ChartDataPoint(string Label, IReadOnlyDictionary<string, double> Values)
{
    public ChartDataPoint(string label, params (string Key, double Value)[] values)
        : this(label, values.ToDictionary(v => v.Key, v => v.Value)) { }
}

/// <summary>
/// One line of a tooltip: the series <paramref name="Key"/> and its numeric
/// <paramref name="Value"/>, with optional per-item label/colour overrides. When the
/// overrides are null the tooltip falls back to the cascaded <see cref="ChartConfig"/>
/// (label) and <c>var(--color-&lt;key&gt;)</c> (colour).
/// </summary>
public sealed record ChartTooltipPayloadItem(string Key, double Value)
{
    public string? Label { get; init; }
    public string? Color { get; init; }
}

/// <summary>
/// Pure-C# scale + layout maths for the hand-rolled SVG charts. Coordinates live in a
/// fixed <c>0 0 100 100</c> viewBox (the chart stretches it with
/// <c>preserveAspectRatio="none"</c>) so a user-unit equals a percent, which keeps the
/// HTML tooltip/axis overlays in lock-step with the SVG geometry.
/// </summary>
internal static class ChartMath
{
    /// <summary>Top edge of the plot band (headroom above the tallest value).</summary>
    public const double Top = 6;

    /// <summary>Bottom edge of the plot band (baseline for bars/areas).</summary>
    public const double Bottom = 96;

    /// <summary>Invariant number formatting so SVG path data + CSS get <c>12.5</c>, never <c>12,5</c>.</summary>
    public static string N(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Map a data value to a y coordinate within the plot band (0 at bottom).</summary>
    public static double Y(double value, double yMax)
        => Top + (1 - value / (yMax <= 0 ? 1 : yMax)) * (Bottom - Top);

    /// <summary>Centre x (a percent) of category <paramref name="i"/> of <paramref name="n"/>.</summary>
    public static double SlotCenter(int i, int n) => n <= 0 ? 0 : (i + 0.5) * 100.0 / n;

    /// <summary>The largest value across the plotted series, rounded up to a "nice" axis max.</summary>
    public static double NiceMax(IEnumerable<ChartDataPoint> data, IReadOnlyList<string> series)
    {
        double max = 0;
        foreach (var p in data)
            foreach (var k in series)
                if (p.Values.TryGetValue(k, out var v) && v > max)
                    max = v;
        return Nice(max);
    }

    /// <summary>Round a raw maximum up to a 1/2/2.5/5/10 × 10ⁿ axis bound.</summary>
    public static double Nice(double rawMax)
    {
        if (rawMax <= 0) return 1;
        var pow = Math.Pow(10, Math.Floor(Math.Log10(rawMax)));
        var f = rawMax / pow;
        double nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return nf * pow;
    }

    // ---- Polar helpers (additive: Pie/Donut, Radar, RadialBar) -----------------
    // The linear helpers above own the x-slot / y-band model of the three original
    // chart types; the polar charts render into a square sub-box centred at (50,50)
    // (so a circle stays round under preserveAspectRatio="none") and use these.

    /// <summary>Centre of the square polar plot, in viewBox units.</summary>
    public const double Cx = 50;

    /// <summary>Centre of the square polar plot, in viewBox units.</summary>
    public const double Cy = 50;

    /// <summary>
    /// x of a point <paramref name="r"/> from centre-x <paramref name="cx"/> at
    /// <paramref name="angleDeg"/>, where 0° is 12 o'clock, growing clockwise (so
    /// bars/slices read like a clock, not like maths degrees).
    /// </summary>
    public static double PolarX(double cx, double r, double angleDeg)
        => cx + r * Math.Cos((angleDeg - 90) * Math.PI / 180);

    /// <summary>y counterpart of <see cref="PolarX"/> (centre-y <paramref name="cy"/>).</summary>
    public static double PolarY(double cy, double r, double angleDeg)
        => cy + r * Math.Sin((angleDeg - 90) * Math.PI / 180);

    /// <summary>
    /// A filled pie slice (<paramref name="rInner"/> = 0) or donut ring segment
    /// (<paramref name="rInner"/> &gt; 0), swept clockwise from <paramref name="a0"/> to
    /// <paramref name="a1"/> degrees. Callers pass <c>a0 &lt; a1</c>.
    /// </summary>
    public static string SlicePath(double cx, double cy, double rOuter, double rInner, double a0, double a1)
    {
        if (a1 - a0 >= 359.999) a1 = a0 + 359.999; // keep a full ring's arc non-degenerate
        var large = (a1 - a0) > 180 ? 1 : 0;
        double ox0 = PolarX(cx, rOuter, a0), oy0 = PolarY(cy, rOuter, a0);
        double ox1 = PolarX(cx, rOuter, a1), oy1 = PolarY(cy, rOuter, a1);
        if (rInner <= 0)
            return $"M {N(cx)} {N(cy)} L {N(ox0)} {N(oy0)} A {N(rOuter)} {N(rOuter)} 0 {large} 1 {N(ox1)} {N(oy1)} Z";
        double ix1 = PolarX(cx, rInner, a1), iy1 = PolarY(cy, rInner, a1);
        double ix0 = PolarX(cx, rInner, a0), iy0 = PolarY(cy, rInner, a0);
        return $"M {N(ox0)} {N(oy0)} A {N(rOuter)} {N(rOuter)} 0 {large} 1 {N(ox1)} {N(oy1)} " +
               $"L {N(ix1)} {N(iy1)} A {N(rInner)} {N(rInner)} 0 {large} 0 {N(ix0)} {N(iy0)} Z";
    }

    /// <summary>
    /// An open arc along radius <paramref name="r"/> swept clockwise from
    /// <paramref name="a0"/> to <paramref name="a1"/> degrees (the stroked RadialBar
    /// segments and their background tracks). Empty when the sweep is zero.
    /// </summary>
    public static string ArcPath(double cx, double cy, double r, double a0, double a1)
    {
        if (a1 - a0 < 0.001) return string.Empty;
        if (a1 - a0 >= 359.999) a1 = a0 + 359.999;
        var large = (a1 - a0) > 180 ? 1 : 0;
        double x0 = PolarX(cx, r, a0), y0 = PolarY(cy, r, a0);
        double x1 = PolarX(cx, r, a1), y1 = PolarY(cy, r, a1);
        return $"M {N(x0)} {N(y0)} A {N(r)} {N(r)} 0 {large} 1 {N(x1)} {N(y1)}";
    }
}
