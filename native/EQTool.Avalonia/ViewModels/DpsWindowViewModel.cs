using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Models;
using EQTool.Services;
using EQTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EQTool.Avalonia.ViewModels
{
    public class DpsRowViewModel
    {
        public DpsRowViewModel(EntittyDPS entity, int highestTargetDamage)
        {
            SourceName = entity.SourceName;
            TotalDamage = entity.TotalDamage;
            CurrentDps = entity.DPS;
            OverallDps = entity.TotalDPS;
            HighestHit = entity.HighestHit;
            IsNpc = entity.isSourceNpc;

            // Bar width is relative to the biggest contributor on this target, so a
            // fight against a trash mob still fills the bar rather than reading as
            // empty next to a raid boss.
            ShareOfTarget = highestTargetDamage > 0
                ? Math.Clamp(entity.TotalDamage * 100.0 / highestTargetDamage, 0, 100)
                : 0;
        }

        public string SourceName { get; }

        public int TotalDamage { get; }

        public int CurrentDps { get; }

        public int OverallDps { get; }

        public int HighestHit { get; }

        public bool IsNpc { get; }

        public double ShareOfTarget { get; }

        public string TotalDamageText => TotalDamage.ToString("N0");

        public string CurrentDpsText => CurrentDps.ToString("N0");

        public string DetailText => "dps " + OverallDps.ToString("N0") + " · max " + HighestHit.ToString("N0");
    }

    public class DpsTargetGroupViewModel
    {
        public DpsTargetGroupViewModel(string targetName, ObservableCollection<DpsRowViewModel> rows, int totalDamage)
        {
            TargetName = targetName;
            Rows = rows;
            TotalDamage = totalDamage;
        }

        public string TargetName { get; }

        public ObservableCollection<DpsRowViewModel> Rows { get; }

        public int TotalDamage { get; }

        public string TotalDamageText => TotalDamage.ToString("N0") + " total";
    }

    public class DpsWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

        private readonly DPSWindowViewModel source;
        private readonly LogEvents logEvents;
        private readonly ActivePlayer activePlayer;
        private readonly DispatcherTimer ticker;

        public DpsWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public DpsWindowViewModel(AppServices services)
        {
            source = services.Resolve<DPSWindowViewModel>();
            logEvents = services.Resolve<LogEvents>();
            activePlayer = services.Resolve<ActivePlayer>();

            source.EntityList = new ObservableCollection<EntittyDPS>();

            logEvents.DamageEvent += OnDamage;
            logEvents.SlainEvent += OnSlain;

            ticker = new DispatcherTimer { Interval = TickInterval };
            ticker.Tick += OnTick;
            ticker.Start();

            Rebuild();
        }

        public ObservableCollection<DpsTargetGroupViewModel> Groups { get; }
            = new ObservableCollection<DpsTargetGroupViewModel>();

        public bool HasGroups => Groups.Count > 0;

        public bool IsEmpty => Groups.Count == 0;

        public string CharacterLine => activePlayer?.Player?.Name ?? "No character detected yet";

        private void OnDamage(object sender, DamageEvent e)
        {
            Dispatcher.UIThread.Post(() => source.TryAdd(e));
        }

        private void OnSlain(object sender, SlainEvent e)
        {
            Dispatcher.UIThread.Post(() => source.TargetDied(e?.Victim));
        }

        private void OnTick(object sender, EventArgs e)
        {
            source.UpdateDPS();
            Rebuild();
        }

        // Avalonia has no live-grouping collection view, so the grouping WPF got
        // from ListCollectionView is rebuilt here each tick: group by target,
        // order targets by most recent damage, order rows by damage descending.
        private void Rebuild()
        {
            var grouped = source.EntityList
                .Where(a => !string.IsNullOrWhiteSpace(a.TargetName))
                .GroupBy(a => a.TargetName)
                .Select(group =>
                {
                    var ordered = group.OrderByDescending(a => a.TotalDamage).ToList();
                    var highest = ordered.Count > 0 ? ordered[0].TotalDamage : 0;
                    var rows = new ObservableCollection<DpsRowViewModel>(
                        ordered.Select(a => new DpsRowViewModel(a, highest)));

                    return new
                    {
                        Group = new DpsTargetGroupViewModel(group.Key, rows, ordered.Sum(a => a.TotalDamage)),
                        LastDamage = group.Max(a => a.LastDamageDone ?? a.StartTime)
                    };
                })
                .OrderByDescending(a => a.LastDamage)
                .Select(a => a.Group)
                .ToList();

            Groups.Clear();
            foreach (var group in grouped)
                Groups.Add(group);

            OnPropertyChanged(nameof(HasGroups));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CharacterLine));
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
            logEvents.DamageEvent -= OnDamage;
            logEvents.SlainEvent -= OnSlain;
        }
    }
}
