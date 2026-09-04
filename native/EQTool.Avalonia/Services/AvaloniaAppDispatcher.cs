using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using EQTool.Services;

namespace EQTool.Avalonia.Services
{
    // The real dispatcher for the native client.
    //
    // EQTool.Core ships a synchronous no-op `AppDispatcher` in
    // `Compat/EqToolStubs.cs`. That is correct for a headless test run and wrong
    // at runtime: `LogParser`'s 100 ms `System.Timers.Timer` raises `Poll` on a
    // thread-pool thread, and everything it hands to `DispatchUI` ends up
    // mutating `ObservableCollection`s that Avalonia is bound to. Those mutations
    // have to land on the UI thread.
    //
    // Semantics deliberately mirror upstream `EQTool/Services/AppDispatcher.cs`:
    // a blocking `Invoke` rather than a fire-and-forget `Post`, so a batch of log
    // lines is fully applied before the caller continues.
    public class AvaloniaAppDispatcher : IAppDispatcher
    {
        public void DispatchUI(Action action)
        {
            if (action == null)
                return;

            var dispatcher = Dispatcher.UIThread;
            if (dispatcher == null)
                return;

            if (dispatcher.CheckAccess())
            {
                Run(action);
                return;
            }

            try
            {
                dispatcher.Invoke(() => Run(action));
            }
            catch (TaskCanceledException)
            {
                // The dispatcher was shut down while this batch was queued. Nothing
                // left to update; dropping the work is the correct response.
            }
        }

        public void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action)
        {
            DebounceToUI(ref debounceCancellationSource, delay, action, () => false);
        }

        public void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action, Func<bool> shouldCancel)
        {
            debounceCancellationSource?.Cancel();
            debounceCancellationSource = new CancellationTokenSource();
            var debounceToken = debounceCancellationSource.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, debounceToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (debounceToken.IsCancellationRequested || shouldCancel())
                    return;

                DispatchUI(action);
            }, debounceToken);
        }

        // EQTool.Core is compiled with the TEST constant, which strips
        // LogParser.MainRun's own try/catch. Without a net here a single bad log
        // line would take the whole window down. Report it and keep tailing.
        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception dispatchFailure)
            {
                Console.Error.WriteLine("[pigparse] dispatched UI work failed: " + dispatchFailure);
            }
        }
    }
}
