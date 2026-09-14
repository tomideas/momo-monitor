using System.ComponentModel;
using System.Globalization;
using StatusMonitor.Services;
using StatusMonitor.Settings;

namespace StatusMonitor.ViewModels;

/// <summary>
/// One controllable fan, bound to the Fans page.
/// <para>
/// Two controls, the way Macs Fan Control frames it: a fan is on Auto or it is Custom, and
/// Custom is either a fixed speed or a ramp between two temperatures. Fan speeds always come
/// from what the hardware reports it can do, so no percentage is ever typed except for a
/// deliberately fixed speed.
/// </para>
/// </summary>
public sealed class FanProfileVm : INotifyPropertyChanged
{
    private readonly Action _save;

    // What the panel's controls are bound to. Edits land here and reach the fan only when Apply
    // is pressed, so a half-typed temperature never drives anything and leaving a panel open
    // changes nothing. Auto is the exception and applies at once: it is the safe direction.
    private FanProfile _draft;
    private bool _editing;

    public FanProfileVm(FanTarget target, FanProfile profile, Action save)
    {
        Target = target;
        Profile = profile;
        _draft = profile.Copy();
        _save = save;
    }

    public FanTarget Target { get; }
    public FanProfile Profile { get; }

    public string Title => $"{Target.HardwareName} · {Target.SensorName}";

    /// <summary>The fan's own range. Printed because its floor is rarely zero and surprises people.</summary>
    public string RangeText => string.Format(CultureInfo.InvariantCulture,
        "{0:0}–{1:0}%", Target.MinPercent, Target.MaxPercent);

    // ---- two choices, then one sub-choice ----

    public bool IsAuto
    {
        get => Profile.Mode == FanMode.Auto && !_editing;
        set
        {
            if (!value) return;
            // Handing the fan back needs no confirming, and anything half-entered goes with it.
            _editing = false;
            _draft = Profile.Copy();
            _draft.Mode = FanMode.Auto;
            Profile.Mode = FanMode.Auto;
            _save();
            Notify();
        }
    }

    /// <summary>
    /// Which of the two chips is lit. This is the fan's state, not the panel's: a fan on a
    /// custom speed reads as Custom whether or not the panel happens to be open.
    /// </summary>
    public bool IsCustom => Profile.Mode != FanMode.Auto || _editing;

    /// <summary>
    /// Whether the panel is open. Separate from <see cref="IsCustom"/> because the panel closes
    /// once its values are applied — leaving it open under every configured fan turns a list of
    /// six into a wall of controls, and the setting itself is reported on the row's own line.
    /// </summary>
    public bool IsEditing => _editing;

    /// <summary>Opens the panel. The chip stays pressable while already lit, so a fan that was
    /// configured earlier can be opened again.</summary>
    public void BeginEdit()
    {
        if (_editing) return;
        _editing = true;
        // Landing on the temperature ramp rather than a fixed speed: a constant speed ignores
        // temperature entirely, which is the choice that deserves to be deliberate.
        if (_draft.Mode == FanMode.Auto) _draft.Mode = FanMode.Sensor;
        Notify();
    }

    public bool IsConstant
    {
        get => _draft.Mode == FanMode.Constant;
        set { if (value) SetMode(FanMode.Constant); }
    }

    public bool IsSensor
    {
        get => _draft.Mode == FanMode.Sensor;
        set { if (value) SetMode(FanMode.Sensor); }
    }

    private void SetMode(FanMode mode)
    {
        if (_draft.Mode == mode) return;
        _draft.Mode = mode;
        Notify();
    }

    /// <summary>Takes what the panel holds, lets it drive the fan, and puts the panel away.</summary>
    public void Apply()
    {
        if (_draft.Mode == FanMode.Auto) _draft.Mode = FanMode.Sensor;
        Profile.CopyFrom(_draft);
        _editing = false;
        _save();
        Notify();
    }

    /// <summary>
    /// What the fan has actually been told to do, for the row's own line. The panel closes after
    /// Apply, so without this the setting would be invisible until it was opened again.
    /// </summary>
    public string SettingText => Profile.Mode switch
    {
        FanMode.Sensor => string.Format(CultureInfo.InvariantCulture, "{0:0}–{1:0} °C",
            Profile.StartTemperatureC, Profile.MaxTemperatureC),
        FanMode.Constant => string.Format(CultureInfo.InvariantCulture, "{0:0}%", Profile.ConstantPercent),
        _ => "",
    };

    /// <summary>Whether the panel holds anything the fan has not been given yet.</summary>
    public bool IsDirty =>
        Profile.Mode != _draft.Mode ||
        Profile.ConstantPercent != _draft.ConstantPercent ||
        Profile.StartTemperatureC != _draft.StartTemperatureC ||
        Profile.MaxTemperatureC != _draft.MaxTemperatureC;

    /// <summary>
    /// Which temperature this fan follows, stated rather than asked. The service resolves it
    /// from the hardware the fan is attached to — a GPU fan its own card, anything on the board
    /// whichever of CPU and GPU is hotter — so there is nothing to pick, only something to know.
    /// </summary>
    public bool HasCpuSource => Target.CpuSensorId.Length > 0;
    public bool HasGpuSource => Target.GpuSensorId.Length > 0;


    // ---- the three numbers ----
    // Each clamps and then announces the clamped value, so a box never shows something other
    // than what is in effect.

    public string ConstantPercent
    {
        get => _draft.ConstantPercent.ToString("0", CultureInfo.InvariantCulture);
        set => SetNumber(value, Target.MinPercent, Target.MaxPercent, v => _draft.ConstantPercent = v);
    }

    public string StartTemperature
    {
        get => _draft.StartTemperatureC.ToString("0", CultureInfo.InvariantCulture);
        set => SetNumber(value, 0, 120, v => _draft.StartTemperatureC = v);
    }

    public string MaxTemperature
    {
        get => _draft.MaxTemperatureC.ToString("0", CultureInfo.InvariantCulture);
        set => SetNumber(value, 0, 120, v => _draft.MaxTemperatureC = v);
    }

    private void SetNumber(string text, double min, double max, Action<double> assign)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return;
        assign(Math.Clamp(v, min, max));
        Notify();
    }

    // ---- the live reading ----
    // Macs Fan Control's hierarchy, and the reason it reads at a glance: exactly one number per
    // row is set big and bold — the speed the fan is actually running at. Everything else is
    // context arranged around it. The old single grey line gave all four facts the same weight,
    // which meant the speed, the one thing the page exists for, was the hardest part to find.

    /// <summary>The headline: what this fan is running at right now.</summary>
    public string SpeedText => Target.CurrentPercent is { } p
        ? p.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";

    /// <summary>Second rank: the same speed in RPM, and the temperature it is answering to.</summary>
    public string LiveText
    {
        get
        {
            string rpm = Target.CurrentRpm is { } r
                ? r.ToString("0", CultureInfo.InvariantCulture) + " RPM" : "—";
            return Target.SourceTemperatureC is { } t
                ? rpm + "  ·  " + t.ToString("0", CultureInfo.InvariantCulture) + " °C"
                : rpm;
        }
    }

    /// <summary>Under the name: who is driving this fan, what it was told, and its own range.</summary>
    public string SubText
    {
        get
        {
            string mode = Target.SoftwareControlled
                ? I18n.Loc.Instance["fan_mode_software"]
                : I18n.Loc.Instance["fan_mode_auto"];
            string setting = SettingText;
            return setting.Length > 0
                ? mode + "  ·  " + setting + "  ·  " + RangeText
                : mode + "  ·  " + RangeText;
        }
    }

    /// <summary>Refreshes the live reading; called each tick while the tab is on screen.</summary>
    public void RefreshLive()
    {
        foreach (string name in new[] { nameof(SpeedText), nameof(LiveText), nameof(SubText), nameof(SettingText) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public event PropertyChangedEventHandler? PropertyChanged;
}
