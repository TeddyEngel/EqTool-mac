using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Autofac;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Services;
using EQTool.ViewModels;
using EQTool.ViewModels.SpellWindow;

namespace EQTool.Avalonia.ViewModels
{
    // Owns the running parser and turns upstream's spell list into the rows the
    // window draws.
    //
    // Nothing here polls the log file. `LogParser` starts its own 100 ms timer
    // in its constructor and pushes parsed lines through `IAppDispatcher`, so
    // resolving it is all it takes to start tailing. The ticker below only does
    // what upstream's `UIRunner` does: run the countdowns down once per frame
    // and drop the ones that have expired.
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        // Upstream's UIRunner ticks once a second. A shorter interval costs
        // nothing here and makes the progress bars drain smoothly instead of
        // stepping.
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

        private static readonly SpellViewModelType[] CountdownTypes =
        {
            SpellViewModelType.Timer,
            SpellViewModelType.Spell,
            SpellViewModelType.Roll
        };

        private readonly SettingsBootstrapResult bootstrap;
        private readonly Autofac.IContainer container;
        private readonly SpellWindowViewModel spellWindow;
        private readonly ActivePlayer activePlayer;
        private readonly LogParser logParser;
        private readonly SpellIconService iconService = new SpellIconService();
        private readonly DispatcherTimer ticker;
        private readonly Dictionary<BaseTriggerViewModel, TimerRowViewModel> rowsBySource
            = new Dictionary<BaseTriggerViewModel, TimerRowViewModel>();

        private DateTime? lastTick;
        private bool hasCharacter;
        private string characterLine;
        private string sourceLine;
        private string noticeLine;

        public MainWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public MainWindowViewModel(AppServices services)
        {
            bootstrap = services.Bootstrap;
            container = services.Container;

            spellWindow = container.Resolve<SpellWindowViewModel>();
            activePlayer = container.Resolve<ActivePlayer>();
            logParser = container.Resolve<LogParser>();

            ((INotifyCollectionChanged)spellWindow.SpellList).CollectionChanged += OnSpellListChanged;
            SeedExistingRows();
            RefreshStatus();

            ticker = new DispatcherTimer { Interval = TickInterval };
            ticker.Tick += OnTick;
            ticker.Start();
        }

        public ObservableCollection<TimerRowViewModel> Rows { get; }
            = new ObservableCollection<TimerRowViewModel>();

        public bool HasRows => Rows.Count > 0;

        public bool IsEmpty => Rows.Count == 0;

        public string CharacterLine
        {
            get => characterLine;
            private set => Set(ref characterLine, value);
        }

        public bool HasCharacter
        {
            get => hasCharacter;
            private set => Set(ref hasCharacter, value);
        }

        public string SourceLine
        {
            get => sourceLine;
            private set => Set(ref sourceLine, value);
        }

        // Plain-language explanation of why nothing is going to happen, shown
        // when the log directory is missing or could not be resolved.
        public string NoticeLine
        {
            get => noticeLine;
            private set
            {
                if (!Set(ref noticeLine, value))
                    return;

                OnPropertyChanged(nameof(HasNotice));
            }
        }

        public bool HasNotice => !string.IsNullOrEmpty(noticeLine);

        public async Task ChooseLogFolderAsync(IStorageProvider storageProvider)
        {
            if (storageProvider == null)
                return;

            var picked = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose your EverQuest Logs folder",
                AllowMultiple = false
            }).ConfigureAwait(true);

            var folder = picked?.FirstOrDefault();
            var path = folder?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            bootstrap.Settings.EqLogDirectory = path;
            bootstrap.LogDirectoryResolved = true;
            bootstrap.Loader.Save(bootstrap.Settings);
            RefreshStatus();
        }

        public void Dispose()
        {
            ticker.Stop();
            ticker.Tick -= OnTick;
            ((INotifyCollectionChanged)spellWindow.SpellList).CollectionChanged -= OnSpellListChanged;

            foreach (var row in Rows)
                row.Dispose();

            Rows.Clear();
            rowsBySource.Clear();

            logParser.Dispose();
            container.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var elapsedMilliseconds = lastTick.HasValue ? (now - lastTick.Value).TotalMilliseconds : 0.0;
            lastTick = now;

            spellWindow.UpdateSpells(elapsedMilliseconds);
            RefreshStatus();
        }

        private void SeedExistingRows()
        {
            foreach (var item in spellWindow.SpellList.ToList())
                TryAddRow(item);

            RaiseEmptyState();
        }

        private void OnSpellListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var row in Rows)
                    row.Dispose();

                Rows.Clear();
                rowsBySource.Clear();
                SeedExistingRows();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (BaseTriggerViewModel item in e.OldItems)
                    RemoveRow(item);
            }

            if (e.NewItems != null)
            {
                foreach (BaseTriggerViewModel item in e.NewItems)
                    TryAddRow(item);
            }

            RaiseEmptyState();
        }

        // Boats and counters live in the same collection but are not countdowns
        // the user started: boats are always present and cycle forever, counters
        // have no duration. Neither belongs in a "what is running right now"
        // list, so this milestone shows only the three types that tick down and
        // then disappear.
        private void TryAddRow(BaseTriggerViewModel item)
        {
            if (!(item is TimerViewModel timer))
                return;

            if (!CountdownTypes.Contains(item.SpellViewModelType))
                return;

            if (rowsBySource.ContainsKey(item))
                return;

            var row = new TimerRowViewModel(timer, iconService);
            rowsBySource[item] = row;
            Rows.Add(row);
        }

        private void RemoveRow(BaseTriggerViewModel item)
        {
            if (!rowsBySource.TryGetValue(item, out var row))
                return;

            _ = rowsBySource.Remove(item);
            _ = Rows.Remove(row);
            row.Dispose();
        }

        private void RaiseEmptyState()
        {
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void RefreshStatus()
        {
            var player = activePlayer.Player;
            HasCharacter = player != null;
            CharacterLine = player == null
                ? "No character detected yet"
                : player.Name + "  ·  " + player.Server;

            var logFile = activePlayer.LogFileName;
            SourceLine = string.IsNullOrWhiteSpace(logFile)
                ? bootstrap.Settings.EqLogDirectory ?? "No log folder chosen"
                : Path.GetFileName(logFile);

            NoticeLine = BuildNotice();
        }

        private string BuildNotice()
        {
            if (!bootstrap.LogDirectoryResolved)
                return "Your saved log folder was written for Windows and this Mac cannot find it. Choose your EverQuest Logs folder to carry on.";

            if (string.IsNullOrWhiteSpace(bootstrap.Settings.EqLogDirectory))
                return "No EverQuest Logs folder has been chosen yet.";

            if (!Directory.Exists(bootstrap.Settings.EqLogDirectory))
                return "The saved log folder is no longer there. Choose your EverQuest Logs folder again.";

            return null;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
