namespace StatusMonitor.Models;

/// <summary>A rectangle in screen coordinates, free of any windowing type so it can be tested.</summary>
public readonly record struct Box(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public bool Intersects(Box other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

/// <summary>
/// Whether a remembered window position is still usable.
/// <para>
/// A saved rectangle names a place on a desktop that may no longer exist: a laptop undocked, a
/// second monitor unplugged, a display rearranged. Restoring onto one of those looks exactly
/// like the app failing to start — the process runs, the window is somewhere nobody can see.
/// The test is not whether the window overlaps a screen but whether its <em>title bar</em>
/// does, because a window you cannot grab is only marginally better than one you cannot see.
/// </para>
/// </summary>
public static class WindowPlacement
{
    /// <summary>The strip that has to be reachable: enough title bar to grab and drag.</summary>
    public const double GripWidth = 160;
    public const double GripHeight = 32;

    /// <summary>
    /// How much of that strip has to be showing. Any overlap at all is not the right test: a
    /// window hanging off the right edge with twenty pixels of title bar visible is reachable in
    /// arithmetic and not in practice.
    /// </summary>
    public const double MinVisibleGripWidth = 60;
    public const double MinVisibleGripHeight = 16;

    public static bool IsReachable(Box saved, IEnumerable<Box> workAreas)
    {
        var grip = new Box(saved.Left, saved.Top, GripWidth, GripHeight);
        foreach (var area in workAreas)
        {
            double width = Math.Min(grip.Right, area.Right) - Math.Max(grip.Left, area.Left);
            double height = Math.Min(grip.Bottom, area.Bottom) - Math.Max(grip.Top, area.Top);
            if (width >= MinVisibleGripWidth && height >= MinVisibleGripHeight) return true;
        }
        return false;
    }
}
