using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using StatusMonitor.Models;
using StatusMonitor.Services;
using StatusMonitor.Settings;

namespace StatusMonitor.ViewModels;

public sealed class ProcessRowVm
{
    public string Name { get; init; } = "";
    public string Cpu { get; init; } = "";
    public string Gpu { get; init; } = "";
    public string Ram { get; init; } = "";
    public string Power { get; init; } = "";
    public string Disk { get; init; } = "";
}

public sealed class GpuCard
{
    public string MemoryLabel { get; init; } = "VRAM";
    public string Name { get; init; } = "";
    public double Load { get; init; }
    public string LoadText { get; init; } = "";
    public string TempText { get; init; } = "";
    public double TempFraction { get; init; }
    public string ClockText { get; init; } = "";
    public double ClockFraction { get; init; }
    public string FanText { get; init; } = "";
    public double FanFraction { get; init; }
    public string PowerText { get; init; } = "";
    public double PowerFraction { get; init; }
    public string VramText { get; init; } = "";
    public double VramFraction { get; init; }

    /// <summary>Assigned by the view model once the load is known; alarm past the threshold.</summary>
    public Views.BarTone Tone { get; set; }

    public double LoadFraction => Math.Clamp(Load / 100.0, 0, 1);

    /// <summary>
    /// The detail line under the bar: clock, memory, temperature — the same order the CPU row
    /// uses, with temperature labelled and last. It used to sit unlabelled in the middle, so a
    /// bare "33°" had to be guessed at while the CPU row spelled its own out.
    /// </summary>
    public string SubText =>
        $"{ClockText} · {MemoryLabel} {VramText} · {I18n.Loc.Instance["temp_short"]} {TempText}";
}

/// <summary>
/// One drive inside the dashboard's DISK row. Updated in place rather than rebuilt each tick,
/// so the bar keeps animating from its previous value instead of restarting from zero.
/// </summary>
public sealed class DiskRowVm : INotifyPropertyChanged
{
    /// <summary>Drive root ("C:\"), used to tell whether the set of drives changed.</summary>
    public string Key { get; init; } = "";

    public string Label { get; private set; } = "";
    public string CapacityText { get; private set; } = "";
    public string PercentText { get; private set; } = "";
    public double Fraction { get; private set; }
    public Views.BarTone Tone { get; private set; }

    public void Update(Models.DiskVolume disk, double freeLimit)
    {
        Label = disk.Name.TrimEnd('\\', '/');
        CapacityText = $"{Cap(disk.UsedGb)} / {Cap(disk.TotalGb)}";
        PercentText = (disk.UsedFraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        Fraction = disk.UsedFraction;
        // A drive running out of room is the one thing worth recolouring for.
        Tone = disk.TotalGb > 0 && disk.FreePercent < freeLimit ? Views.BarTone.Alarm : Views.BarTone.Normal;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static string Cap(double gb) =>
        gb >= 1000 ? (gb / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " TB"
                   : gb.ToString("0", CultureInfo.InvariantCulture) + " GB";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly MonitorService _service;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private Timer? _timer;
    private int _busy;
    private int _infoRequest;
    private volatile bool _stopped;
    private readonly object _sampleLock = new();
    private Snapshot _snap = new();
    private double _cpuBaseMhz;
    private int _cpuCores;
    private int _cpuThreads;

    public AppSettings Settings { get; }
    public ObservableCollection<ProcessRowVm> Processes { get; } = new();
    public ObservableCollection<InfoCard> InfoCards { get; } = new();

    /// <summary>True while the hardware information tab is visible.</summary>
    public bool IsInfoVisible { get; set; }

    public MainViewModel(AppSettings settings)
    {
        Settings = settings;
        _service = new MonitorService(settings);
    }

    public bool HasWarning => _service.Limited;

    public double CpuLoad => _snap.Cpu.LoadPercent ?? 0;
    public string CpuLoadText => Pct(_snap.Cpu.LoadPercent);
    public string CpuTempText => Temp(_snap.Cpu.TemperatureC);
    public string CpuClockText => Clock(_snap.Cpu.ClockMhz);
    public string CpuFanText => Rpm(_snap.Cpu.FanRpm);
    public string CpuPowerText => Watts(_snap.Cpu.PowerWatts ?? 0);
    public double CpuTempFraction => Frac(_snap.Cpu.TemperatureC, 100);
    public double CpuClockFraction => Frac(_snap.Cpu.ClockMhz, 6000);
    public double CpuFanFraction => Frac(_snap.Cpu.FanRpm, 2500);
    public double CpuPowerFraction => Frac(_snap.Cpu.PowerWatts, 200);
    public string CpuCoresText => _cpuCores > 0 ? $"{_cpuCores}C / {_cpuThreads}T" : "";
    public string CpuBaseClockText => _cpuBaseMhz >= 1000
        ? (_cpuBaseMhz / 1000.0).ToString("0.00", Inv) + " GHz"
        : _cpuBaseMhz > 0 ? _cpuBaseMhz.ToString("0", Inv) + " MHz" : "—";
    public double CpuBaseFraction => _cpuBaseMhz > 0 ? Frac(_snap.Cpu.ClockMhz, _cpuBaseMhz) : 0;

    public GpuCard? GpuCard1 { get; private set; }
    public GpuCard? GpuCard2 { get; private set; }
    public bool Gpu1Visible => GpuCard1 is not null;
    public bool Gpu2Visible => GpuCard2 is not null;

    public double RamLoad => _snap.RamLoadPercent ?? 0;
    public string RamLoadText => Pct(_snap.RamLoadPercent);
    public string RamDetailText => $"{_snap.RamUsedGb:0.0} / {_snap.RamTotalGb:0.0} GB";

    // ---- storage is occupancy, not activity ----
    // The DISK row reports how full each fixed volume is, not "% Disk Time". There is no
    // aggregate figure on purpose: averaging a nearly-full SSD with a roomy HDD hides the
    // one that needs attention.
    /// <summary>One entry per fixed volume, laid out side by side inside the single DISK row.</summary>
    public ObservableCollection<DiskRowVm> DiskRows { get; } = new();

    /// <summary>
    /// The fullest volume, for the mini panel where only one disk figure fits. Showing the
    /// fullest one (rather than an average) keeps the number that could actually become a
    /// problem.
    /// </summary>
    private DiskRowVm? FullestDisk => DiskRows.OrderByDescending(d => d.Fraction).FirstOrDefault();

    public string DiskPeakText => FullestDisk is { } d ? $"{d.Label} {d.PercentText}" : "—";
    public Views.BarTone DiskPeakTone => FullestDisk?.Tone ?? Views.BarTone.Normal;

    private void RefreshDiskRows()
    {
        var disks = _snap.Disks;
        // Only rebuild when the set of drives actually changes; otherwise update in place.
        if (DiskRows.Count != disks.Count || !DiskRows.Select(r => r.Key).SequenceEqual(disks.Select(d => d.Name)))
        {
            DiskRows.Clear();
            foreach (var disk in disks) DiskRows.Add(new DiskRowVm { Key = disk.Name });
        }
        for (int i = 0; i < disks.Count; i++) DiskRows[i].Update(disks[i], Settings.DiskFreeLimit);
    }

    // ---- what colour means ----
    // Every bar is the skin's accent; the bars share one baseline, so their lengths already do
    // the comparing. Colour is therefore reserved for a single statement: this reading is past
    // the threshold. An earlier version tinted only the highest bar, which made the colour
    // arbitrary — being the largest of several small numbers is not worth highlighting.
    /// <summary>Where a reading stops being routine. Matches the alert engine's default.</summary>
    public const double AlarmPercent = 90;

    private static Views.BarTone ToneFor(double load) =>
        load >= AlarmPercent ? Views.BarTone.Alarm : Views.BarTone.Normal;

    public Views.BarTone CpuTone => ToneFor(CpuLoad);
    public Views.BarTone RamTone => ToneFor(RamLoad);

    public double CpuFraction => Math.Clamp(CpuLoad / 100.0, 0, 1);
    public double RamFraction => Math.Clamp(RamLoad / 100.0, 0, 1);

    public string NetDownText => Rate(_snap.NetDownBytesPerSec);
    public string NetUpText => Rate(_snap.NetUpBytesPerSec);

    // ---- accumulated figures, per period ----
    // One selector governs energy, carbon and cost together: they are the same measurement
    // expressed three ways, so letting them disagree about the period would be nonsense.
    /// <summary>
    /// False under --render, where Settings is a throwaway instance. Without this guard a
    /// preview run would persist its temporary settings over the user's real file.
    /// </summary>
    public bool CanPersist
    {
        get => _canPersist;
        // A preview run must not drive hardware either, and it is the same condition.
        set { _canPersist = value; _service.AllowFanControl = value; }
    }

    private bool _canPersist = true;

    /// <summary>Software fan control, for the settings page to bind against.</summary>
    public Services.FanControlService Fans => _service.Fans;

    /// <summary>Hands every driven fan back to its firmware. Safe to call more than once.</summary>
    public void RevertFans() => _service.Fans.RevertAll();

    public Services.EnergyPeriod EnergyPeriod
    {
        get => Settings.EnergyPeriod;
        set
        {
            if (Settings.EnergyPeriod == value) return;
            Settings.EnergyPeriod = value;
            if (CanPersist) Settings.Save();
            OnPropertyChanged(null);
        }
    }

    // Two-way so the chips drive the selection directly; the template lives in App.xaml where
    // a code-behind handler would resolve against App, not the window. Setting false is the
    // radio group un-checking a sibling and carries no meaning of its own.
    public bool IsPeriodToday
    {
        get => EnergyPeriod == Services.EnergyPeriod.Today;
        set { if (value) EnergyPeriod = Services.EnergyPeriod.Today; }
    }

    public bool IsPeriodWeek
    {
        get => EnergyPeriod == Services.EnergyPeriod.Week;
        set { if (value) EnergyPeriod = Services.EnergyPeriod.Week; }
    }

    public bool IsPeriodMonth
    {
        get => EnergyPeriod == Services.EnergyPeriod.Month;
        set { if (value) EnergyPeriod = Services.EnergyPeriod.Month; }
    }

    public bool IsPeriodAll
    {
        get => EnergyPeriod == Services.EnergyPeriod.All;
        set { if (value) EnergyPeriod = Services.EnergyPeriod.All; }
    }

    /// <summary>
    /// Says when the day-by-day record starts, whenever the chosen window reaches back further
    /// than the record does. Without this, picking "7 days" right after the feature shipped
    /// shows far less than "All" and looks like a bug rather than a record that only just began.
    /// </summary>
    public string EnergyNote
    {
        get
        {
            if (!_service.History.IsPartial(EnergyPeriod, DateOnly.FromDateTime(DateTime.Now))) return "";
            var first = _service.History.FirstDay;
            return first is null
                ? I18n.Loc.Instance["record_empty"]
                : string.Format(I18n.Loc.Instance["record_since"], first.Value.ToString("MM-dd"));
        }
    }

    public bool HasEnergyNote => EnergyNote.Length > 0;

    /// <summary>The "累計用電" caption, named for the period actually being shown.</summary>
    public string EnergyPeriodLabel => I18n.Loc.Instance[EnergyPeriod switch
    {
        Services.EnergyPeriod.Today => "period_today",
        Services.EnergyPeriod.Week => "period_week",
        Services.EnergyPeriod.Month => "period_month",
        _ => "energy_total",
    }];

    public string TotalWattsText => Watts(_snap.TotalWatts);

    /// <summary>The hero numeral without its unit, so the unit can be set at its own size.</summary>
    public string TotalWattsValue => _snap.TotalWatts.ToString("0.0", Inv);

    /// <summary>The detail line under each load row.</summary>
    public string CpuSubText =>
        $"{CpuCoresText} · {CpuClockText} · {CpuPowerText} · {I18n.Loc.Instance["temp_short"]} {CpuTempText}";

    /// <summary>Free space is the number that actually matters; the row name already shows used/total.</summary>
    public string RamSubText => _snap.RamTotalGb > 0
        ? $"{(_snap.RamTotalGb - _snap.RamUsedGb).ToString("0.0", Inv)} GB {I18n.Loc.Instance["available"]}"
        : "—";
    public string EnergyText => Energy(_snap.TotalEnergyWh);
    public string CarbonText => Carbon(_snap.CarbonGrams);
    public string CostText => Cost(_snap.CostAmount, _snap.CostCurrency);

    public string FansText => _snap.Fans.Count == 0
        ? "—"
        : string.Join("     ", _snap.Fans.Select(f => $"{f.Name}  {f.Rpm:0} RPM"));

    public void Start()
    {
        var (_, cores, threads, baseMhz) = SystemInfoService.GetCpuStatic();
        _cpuCores = cores;
        _cpuThreads = threads;
        _cpuBaseMhz = baseMhz;
        Apply(_service.Sample()); // prime sensors and publish initial values before building specifications
        RefreshInfo();
        _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        ApplyInterval();
    }

    /// <summary>
    /// Same work as <see cref="Start"/>, but the WMI query and the first sensor read happen
    /// off the UI thread. Opening the hardware hub loads a driver and can take a second or
    /// more; done inline it froze the window before it had drawn anything. The sample runs
    /// under the same lock the timer uses, so nothing here races with a tick.
    /// </summary>
    public async Task StartAsync()
    {
        var (_, cores, threads, baseMhz) = await Task.Run(SystemInfoService.GetCpuStatic);
        _cpuCores = cores;
        _cpuThreads = threads;
        _cpuBaseMhz = baseMhz;

        var snap = await Task.Run(() => { lock (_sampleLock) return _service.Sample(); });
        if (_stopped) return;
        Apply(snap);
        RefreshInfo();
        _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        ApplyInterval();
    }

    /// <summary>Rebuilds the Info page cards (also called when the language changes).</summary>
    public async void RefreshInfo()
    {
        int request = ++_infoRequest;
        var snapshot = _snap;
        var cards = await Task.Run(() => SystemInfoService.Build(snapshot));
        if (_stopped || request != _infoRequest) return;
        InfoCards.Clear();
        foreach (var card in cards) InfoCards.Add(card);
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            Snapshot snap;
            lock (_sampleLock)
            {
                if (_stopped) return;
                snap = _service.Sample();
            }
            _dispatcher.BeginInvoke(new Action(() => { if (!_stopped) Apply(snap); }));
        }
        catch { if (!_stopped) _dispatcher.BeginInvoke(new Action(() => { if (!_stopped) MarkStale(); })); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    private void Apply(Snapshot snap)
    {
        _snap = snap;
        RefreshGpuCards();
        // PeakLoad needs every card built first, so tones are stamped after the refresh.
        if (GpuCard1 is not null) GpuCard1.Tone = ToneFor(GpuCard1.Load);
        if (GpuCard2 is not null) GpuCard2.Tone = ToneFor(GpuCard2.Load);
        RefreshDiskRows();
        RecordFeatures(DateTimeOffset.UtcNow);
        Processes.Clear();
        foreach (var row in snap.Processes) Processes.Add(ToVm(row));
        OnPropertyChanged(null);
        // Hardware specifications are refreshed on entry, not every sensor tick.
    }

    /// <summary>
    /// True while no window is on screen. Nothing is being read then, so the sensors drop to
    /// the idle interval — the energy total keeps accruing, the readings just arrive less
    /// often. Set by the windows as they show and hide.
    /// </summary>
    public bool IsIdle
    {
        get => _idle;
        set
        {
            if (_idle == value) return;
            _idle = value;
            // Coming back on screen reads at once, so the window is never showing a figure
            // left over from the idle interval.
            ApplyInterval(dueNow: !value);
        }
    }

    private bool _idle;

    /// <summary>Re-arms the timer at the interval the current state calls for.</summary>
    public void ApplyInterval(bool dueNow = false)
    {
        if (_timer is null || _stopped) return;
        int seconds = _idle ? Settings.EffectiveIdleRefreshSeconds : Settings.EffectiveRefreshSeconds;
        var period = TimeSpan.FromSeconds(seconds);
        _timer.Change(dueNow ? TimeSpan.Zero : period, period);
    }

    public void Stop(bool save)
    {
        _stopped = true;
        _timer?.Dispose();
        lock (_sampleLock)
        {
            if (save) _service.SaveTotals();
            _service.Dispose();
        }
    }

    public void ResetTotals()
    {
        _service.ResetTotals();
        OnPropertyChanged(null);
    }

    private static ProcessRowVm ToVm(ProcessRow r) => new()
    {
        Name = r.Name,
        Cpu = Pct(r.CpuPercent),
        Gpu = r.GpuPercent.HasValue ? Pct(r.GpuPercent) : "—",
        Ram = Mb(r.RamMb),
        Power = Watts(r.PowerWatts),
        Disk = Rate(r.DiskReadBytesPerSec + r.DiskWriteBytesPerSec),
    };

    private static GpuCard? BuildGpuCard(Snapshot snap, int i)
    {
        if (i < 0 || i >= snap.Gpus.Count) return null;
        var g = snap.Gpus[i];
        double? usedMb = g.MemoryUsedMb, totalMb = g.MemoryTotalMb;
        bool vramOk = usedMb.HasValue && totalMb.HasValue && totalMb.Value > 0;
        string vramText = vramOk
            ? $"{(usedMb!.Value / 1024.0).ToString("0.0", Inv)} / {(totalMb!.Value / 1024.0).ToString("0.0", Inv)} GB"
            : usedMb.HasValue ? Gb(usedMb.Value) : "—";
        return new GpuCard
        {
            // Every GPU row reads "VRAM" so the column scans as one thing. Whether the memory is
            // dedicated or shared with system RAM is a spec, and lives in the Info card's details.
            MemoryLabel = "VRAM",
            Name = i < snap.GpuNames.Count ? snap.GpuNames[i] : "",
            Load = g.LoadPercent ?? 0,
            LoadText = Pct(g.LoadPercent),
            TempText = Temp(g.TemperatureC),
            TempFraction = Frac(g.TemperatureC, 100),
            ClockText = Clock(g.ClockMhz),
            ClockFraction = Frac(g.ClockMhz, 3000),
            FanText = Rpm(g.FanRpm),
            FanFraction = Frac(g.FanRpm, 3000),
            PowerText = g.PowerWatts.HasValue ? Watts(g.PowerWatts.Value) : "—",
            PowerFraction = Frac(g.PowerWatts, 600),
            VramText = vramText,
            VramFraction = vramOk ? Math.Clamp(usedMb!.Value / totalMb!.Value, 0, 1) : 0,
        };
    }

    // ---- formatting ----

    private static string Gb(double mb) => (mb / 1024.0).ToString("0.0", Inv) + " GB";

    private static string Pct(double? v) => v.HasValue ? v.Value.ToString("0", Inv) + "%" : "—";
    private static string Temp(double? v) => v.HasValue ? v.Value.ToString("0", Inv) + "°" : "—";
    private static string Clock(double? v) => v.HasValue ? v.Value.ToString("0", Inv) + " MHz" : "—";
    private static string Rpm(double? v) => v.HasValue ? v.Value.ToString("0", Inv) + " RPM" : "—";
    private static double Frac(double? v, double max) => v.HasValue ? Math.Clamp(v.Value / max, 0, 1) : 0;
    private static string Watts(double v) => v.ToString("0.0", Inv) + " W";

    private static string Mb(double mb) =>
        mb >= 1024 ? (mb / 1024).ToString("0.0", Inv) + " GB" : mb.ToString("0", Inv) + " MB";

    private static string Rate(double bps)
    {
        if (bps >= 1048576) return (bps / 1048576).ToString("0.0", Inv) + " MB/s";
        if (bps >= 1024) return (bps / 1024).ToString("0.0", Inv) + " KB/s";
        return bps.ToString("0", Inv) + " B/s";
    }

    // A decimal below 10 Wh: with no decimals a freshly started "Today" reads exactly "0 Wh",
    // which looks broken rather than "barely any yet".
    private static string Energy(double wh) =>
        wh >= 1000 ? (wh / 1000).ToString("0.00", Inv) + " kWh"
        : wh >= 10 ? wh.ToString("0", Inv) + " Wh"
        : wh.ToString("0.0", Inv) + " Wh";

    private static string Carbon(double grams) =>
        grams >= 1000 ? (grams / 1000).ToString("0.00", Inv) + " kg" : grams.ToString("0", Inv) + " g";

    private static string Cost(double amount, string symbol) => symbol + amount.ToString("0.00", Inv);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
