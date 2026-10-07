using System.ComponentModel;
using System.Globalization;
using StatusMonitor.Settings;

namespace StatusMonitor.ViewModels;

/// <summary>
/// One graphics card the wattage total had to leave out, with a box for the owner to supply
/// its board power.
/// <para>
/// This exists because neither of the other two routes can be made to cover a program handed
/// to strangers. A table of models is always behind the market and never knew the obscure
/// parts anyway; and asking the driver does not help, because a card that cannot report its
/// power draw cannot report its power limit either — both come out of the same
/// power-management block. What is left is the person holding the card, who can read the
/// figure off a spec sheet in a few seconds, and whose answer is exact rather than inferred.
/// </para>
/// </summary>
public sealed class GpuTdpVm : INotifyPropertyChanged
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly AppSettings _settings;
    private readonly Action _save;

    public GpuTdpVm(string name, AppSettings settings, Action save)
    {
        Name = name;
        _settings = settings;
        _save = save;
    }

    /// <summary>The card as the driver names it, which is also the key the figure is stored under.</summary>
    public string Name { get; }

    /// <summary>
    /// Board power in watts, or empty for "still unknown". Empty is a real answer and is kept
    /// as one: the card then stays out of the total and the page keeps saying so, which is
    /// better than a blank box quietly meaning zero.
    /// </summary>
    public string Watts
    {
        get => _settings.GpuTdpWatts.TryGetValue(Name, out double watts) && watts > 0
            ? watts.ToString("0", Inv)
            : "";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) _settings.GpuTdpWatts.Remove(Name);
            else if (double.TryParse(value, NumberStyles.Float, Inv, out double parsed))
            {
                // A card outside this range is a typo, not a card. The ceiling is above any
                // single board on sale and the floor is below the smallest passive part.
                if (parsed < 1 || parsed > 1000) return;
                _settings.GpuTdpWatts[Name] = parsed;
            }
            else return;

            _save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Watts)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
