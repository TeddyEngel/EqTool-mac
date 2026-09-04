using System;
using Avalonia.Threading;

namespace EQTool.Avalonia.Services
{
    // Gives EQTool.Core's inert `System.Windows.Threading.DispatcherTimer` shim a
    // real clock, on the UI thread, matching WPF's semantics.
    //
    // `TriggerTimerManager` builds one of these shims in its constructor and
    // prunes its list of running timers from the Tick. Without a host installed
    // that list never empties, and the second time a trigger fires the manager
    // restarts a viewmodel the spell list has already dropped - so the row
    // silently never reappears.
    public class AvaloniaDispatcherTimerHost : global::System.Windows.Threading.IDispatcherTimerHost
    {
        public static void Install()
        {
            global::System.Windows.Threading.DispatcherTimer.Host = new AvaloniaDispatcherTimerHost();
        }

        public IDisposable Schedule(TimeSpan interval, Action onTick)
        {
            if (onTick == null)
                return null;

            var timer = new DispatcherTimer { Interval = interval };
            timer.Tick += (sender, args) => onTick();
            timer.Start();
            return new TimerSubscription(timer);
        }

        private class TimerSubscription : IDisposable
        {
            private readonly DispatcherTimer timer;

            public TimerSubscription(DispatcherTimer timer)
            {
                this.timer = timer;
            }

            public void Dispose()
            {
                timer.Stop();
            }
        }
    }
}
