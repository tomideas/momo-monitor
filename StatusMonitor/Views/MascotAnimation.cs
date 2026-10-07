using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StatusMonitor.Views;

/// <summary>Embedded, registered animation frames; a clock runs only while playing.</summary>
internal sealed class MascotAnimation : IDisposable
{
    private readonly List<BitmapSource> _frames = [];
    private readonly int[] _durations;
    private readonly Action<BitmapSource> _display;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private int _index;
    private long _frameEnd;
    public bool IsPlaying => _timer.IsEnabled;
    public event Action? Completed;
    public int DurationMilliseconds => _durations.Sum();

    public MascotAnimation(string asset, Action<BitmapSource> display)
    {
        _display = display;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/" + asset)).Stream;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using (var timing = archive.GetEntry("durations.json")!.Open())
            _durations = JsonSerializer.Deserialize<int[]>(timing)!;
        for (int index = 0; index < _durations.Length; index++)
        {
            using var entry = archive.GetEntry($"{index:000}.png")!.Open();
            using var pixels = new MemoryStream();
            entry.CopyTo(pixels);
            pixels.Position = 0;
            var bitmap = BitmapFrame.Create(pixels, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            bitmap.Freeze();
            _frames.Add(bitmap);
        }
        _timer.Tick += Advance;
        _display(_frames[0]);
    }

    public void PlayOnce()
    {
        Stop();
        _index = 0;
        _frameEnd = _durations[0];
        _display(_frames[0]);
        _clock.Restart();
        _timer.Start();
    }

    private void Advance(object? sender, EventArgs e)
    {
        while (_clock.ElapsedMilliseconds >= _frameEnd)
        {
            if (_index == _frames.Count - 1) { Stop(); Completed?.Invoke(); return; }
            _frameEnd += _durations[++_index];
            _display(_frames[_index]);
        }
    }

    public void Stop() { _timer.Stop(); _clock.Stop(); }
    public void ShowRestFrame() { Stop(); _display(_frames[^1]); }
    public void Dispose() { Stop(); _timer.Tick -= Advance; Completed = null; }
}
