using System.Windows;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StatusMonitor.Settings;
using StatusMonitor.Views;

namespace StatusMonitor;

/// <summary>
/// Brand panel shown while the hardware hub opens. Owns the one-shot mascot playback
/// and loading sweep; sensors remain owned by the dashboard.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly List<BitmapSource> _frames = [];
    private readonly int[] _durations;
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(10) };
    private readonly Stopwatch _playback = new();
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _frameIndex;
    private long _frameEnd;
    public Task AnimationCompleted => _completed.Task;

    public SplashWindow(AppSettings settings)
    {
        InitializeComponent();
        FontFamily = AppFont.Resolve(settings.Font);
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/startup.frames.zip")).Stream;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using (var timing = archive.GetEntry("durations.json")!.Open())
            _durations = JsonSerializer.Deserialize<int[]>(timing)!;
        for (int index = 0; index < _durations.Length; index++)
        {
            using var frame = archive.GetEntry($"{index:000}.png")!.Open();
            using var pixels = new MemoryStream();
            frame.CopyTo(pixels);
            pixels.Position = 0;
            var bitmap = BitmapFrame.Create(pixels, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            bitmap.Freeze();
            _frames.Add(bitmap);
        }
        Mascot.Source = _frames[0];
        _frameTimer.Tick += (_, _) => AdvanceFrame();
        Loaded += (_, _) => { StartPlayback(); StartSweep(); };
        Closed += (_, _) => StopPlayback();
    }

    private void StartPlayback()
    {
        if (Motion.Reduced || !SystemParameters.ClientAreaAnimation)
        {
            Mascot.Source = _frames[^1];
            _completed.TrySetResult();
            return;
        }
        _frameEnd = _durations[0];
        _playback.Start();
        _frameTimer.Start();
    }

    private void AdvanceFrame()
    {
        while (_playback.ElapsedMilliseconds >= _frameEnd)
        {
            if (_frameIndex == _frames.Count - 1)
            {
                StopPlayback();
                return;
            }
            _frameEnd += _durations[++_frameIndex];
            Mascot.Source = _frames[_frameIndex];
        }
    }

    private void StopPlayback()
    {
        _frameTimer.Stop();
        _playback.Stop();
        _completed.TrySetResult();
    }

    /// <summary>
    /// Sweeps the block across the track until the window closes. The work behind it is a
    /// single opaque driver call, so there is no honest percentage to show — this says
    /// "still going", nothing more.
    /// </summary>
    private void StartSweep()
    {
        // Reduced motion gets a static block rather than a still empty track: the point is
        // that something is happening, and an empty bar reads as stalled.
        if (Motion.Reduced || !SystemParameters.ClientAreaAnimation)
        {
            PipShift.X = (Track.ActualWidth - Pip.Width) / 2;
            return;
        }

        double travel = Track.ActualWidth - Pip.Width;
        if (travel <= 0) return;

        PipShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(0, travel, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    /// <summary>Stops the sweep before closing so the animation clock is not left running.</summary>
    public void Finish()
    {
        StopPlayback();
        PipShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        Close();
    }
}
