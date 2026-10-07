namespace StatusMonitor.Models;

/// <summary>One node on a fan curve: at this temperature, run this percentage.</summary>
public sealed class FanPoint
{
    public double TemperatureC { get; set; }
    public double Percent { get; set; }
}

/// <summary>
/// Curve maths, deliberately separate from any hardware so it can be tested without a GPU
/// present. Nothing here writes anything; <see cref="Services.FanControlService"/> owns that.
/// </summary>
public static class FanCurve
{
    /// <summary>
    /// The ramp the Fans tab actually configures: hold the fan's own minimum until
    /// <paramref name="startC"/>, rise linearly, reach the fan's maximum at
    /// <paramref name="maxC"/>.
    /// <para>
    /// Two temperatures and nothing else. An editable node table let a curve say anything, at
    /// the cost of making the common case — "spin up sooner and harder than the firmware
    /// does" — an exercise in filling in ten boxes. The speeds come from what the hardware
    /// reports it can do, so there are no percentages to enter at all. Tightening the two
    /// temperatures is what makes the ramp aggressive: 55 → 80 reaches ~74% at 70 °C on a fan
    /// with a 34% floor, where 50 → 90 would only reach ~67%.
    /// </para>
    /// </summary>
    public static List<FanPoint> Ramp(double startC, double maxC, double minPercent, double maxPercent)
    {
        // A non-positive span would divide by zero in the interpolation; nudging the top above
        // the bottom turns it into a step at that temperature, which is a sane reading of
        // "start and finish at the same place".
        if (maxC <= startC) maxC = startC + 0.1;
        return new List<FanPoint>
        {
            new() { TemperatureC = startC, Percent = minPercent },
            new() { TemperatureC = maxC, Percent = maxPercent },
        };
    }

    /// <summary>
    /// Percentage for a temperature: flat below the first node, flat above the last, linear
    /// between. Flat rather than extrapolated on purpose — extrapolating past the ends of a
    /// user-drawn curve invents fan speeds nobody asked for, in both directions.
    /// </summary>
    public static double Evaluate(IReadOnlyList<FanPoint> points, double temperatureC)
    {
        if (points.Count == 0) return 0;

        var sorted = Sorted(points);
        if (temperatureC <= sorted[0].TemperatureC) return sorted[0].Percent;
        if (temperatureC >= sorted[^1].TemperatureC) return sorted[^1].Percent;

        for (int i = 1; i < sorted.Count; i++)
        {
            var high = sorted[i];
            if (temperatureC > high.TemperatureC) continue;
            var low = sorted[i - 1];
            double span = high.TemperatureC - low.TemperatureC;
            if (span <= 0) return high.Percent;
            double t = (temperatureC - low.TemperatureC) / span;
            return low.Percent + (high.Percent - low.Percent) * t;
        }
        return sorted[^1].Percent;
    }

    /// <summary>Ascending by temperature; the editor lets points be entered in any order.</summary>
    public static List<FanPoint> Sorted(IReadOnlyList<FanPoint> points) =>
        points.OrderBy(p => p.TemperatureC).ToList();
}

/// <summary>
/// Applies hysteresis to a curve so the fan does not oscillate. A workload that alternates
/// between bursts and idle walks the temperature across a node repeatedly; without this the
/// fan audibly hunts up and down on every crossing.
/// <para>
/// Rising temperature is followed immediately — cooling should never be delayed. Falling
/// temperature only takes effect once it has dropped a full <c>hysteresis</c> below the
/// temperature that set the current output, so the fan steps down once rather than flutters.
/// </para>
/// </summary>
public sealed class FanCurveState
{
    private double? _appliedTemperature;
    private double _output;

    /// <summary>The percentage currently being asked for, before any hardware clamping.</summary>
    public double Output => _output;

    public void Reset()
    {
        _appliedTemperature = null;
        _output = 0;
    }

    public double Next(IReadOnlyList<FanPoint> points, double temperatureC, double hysteresisC, double failsafeTemperatureC)
    {
        // The failsafe ignores both the curve and the hysteresis. If the card is this hot, the
        // curve has already failed to keep up and a delayed response is the wrong answer.
        if (failsafeTemperatureC > 0 && temperatureC >= failsafeTemperatureC)
        {
            _appliedTemperature = temperatureC;
            return _output = 100;
        }

        if (_appliedTemperature is null ||
            temperatureC > _appliedTemperature ||
            temperatureC <= _appliedTemperature - Math.Max(0, hysteresisC))
        {
            _appliedTemperature = temperatureC;
            _output = FanCurve.Evaluate(points, temperatureC);
        }
        return _output;
    }
}
