using System.Windows;
using System.Windows.Media.Animation;

namespace StatusMonitor.Views;

internal static class Motion
{
    public static bool Reduced { get; set; }

    public static void Transition(FrameworkElement target, DependencyProperty property, double value)
    {
        double current = (double)target.GetValue(property);
        target.BeginAnimation(property, null);
        target.SetValue(property, value);
        if (Reduced || !SystemParameters.ClientAreaAnimation || !target.IsVisible ||
            Window.GetWindow(target)?.WindowState == WindowState.Minimized) return;
        target.BeginAnimation(property, new DoubleAnimation(current, value, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }
}
