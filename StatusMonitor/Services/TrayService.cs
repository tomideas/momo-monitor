using System.Windows;
using StatusMonitor.I18n;
using Forms = System.Windows.Forms;

namespace StatusMonitor.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _image;
    private readonly Action _restore;
    private readonly Action _mini;
    private readonly Action _exit;
    public bool IsVisible => _icon.Visible;
    public TrayService(Action restore, Action mini, Action exit)
    {
        _restore = restore; _mini = mini; _exit = exit;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;
        using var original = new System.Drawing.Icon(stream);
        _image = (System.Drawing.Icon)original.Clone();
        _icon = new Forms.NotifyIcon { Icon = _image, Visible = true };
        _icon.DoubleClick += (_, _) => Application.Current.Dispatcher.Invoke(_restore);
        _icon.BalloonTipClicked += (_, _) => Application.Current.Dispatcher.Invoke(_restore);
        Localize();
        Loc.Instance.PropertyChanged += LanguageChanged;
    }
    private void LanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Localize();
    private void Localize()
    {
        _icon.Text = Loc.Instance["app_title"];
        var old = _icon.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.Instance["restore"], null, (_, _) => Application.Current.Dispatcher.Invoke(_restore));
        menu.Items.Add(Loc.Instance["mini"], null, (_, _) => Application.Current.Dispatcher.Invoke(_mini));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.Instance["exit"], null, (_, _) => Application.Current.Dispatcher.Invoke(_exit));
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }
    public void Notify(IReadOnlyList<AlertNotice> notices)
    {
        string body = string.Join(Environment.NewLine, notices.Select(n => $"{n.Signal.Label}: {n.Signal.Value:0.0}{n.Signal.Unit}"));
        _icon.ShowBalloonTip(6000, Loc.Instance["app_title"], body, Forms.ToolTipIcon.Warning);
    }
    public void Dispose()
    {
        Loc.Instance.PropertyChanged -= LanguageChanged;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
