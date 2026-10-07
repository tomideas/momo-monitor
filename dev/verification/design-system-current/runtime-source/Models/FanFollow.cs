namespace StatusMonitor.Models;

/// <summary>
/// Which temperature a fan answers to, based on the hardware it serves.
/// <para>
/// A fan's position on the machine gives one automatic answer — a GPU fan follows its own card,
/// a board header the CPU. Kept free of any hardware type so the rule can be tested
/// without a fan present.
/// </para>
/// </summary>
public static class FanFollow
{
    /// <summary>
    /// Resolves exactly one category. GPU fans use their own GPU by default; board fans use CPU.
    /// </summary>
    public static string Category(string kind) => kind == "gpu" ? "gpu" : "cpu";
}
