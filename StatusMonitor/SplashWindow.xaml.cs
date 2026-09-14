using System.Windows;
using System.Windows.Media.Animation;
using StatusMonitor.Settings;
using StatusMonitor.Views;

namespace StatusMonitor;

/// <summary>
/// Brand panel shown while the hardware hub opens. It owns no sensors and no timers beyond
/// its own sweep, so it can be created before anything else is ready.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow(AppSettings settings)
    {
        InitializeComponent();
        FontFamily = AppFont.Resolve(settings.Font);
        Loaded += (_, _) => StartSweep();
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
        PipShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        Close();
    }
}
