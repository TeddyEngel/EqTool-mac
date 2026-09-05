using Avalonia.Media;
using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Models;
using EQTool.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EQTool.Avalonia.ViewModels
{
    public class OverlayBannerViewModel
    {
        public OverlayBannerViewModel(string text, IBrush foreground, DateTime expiresAt)
        {
            Text = text;
            Foreground = foreground;
            ExpiresAt = expiresAt;
        }

        public string Text { get; }

        public IBrush Foreground { get; }

        public DateTime ExpiresAt { get; }
    }

    public class OverlayTimerBarViewModel : INotifyPropertyChanged
    {
        private readonly DateTime endsAt;
        private readonly double totalSeconds;

        public OverlayTimerBarViewModel(string name, int totalSeconds, IBrush barColour)
        {
            Name = name;
            BarColour = barColour;
            this.totalSeconds = Math.Max(1, totalSeconds);
            endsAt = DateTime.Now.AddSeconds(this.totalSeconds);
        }

        public string Name { get; }

        public IBrush BarColour { get; }

        public double SecondsLeft => Math.Max(0, (endsAt - DateTime.Now).TotalSeconds);

        public bool HasExpired => SecondsLeft <= 0;

        public double PercentLeft => Math.Clamp(SecondsLeft / totalSeconds * 100.0, 0, 100);

        public string CountdownText
        {
            get
            {
                var remaining = (int)Math.Ceiling(SecondsLeft);
                if (remaining < 60)
                    return remaining + "s";

                return (remaining / 60) + "m " + (remaining % 60) + "s";
            }
        }

        public void Tick()
        {
            OnPropertyChanged(nameof(SecondsLeft));
            OnPropertyChanged(nameof(PercentLeft));
            OnPropertyChanged(nameof(CountdownText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // The floating overlay: trigger banners, countdown bars and complete-heal
    // chain lines.
    //
    // This is the feature the native client exists for. The Wine build cannot do
    // it: WS_EX_TRANSPARENT never reaches Cocoa through winemac.drv, so overlay
    // content there swallows every click. Here the same effect comes from
    // setIgnoresMouseEvents on the NSWindow, which was proven in the spike.
    public class EventOverlayViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);
        private const int MaximumChainLines = 6;

        private readonly LogEvents logEvents;
        private readonly DispatcherTimer ticker;

        public EventOverlayViewModel() : this(AppServices.Initialize())
        {
        }

        public EventOverlayViewModel(AppServices services)
        {
            logEvents = services.Resolve<LogEvents>();

            logEvents.OverlayEvent += OnOverlay;
            logEvents.TimerBarEvent += OnTimerBar;
            logEvents.CompleteHealEvent += OnCompleteHeal;

            ticker = new DispatcherTimer { Interval = TickInterval };
            ticker.Tick += OnTick;
            ticker.Start();
        }

        public ObservableCollection<OverlayBannerViewModel> Banners { get; }
            = new ObservableCollection<OverlayBannerViewModel>();

        public ObservableCollection<OverlayTimerBarViewModel> TimerBars { get; }
            = new ObservableCollection<OverlayTimerBarViewModel>();

        public ObservableCollection<string> ChainLines { get; }
            = new ObservableCollection<string>();

        public bool IsIdle => Banners.Count == 0 && TimerBars.Count == 0 && ChainLines.Count == 0;

        private void OnOverlay(object sender, OverlayEvent e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Text))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                Banners.Add(new OverlayBannerViewModel(
                    e.Text,
                    ShimBrushMap.Resolve(e.ForeGround, "BrushSignalAmber"),
                    DateTime.Now.Add(e.Duration)));

                OnPropertyChanged(nameof(IsIdle));
            });
        }

        private void OnTimerBar(object sender, TimerBarEvent e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Name))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                TimerBars.Add(new OverlayTimerBarViewModel(
                    e.Name,
                    e.TotalSeconds,
                    ShimBrushMap.Resolve(e.BarColor, "BrushSignalMint")));

                OnPropertyChanged(nameof(IsIdle));
            });
        }

        // Chain heals are read in call order during a raid, so the newest line goes
        // on top and the list is capped rather than growing without bound.
        private void OnCompleteHeal(object sender, CompleteHealEvent e)
        {
            if (e == null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                var position = string.IsNullOrWhiteSpace(e.Position) ? string.Empty : e.Position + "  ";
                var caster = string.IsNullOrWhiteSpace(e.Caster) ? string.Empty : "  (" + e.Caster + ")";
                ChainLines.Insert(0, position + e.Recipient + caster);

                while (ChainLines.Count > MaximumChainLines)
                    ChainLines.RemoveAt(ChainLines.Count - 1);

                OnPropertyChanged(nameof(IsIdle));
            });
        }

        private void OnTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var changed = false;

            foreach (var expired in Banners.Where(a => a.ExpiresAt <= now).ToList())
            {
                Banners.Remove(expired);
                changed = true;
            }

            foreach (var bar in TimerBars.ToList())
            {
                if (bar.HasExpired)
                {
                    TimerBars.Remove(bar);
                    changed = true;
                    continue;
                }

                bar.Tick();
            }

            if (changed)
                OnPropertyChanged(nameof(IsIdle));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            ticker.Stop();
            ticker.Tick -= OnTick;
            logEvents.OverlayEvent -= OnOverlay;
            logEvents.TimerBarEvent -= OnTimerBar;
            logEvents.CompleteHealEvent -= OnCompleteHeal;
        }
    }
}
