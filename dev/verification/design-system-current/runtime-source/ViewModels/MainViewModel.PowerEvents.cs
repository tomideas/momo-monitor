using Microsoft.Win32;

namespace StatusMonitor.ViewModels;

public sealed partial class MainViewModel
{
    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_stopped) return;
        if (e.Mode == PowerModes.Suspend)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            lock (_sampleLock)
            {
                if (_stopped) return;
                _service.SuspendMonitoring();
            }
            _dispatcher.BeginInvoke(new Action(() => { if (!_stopped) MarkStale(); }));
        }
        else if (e.Mode == PowerModes.Resume)
        {
            lock (_sampleLock)
            {
                if (_stopped) return;
                _service.ResumeMonitoring();
            }
            _dispatcher.BeginInvoke(new Action(() => { if (!_stopped) ApplyInterval(dueNow: true); }));
        }
    }
}
