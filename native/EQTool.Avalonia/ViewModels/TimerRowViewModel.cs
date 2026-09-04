using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EQTool.Avalonia.Services;
using EQTool.ViewModels.SpellWindow;

namespace EQTool.Avalonia.ViewModels
{
    // One row in the timer list.
    //
    // The countdown itself lives in upstream's `TimerViewModel`, which is driven
    // by `SpellWindowViewModel.UpdateSpells`. This wrapper exists to translate
    // that into things Avalonia can bind to: a real `IBrush` instead of the
    // `System.Windows.Media.SolidColorBrush` shim, and change notifications on
    // the two properties the row actually shows.
    public class TimerRowViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly TimeSpan UrgentThreshold = TimeSpan.FromSeconds(10);

        private readonly TimerViewModel timer;
        private bool urgent;

        public TimerRowViewModel(TimerViewModel timer, SpellIconService iconService)
        {
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));

            if (iconService == null)
                throw new ArgumentNullException(nameof(iconService));

            Accent = ToAvaloniaBrush(timer.ProgressBarColor);
            AccentWash = ToWashBrush(timer.ProgressBarColor);
            Icon = ResolveIcon(timer, iconService);
            urgent = timer.TotalRemainingDuration <= UrgentThreshold;

            timer.PropertyChanged += OnTimerPropertyChanged;
        }

        public TimerViewModel Source => timer;

        public string Name => timer.Name;

        public string GroupName => timer.GroupName?.Trim();

        public string Countdown => timer.SecondsLeftPretty;

        // Bound to a ProgressBar with Minimum 0 / Maximum 100.
        public double PercentLeft => Math.Max(0, Math.Min(100, timer.PercentLeft));

        // Full-strength accent, used for the left spine so the row keeps a
        // legible colour identity even once the bar has almost drained.
        public IBrush Accent { get; }

        // The draining fill. Held well below full strength so the row label
        // stays readable across both the filled and the empty part of the track.
        public IBrush AccentWash { get; }

        // Null when the trigger's icon index falls outside the seven sheets that
        // ship. Timers with no spell of their own are not that case: upstream
        // lends them Feign Death's artwork, so nearly every row resolves to
        // something. Fixed for the life of the trigger, so it is resolved once
        // and never raises a change notification.
        public Bitmap Icon { get; }

        public bool HasIcon => Icon != null;

        public bool IsUrgent
        {
            get => urgent;
            private set
            {
                if (urgent == value)
                    return;

                urgent = value;
                OnPropertyChanged();
            }
        }

        public void Dispose()
        {
            timer.PropertyChanged -= OnTimerPropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnTimerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimerViewModel.Name))
            {
                OnPropertyChanged(nameof(Name));
                return;
            }

            if (e.PropertyName != nameof(TimerViewModel.SecondsLeftPretty) &&
                e.PropertyName != nameof(TimerViewModel.PercentLeft))
                return;

            OnPropertyChanged(nameof(Countdown));
            OnPropertyChanged(nameof(PercentLeft));
            IsUrgent = timer.TotalRemainingDuration <= UrgentThreshold;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static Bitmap ResolveIcon(TimerViewModel timer, SpellIconService iconService)
        {
            if (!timer.HasIcon)
                return null;

            return iconService.GetIcon(timer.Icon.SpellIndex, timer.Rect);
        }

        private static Color ToColor(System.Windows.Media.SolidColorBrush brush, byte alpha)
        {
            if (brush == null)
                return Color.FromArgb(alpha, 0x62, 0xD6, 0xA0);

            var color = brush.Color;

            // A default-constructed shim colour is fully transparent black, which
            // would render as nothing at all. Treat it as "no colour was set".
            if (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0)
                return Color.FromArgb(alpha, 0x62, 0xD6, 0xA0);

            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static IBrush ToAvaloniaBrush(System.Windows.Media.SolidColorBrush brush)
        {
            return new SolidColorBrush(ToColor(brush, 0xFF));
        }

        private static IBrush ToWashBrush(System.Windows.Media.SolidColorBrush brush)
        {
            return new SolidColorBrush(ToColor(brush, 0x4D));
        }
    }
}
