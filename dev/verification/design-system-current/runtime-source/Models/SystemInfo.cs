namespace StatusMonitor.Models;

/// <summary>A label / value pair shown inside an info card.</summary>
public sealed class InfoItem
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";

    /// <summary>
    /// 0..1 fill for items that represent a proportion (drive capacity, GPU memory). Null for
    /// plain facts, which render as label / value text only.
    /// </summary>
    public double? Fraction { get; set; }

    public bool HasBar => Fraction.HasValue;
}

/// <summary>A titled card on the Info page.</summary>
public sealed class InfoCard
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public List<InfoItem> Items { get; set; } = new();
    public IEnumerable<InfoItem> SummaryItems => Items.Take(3);
}
