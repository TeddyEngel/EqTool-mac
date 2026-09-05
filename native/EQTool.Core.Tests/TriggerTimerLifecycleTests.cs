using Autofac;
using EQTool.Models;
using EQTool.Services;
using EQTool.ViewModels;
using EQtoolsTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EQTool.Core.Tests
{
    // TriggerTimerManager keeps its running timers in a list it prunes from Tick,
    // and Tick comes from the DispatcherTimer shim, which does nothing until a
    // host is installed. That was a real fault here: with no host the list never
    // shrank, so a second match on the same timer found a stale entry, took the
    // restart branch, and updated a view model that had already left the spell
    // list. The row never came back.
    //
    // It was fixed by installing a host, and never covered by a test. Nothing in
    // either suite mentions TriggerTimerManager; upstream's trigger tests cover
    // loading, folders and defaults rather than the timer lifecycle.
    //
    // The host is an interface with a settable static behind it, so ticks can be
    // driven here rather than waited for.
    [TestClass]
    public class TriggerTimerLifecycleTests : BaseTestClass
    {
        private sealed class CapturingTimerHost : System.Windows.Threading.IDispatcherTimerHost
        {
            private sealed class Subscription : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public List<Action> Ticks { get; } = new List<Action>();

            public IDisposable Schedule(TimeSpan interval, Action onTick)
            {
                Ticks.Add(onTick);
                return new Subscription();
            }

            public void FireAll()
            {
                foreach (var tick in Ticks.ToList())
                    tick();
            }
        }

        private System.Windows.Threading.IDispatcherTimerHost previousHost;
        private CapturingTimerHost host;

        [TestInitialize]
        public void Setup()
        {
            previousHost = System.Windows.Threading.DispatcherTimer.Host;
            host = new CapturingTimerHost();
            System.Windows.Threading.DispatcherTimer.Host = host;
        }

        [TestCleanup]
        public void Cleanup()
        {
            System.Windows.Threading.DispatcherTimer.Host = previousHost;
        }

        private static Trigger TimerTrigger(string name, int seconds)
        {
            return new Trigger
            {
                TriggerName = name,
                Timer = new TriggerTimer
                {
                    TimerName = name,
                    Seconds = seconds,

                    // The branch that broke. StartNewTimer adds a row whether or
                    // not the old one was pruned, so it cannot see this fault.
                    RestartBehavior = TimerRestartBehavior.RestartTimer,
                },
            };
        }

        private static int RowsNamed(SpellWindowViewModel spellWindow, string name)
        {
            return spellWindow.SpellList.Count(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void HandleTimerMatch_AddsARowForTheTimer()
        {
            // Arrange
            var manager = container.Resolve<TriggerTimerManager>();
            var spellWindow = container.Resolve<SpellWindowViewModel>();
            var trigger = TimerTrigger("Dragon Roar", 1);

            // Act
            manager.HandleTimerMatch(trigger);

            // Assert
            Assert.AreEqual(1, RowsNamed(spellWindow, "Dragon Roar"));
        }

        [TestMethod]
        public void AfterExpiryAndATick_TheSameTriggerStartsAFreshTimer()
        {
            // Arrange
            // The fault this covers: without a tick the expired entry stays in the
            // manager's list, the second match finds it, and the restart branch
            // updates a view model that is no longer on screen.
            var manager = container.Resolve<TriggerTimerManager>();
            var spellWindow = container.Resolve<SpellWindowViewModel>();
            var trigger = TimerTrigger("Enrage", 1);

            manager.HandleTimerMatch(trigger);
            Assert.AreEqual(1, RowsNamed(spellWindow, "Enrage"), "The first match did not add a row.");

            // Act
            // Past the one second duration, then drive the tick the manager is
            // waiting on rather than waiting for a real one.
            Thread.Sleep(1200);
            host.FireAll();

            manager.HandleTimerMatch(trigger);

            // Assert
            Assert.AreEqual(
                2,
                RowsNamed(spellWindow, "Enrage"),
                "The second match did not start a fresh timer, so the expired one was never pruned.");
        }

        [TestMethod]
        public void TheManagerSubscribesToATick()
        {
            // Arrange
            // Resolving the manager is what starts its ticker. If nothing is
            // scheduled, the host was never asked and nothing would ever prune.

            // Act
            _ = container.Resolve<TriggerTimerManager>();

            // Assert
            Assert.IsTrue(host.Ticks.Any(), "The manager never scheduled a tick, so its list would never be pruned.");
        }

        [TestMethod]
        public void ATickBeforeExpiry_LeavesTheTimerAlone()
        {
            // Arrange
            // Pruning early would be as wrong as never pruning: the row would
            // vanish while the timer was still counting down.
            var manager = container.Resolve<TriggerTimerManager>();
            var spellWindow = container.Resolve<SpellWindowViewModel>();
            var trigger = TimerTrigger("Ring War", 30);

            manager.HandleTimerMatch(trigger);

            // Act
            host.FireAll();
            manager.HandleTimerMatch(trigger);

            // Assert
            // Still one row: the second match restarted the live timer rather than
            // adding a second one.
            Assert.AreEqual(1, RowsNamed(spellWindow, "Ring War"));
        }
    }
}
