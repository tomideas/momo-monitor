using StatusMonitor.Services;
using StatusMonitor.Settings;
using StatusMonitor.I18n;
using StatusMonitor.ViewModels;

int checks = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
var start = DateTimeOffset.Parse("2026-10-06T00:00:00Z");
AlertSignal Cpu(double? value) => new("cpu_temp", "CPU temperature", value, 90, "°C", TrendMetric: "cpu_temp");

// Supported foreground/background intervals must all qualify, including normal timer jitter.
foreach (int interval in new[] { 1, 2, 5, 10, 30, 60 })
{
    var engine = new AlertEngine();
    int noticeCount = 0;
    for (double seconds = 0; seconds <= 180; seconds += interval + 0.03)
        noticeCount += engine.Evaluate(start.AddSeconds(seconds), new[] { Cpu(95) }, 15, 300, interval).Count;
    Check(noticeCount == 1, $"{interval}s sampling must notify exactly once during a sustained incident");
    Check(engine.IncidentUpdates.Single().Status == AlertIncidentStatus.Active, "The active incident must keep updating");
}

var frequency = new AlertEngine();
frequency.Evaluate(start, new[] { Cpu(95) }, 15, 300, 30);
Check(frequency.Evaluate(start.AddSeconds(30), new[] { Cpu(96) }, 15, 300, 2).Count == 1,
    "Changing from slow to fast sampling must preserve the previous legal interval");
string frequencyId = frequency.IncidentUpdates.Single().Id;
frequency.Evaluate(start.AddSeconds(32), new[] { Cpu(99) }, 15, 300, 2);
Check(frequency.IncidentUpdates.Single().Id == frequencyId, "Changing frequency must preserve incident identity");
frequency.Evaluate(start.AddSeconds(300), new[] { Cpu(95) }, 15, 300, 2);
Check(frequency.IncidentUpdates.Single().Status == AlertIncidentStatus.Interrupted &&
    frequency.IncidentUpdates.Single().EndedAt == start.AddSeconds(32), "A sleep gap ends at the last high observation");
Check(frequency.Evaluate(start.AddSeconds(302), new[] { Cpu(95) }, 15, 300, 2).Count == 0,
    "Time without samples cannot satisfy the hold period");

var engine2 = new AlertEngine();
engine2.Evaluate(start, new[] { Cpu(95) }, 15, 60, 5);
for (int s = 5; s <= 15; s += 5) engine2.Evaluate(start.AddSeconds(s), new[] { Cpu(95 + s / 5) }, 15, 60, 5);
var active = engine2.IncidentUpdates.Single();
Check(active.StartedAt == start && active.LastObservedAt == start.AddSeconds(15) && active.PeakValue == 98,
    "Sustained events retain the original start, latest sample and peak");
Check(active.NotifiedAt == start.AddSeconds(15), "Event and notice share the notification time");
var path = Path.Combine(AppSettings.DataDir, "alert-history.json");
var journal = new AlertHistory(path, start);
journal.Apply(start.AddSeconds(15), engine2.IncidentUpdates);
Check(File.Exists(path), "First sustained incident is persisted immediately");
journal.Acknowledge(active.Id, start.AddSeconds(16));
engine2.Evaluate(start.AddSeconds(20), new[] { Cpu(102) }, 15, 60, 5);
journal.Apply(start.AddSeconds(20), engine2.IncidentUpdates);
Check(journal.Events.Single().AcknowledgedAt == start.AddSeconds(16), "New sensor samples must preserve acknowledgement");
engine2.Evaluate(start.AddSeconds(25), new[] { Cpu(60) }, 15, 60, 5);
journal.Apply(start.AddSeconds(25), engine2.IncidentUpdates);
var recovered = journal.Events.Single();
Check(recovered.Status == AlertIncidentStatus.Recovered && recovered.EndedAt == start.AddSeconds(25) &&
    recovered.Duration.TotalSeconds == 25 && recovered.PeakValue == 102, "Recovery records duration and retains worst value");
Check(new AlertHistory(path, start.AddSeconds(26)).Events.Single().Status == AlertIncidentStatus.Recovered,
    "Recovered incident and acknowledgement survive restart");

// Cooldown suppresses balloons, not the existence of a distinct sustained incident.
engine2.Evaluate(start.AddSeconds(30), new[] { Cpu(95) }, 15, 60, 5);
for (int s = 35; s <= 45; s += 5)
    Check(engine2.Evaluate(start.AddSeconds(s), new[] { Cpu(95) }, 15, 60, 5).Count == 0, "Cooldown is respected");
Check(engine2.IncidentUpdates.Single().NotifiedAt is null, "A cooldown-suppressed sustained event remains visible");
journal.Apply(start.AddSeconds(45), engine2.IncidentUpdates);
var suppressed = journal.Events.First(e => e.Status == AlertIncidentStatus.Active);
Check(suppressed.Id != active.Id, "Recovery separates incident identities");
var restarted = new AlertHistory(path, start.AddHours(2));
Check(restarted.Events.Single(e => e.Id == suppressed.Id) is { Status: AlertIncidentStatus.Interrupted, EndReason: AlertEndReason.Restarted } ended &&
    ended.EndedAt == start.AddSeconds(45) && ended.Duration.TotalSeconds == 15, "Restart never accumulates unobserved time as an incident");

engine2.Evaluate(start.AddSeconds(50), new[] { Cpu(null) }, 15, 60, 5);
Check(engine2.IncidentUpdates.Single().EndReason == AlertEndReason.MissingReading, "Missing readings interrupt active events");
var engine3 = new AlertEngine();
engine3.Evaluate(start, new[] { Cpu(95) }, 3, 10);
Check(engine3.Evaluate(start.AddSeconds(20), new[] { Cpu(95) }, 3, 10).Count == 0,
    "Original optional-parameter behavior still treats long gaps as discontinuous");
engine3.Evaluate(start.AddSeconds(21), new[] { Cpu(30) }, 3, 10);
Check(engine3.IncidentUpdates.Count == 0, "Short excursions do not clutter the event journal");

var low = new AlertEngine();
var disk = new AlertSignal("disk:C:\\", "C: free space", 9, 10, "%", true);
low.Evaluate(start, new[] { disk }, 5, 0, 5);
low.Evaluate(start.AddSeconds(5), new[] { disk with { Value = 5 } }, 5, 0, 5);
Check(low.IncidentUpdates.Single().PeakValue == 5, "Low-space incidents track the lowest reading");
low.Reset(start.AddSeconds(6), AlertEndReason.AlertsDisabled);
Check(low.IncidentUpdates.Single().EndReason == AlertEndReason.AlertsDisabled, "Disabling monitoring explicitly ends an active event");

var gpu = new AlertEngine();
var gpuSignal = new AlertSignal("/gpu/one:temp", "GPU one temperature", 95, 85, "°C", TrendMetric: "gpu_temp", GpuId: "/gpu/one");
gpu.Evaluate(start, new[] { gpuSignal }, 5, 0, 5);
var gpuNotice = gpu.Evaluate(start.AddSeconds(5), new[] { gpuSignal }, 5, 0, 5).Single();
Check(gpuNotice.EventId == gpu.IncidentUpdates.Single().Id && gpuNotice.Signal.GpuId == "/gpu/one" &&
    gpu.IncidentUpdates.Single().TrendMetric == "gpu_temp", "Notification carries stable event, device and trend identities");

var retention = new AlertHistory(Path.Combine(AppSettings.DataDir, "retention.json"), start, persist: false);
retention.Apply(start, new[]
{
    recovered with { Id = "old", StartedAt = start.AddDays(-9), LastObservedAt = start.AddDays(-8), EndedAt = start.AddDays(-8) },
    recovered with { Id = "recent", StartedAt = start.AddDays(-7), LastObservedAt = start.AddDays(-6), EndedAt = start.AddDays(-6) }
});
Check(retention.Events.Count == 1 && retention.Events[0].Id == "recent", "Retain the full seven-day event window");

// Real atomic storage is exercised only in this isolated test data directory.
File.WriteAllText(path, "{broken");
var backupRecovered = new AlertHistory(path, start.AddHours(3));
Check(backupRecovered.Events.Count > 0 && DataStorageService.Status.Problem == DataStorageProblem.RecoveredData,
    "Corrupt journal recovers its previous valid backup");
var badPath = Path.Combine(AppSettings.DataDir, "unrecoverable.json");
File.WriteAllText(badPath, "{unrecoverable");
var damaged = new AlertHistory(badPath, start);
damaged.Apply(start, new[] { active });
Check(!damaged.Save() && File.ReadAllText(badPath) == "{unrecoverable", "Unrecoverable history must not be overwritten");
var preview = new AlertHistory(badPath, start, persist: false);
preview.Apply(start, new[] { active });
Check(preview.Events.Count == 1 && File.ReadAllText(badPath) == "{unrecoverable", "Preview events neither read nor write user data");

// ViewModel collection and parent summary must refresh together without waiting for a sensor tick.
var previewVm = new MainViewModel { CanPersist = false };
var vmChanges = new List<string?>();
previewVm.PropertyChanged += (_, change) => vmChanges.Add(change.PropertyName);
previewVm.RefreshAlertEventList();
Check(previewVm.AlertSummary == Loc.Instance["no_alerts"], "An empty preview starts with the localized seven-day message");
vmChanges.Clear();
var previewIncident = active with { Id = "preview-reactive", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), LastObservedAt = DateTimeOffset.UtcNow };
previewVm.PreviewAlertEvent(previewIncident);
Check(vmChanges.Contains(nameof(MainViewModel.AlertSummary)) && previewVm.AlertSummary.Contains(previewIncident.Label),
    "Preview insertion must notify the parent summary immediately");
previewVm.SelectAlertEvent(previewIncident.Id);
AlertEventNavigation? navigation = null;
previewVm.AlertEventNavigationRequested += value => navigation = value;
previewVm.InspectSelectedAlert();
Check(navigation is { EventId: "preview-reactive", TrendMetric: "cpu_temp" }, "Selected event navigation identifies the stored event and metric");
previewVm.AcknowledgeAlertEvent(previewIncident.Id);
Check(previewVm.SelectedAlertEvent is { IsAcknowledged: true, IsActive: true },
    "Acknowledgement records that the event was seen without claiming the condition recovered");
previewVm.PreviewAlertEvent(previewIncident with { Status = AlertIncidentStatus.Recovered,
    EndReason = AlertEndReason.Recovered, EndedAt = previewIncident.LastObservedAt, CurrentValue = 50 });
Check(previewVm.SelectedAlertEvent is { IsAcknowledged: true, IsActive: false } selected &&
    selected.Incident.Status == AlertIncidentStatus.Recovered, "Recovery preserves acknowledgement and the selected event row");
string previousSummary = previewVm.AlertSummary;
vmChanges.Clear();
Loc.Instance.SetLanguage(Loc.Instance.Language == "en" ? "zh" : "en");
previewVm.RefreshAlertEventLanguage();
Check(vmChanges.Contains(nameof(MainViewModel.AlertSummary)) && previewVm.AlertSummary != previousSummary,
    "Changing language must refresh the parent summary as well as event rows");

Console.WriteLine($"PASS: {checks} assertions (sampling intervals, frequency changes, incident lifecycle, cooldown, restart, retention, acknowledgement, backup recovery, preview isolation, reactive summaries and navigation)");
