using System.ComponentModel;
using System.Globalization;
using StatusMonitor.I18n;
using StatusMonitor.Models;
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

    public FanProfileVm(FanTarget target, FanProfile profile, FanIdentity identity, Action save)
    {
        Target = target;
        Profile = profile;
        Identity = identity;
        _draft = profile.Copy();
        _save = save;
    }

    public FanTarget Target { get; }
    public FanProfile Profile { get; }

    /// <summary>The name the owner gave this fan. Saved as its own record.</summary>
    public FanIdentity Identity { get; }

    /// <summary>
    /// What the row is called. The owner's name wins; otherwise the socket label is enough for
    /// the list, with the long hardware name staying in the tooltip.
    /// </summary>
    public string Title => Identity.Name.Trim().Length > 0
        ? Identity.Name
        : string.IsNullOrWhiteSpace(Target.SensorName) ? Target.HardwareName : Target.SensorName;

    /// <summary>
    /// Which glyph marks the row. Read from the effective name, so a header the owner labels
    /// "CPU Fan" is drawn as one — the name is the only thing the hardware tells us, and the
    /// owner's name is a better copy of it than "Fan #4".
    /// </summary>
    public string Icon => Target.Kind == "gpu" ? "gpu" : FanNaming.IconFor(Title);

    public string HardwareText => string.Equals(Title, Target.HardwareName, StringComparison.Ordinal)
        ? Target.HardwareName : $"{Target.HardwareName} · {Target.SensorName}";

    /// <summary>The fan's own range. Printed because its floor is rarely zero and surprises people.</summary>
    public string RangeText => string.Format(CultureInfo.InvariantCulture,
        "{0:0}–{1:0}%", Target.MinPercent, Target.MaxPercent);

    // ---- the name ----
    // Renaming is independent of the fan's mode: a header the owner cannot identify is worth
    // labelling whether or not they ever drive it. Committed on Enter or when the box loses
    // focus, so no name is ever half-saved.

    private bool _renaming;
    private string _renameText = "";

    public bool IsRenaming => _renaming;

    /// <summary>The other half of <see cref="IsRenaming"/>, so the label and the box can swap in
    /// one slot without a converter that inverts.</summary>
    public bool IsNotRenaming => !_renaming;

    public string RenameText
    {
        get => _renameText;
        set
        {
            _renameText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RenameText)));
        }
    }

    /// <summary>Opens the rename box, seeded with what the row currently shows.</summary>
    public void BeginRename()
    {
        if (_renaming) return;
        _renaming = true;
        _renameText = Title;
        Notify();
    }

    /// <summary>Takes the typed name. Blank hands the row back to the hardware's own name.</summary>
    public void CommitRename()
    {
        if (!_renaming) return;
        _renaming = false;
        Identity.Name = _renameText.Trim();
        _save();
        Notify();
    }

    /// <summary>Leaves the name as it was. Clears the flag first, so the box losing focus on the
    /// way out does not then commit what was being undone.</summary>
    public void CancelRename()
    {
        if (!_renaming) return;
        _renaming = false;
        Notify();
    }

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
            Target.ControlStatus = FanControlStatus.WaitingForApply;
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
        Target.ControlStatus = FanControlStatus.WaitingForApply;
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
    // Exactly one number per row is big and bold: the measured RPM. Percentage and temperature
    // are useful context, but making the requested percentage larger than the measured speed
    // forced the eye to decode two competing readings on every row.

    /// <summary>The headline: what this fan is running at right now.</summary>
    public string SpeedText => Target.CurrentRpm is { } rpm
        ? rpm.ToString("0", CultureInfo.InvariantCulture) + " RPM" : "—";

    /// <summary>Second rank: measured percentage and the monitored temperature in every mode.</summary>
    public string LiveText
    {
        get
        {
            var parts = new List<string>();
            if (Target.CurrentPercent is { } p)
                parts.Add(p.ToString("0", CultureInfo.InvariantCulture) + "%");
            if (Target.SourceTemperatureC is { } t)
                parts.Add(t.ToString("0", CultureInfo.InvariantCulture) + " °C");
            else
                parts.Add(Loc.Instance["temp_short"] + " —");
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>Only a custom setting needs a second line; the selected chip already states the mode.</summary>
    public string SubText => SettingText;

    /// <summary>
    /// Names the sensor being displayed. Auto and constant modes monitor it but do not claim
    /// that the firmware uses this sensor to decide its own speed.
    /// </summary>
    public string SourceText => Loc.Instance[Profile.Mode == FanMode.Sensor
        ? "fan_curve_source" : "fan_monitor_source"] + " · " + TemperatureSourceText;

    public string TemperatureSourceText
    {
        get
        {
            string category = Target.SourceCategory switch
            {
                "cpu" => "CPU",
                "gpu" => "GPU",
                _ => Loc.Instance["no_data"],
            };
            if (Target.SourceSensorName.Length == 0) return category;
            return Target.SourceSensorName.Contains(category, StringComparison.OrdinalIgnoreCase)
                ? Target.SourceSensorName : category + " " + Target.SourceSensorName;
        }
    }

    /// <summary>The actual last control result, rather than the selected saved-mode chip.</summary>
    public string ControlStatusText => Loc.Instance[Target.ControlStatus switch
    {
        FanControlStatus.Firmware => "fan_control_firmware",
        FanControlStatus.Software => "fan_control_applied",
        FanControlStatus.SourceUnavailable => "fan_control_source_missing",
        FanControlStatus.DriverRejected => "fan_control_rejected",
        FanControlStatus.FirmwareReleaseRejected => "fan_control_release_failed",
        FanControlStatus.ReadOnly => "fan_control_readonly",
        _ => "fan_control_pending",
    }];

    public bool HasControlWarning => Target.ControlStatus is FanControlStatus.SourceUnavailable
        or FanControlStatus.DriverRejected or FanControlStatus.FirmwareReleaseRejected;

    /// <summary>Zero is a stopped fan; a missing value is never described as zero or normal.</summary>
    public string ReadingStatusText
    {
        get
        {
            var parts = new List<string>();
            if (Target.CurrentRpm == 0) parts.Add(Loc.Instance["fan_stopped"]);
            else if (Target.CurrentRpm is null) parts.Add(Loc.Instance["fan_rpm_unavailable"]);
            if (Target.SourceTemperatureC is null) parts.Add(Loc.Instance["fan_temp_unavailable"]);
            return string.Join(" · ", parts);
        }
    }

    public bool HasReadingNotice => ReadingStatusText.Length > 0;

    public string ReadingStatusDetail
    {
        get
        {
            var parts = new List<string>();
            if (Target.CurrentRpm == 0) parts.Add(Loc.Instance["fan_stopped_detail"]);
            else if (Target.CurrentRpm is null)
                parts.Add(Loc.Instance[Target.RpmStatus == FanReadingStatus.MissingSensor
                    ? "fan_rpm_unsupported" : "fan_rpm_missing"]);
            if (Target.SourceTemperatureC is null)
                parts.Add(Loc.Instance[Target.TemperatureStatus == FanReadingStatus.MissingSensor
                    ? "fan_temp_unsupported" : "fan_temp_missing"]);
            if (HasControlWarning) parts.Add(ControlStatusText);
            return string.Join(Environment.NewLine, parts);
        }
    }

    /// <summary>Refreshes the live reading; called each tick while the tab is on screen.</summary>
    public void RefreshLive()
    {
        foreach (string name in new[]
        {
            nameof(SpeedText), nameof(LiveText), nameof(SubText), nameof(SettingText),
            nameof(SourceText), nameof(TemperatureSourceText), nameof(ControlStatusText),
            nameof(HasControlWarning), nameof(ReadingStatusText), nameof(ReadingStatusDetail),
            nameof(HasReadingNotice),
        })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public event PropertyChangedEventHandler? PropertyChanged;
}
