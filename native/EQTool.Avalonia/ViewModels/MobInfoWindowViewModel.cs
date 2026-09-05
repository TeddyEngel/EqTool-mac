using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.ViewModels.MobInfoComponents;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EQTool.Avalonia.ViewModels
{
    // One wiki link: a special, a faction, a quest, or a piece of known loot.
    //
    // Upstream's TestUriViewModel exposes HasUrl as System.Windows.Visibility
    // from the WPF shim, which Avalonia cannot bind to IsVisible. The same
    // condition is re-derived here as a bool rather than trying to translate the
    // shim value.
    public class MobInfoLinkViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly TestUriViewModel source;
        private readonly PricingUriViewModel pricing;

        public MobInfoLinkViewModel(TestUriViewModel source)
        {
            this.source = source;
            pricing = source as PricingUriViewModel;
            source.PropertyChanged += OnSourceChanged;
        }

        public string Name => source.Name;

        public string Url => source.Url;

        public bool HasUrl => !string.IsNullOrWhiteSpace(source.Url);

        public string Price => pricing?.Price;

        public string PriceUrl => pricing?.PriceUrl;

        // Prices arrive on a later con, one item at a time, so a row with no
        // price yet must not reserve a column that may never fill.
        public bool HasPrice => !string.IsNullOrWhiteSpace(pricing?.Price);

        public bool HasPriceUrl => !string.IsNullOrWhiteSpace(pricing?.PriceUrl);

        private void OnSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnSourceChanged(sender, e));
                return;
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(HasUrl));
            OnPropertyChanged(nameof(Price));
            OnPropertyChanged(nameof(PriceUrl));
            OnPropertyChanged(nameof(HasPrice));
            OnPropertyChanged(nameof(HasPriceUrl));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            source.PropertyChanged -= OnSourceChanged;
        }
    }

    public class MobInfoStatViewModel
    {
        public MobInfoStatViewModel(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public string Value { get; }
    }

    public class MobInfoWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly MobInfoViewModel source;

        public MobInfoWindowViewModel() : this(AppServices.Initialize())
        {
        }

        // MobInfoManagementViewModel is deliberately not used. It is a WPF shell
        // whose job is to hand a System.Windows.Controls.UserControl back from a
        // factory, which means nothing here.
        public MobInfoWindowViewModel(AppServices services)
        {
            source = services.Resolve<MobInfoViewModel>();
            source.PropertyChanged += OnSourceChanged;
            Rebuild();
        }

        public ObservableCollection<MobInfoStatViewModel> Stats { get; }
            = new ObservableCollection<MobInfoStatViewModel>();

        public ObservableCollection<MobInfoLinkViewModel> Specials { get; }
            = new ObservableCollection<MobInfoLinkViewModel>();

        public ObservableCollection<MobInfoLinkViewModel> KnownLoot { get; }
            = new ObservableCollection<MobInfoLinkViewModel>();

        public ObservableCollection<MobInfoLinkViewModel> Factions { get; }
            = new ObservableCollection<MobInfoLinkViewModel>();

        public ObservableCollection<MobInfoLinkViewModel> OpposingFactions { get; }
            = new ObservableCollection<MobInfoLinkViewModel>();

        public ObservableCollection<MobInfoLinkViewModel> RelatedQuests { get; }
            = new ObservableCollection<MobInfoLinkViewModel>();

        public string Name => source.Name;

        public string Url => source.Url;

        public string ErrorResults => source.ErrorResults;

        public string SubtitleLine => Describe(source.Level, source.Class, source.Race);

        public bool HasMob => !string.IsNullOrWhiteSpace(source.Name);

        public bool IsEmpty => !HasMob && !HasError;

        public bool HasError => !string.IsNullOrWhiteSpace(source.ErrorResults);

        public bool HasUrl => !string.IsNullOrWhiteSpace(source.Url);

        public bool HasSubtitle => !string.IsNullOrWhiteSpace(SubtitleLine);

        public bool HasStats => Stats.Count > 0;

        public bool HasSpecials => Specials.Count > 0;

        public bool HasKnownLoot => KnownLoot.Count > 0;

        public bool HasFactions => Factions.Count > 0;

        public bool HasOpposingFactions => OpposingFactions.Count > 0;

        public bool HasRelatedQuests => RelatedQuests.Count > 0;

        private static string Describe(string level, string mobClass, string race)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(level))
                parts.Add("Level " + level.Trim());
            if (!string.IsNullOrWhiteSpace(mobClass))
                parts.Add(mobClass.Trim());
            if (!string.IsNullOrWhiteSpace(race))
                parts.Add(race.Trim());
            return string.Join("  ·  ", parts);
        }

        // ConHandler replaces Results wholesale and Parse repopulates every
        // collection behind it, so the whole window is rebuilt rather than
        // diffed. It happens once per con, not on a tick.
        private void OnSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnSourceChanged(sender, e));
                return;
            }

            Rebuild();
        }

        private void Rebuild()
        {
            // Level, class and race are the subtitle, so they are not repeated
            // here.
            Stats.Clear();
            AddStat("HP", source.HP);
            AddStat("AC", source.AC);
            AddStat("HP REGEN", source.HPRegen);
            AddStat("MANA REGEN", source.ManaRegen);
            AddStat("ATTACKS / ROUND", source.AttacksPerRound);
            AddStat("ATTACK SPEED", source.AttackSpeed);
            AddStat("DAMAGE / HIT", source.DamagePerHit);
            AddStat("AGRO RADIUS", source.AgroRadius);
            AddStat("RUN SPEED", source.RunSpeed);

            Fill(Specials, source.Specials);
            Fill(KnownLoot, source.KnownLoot);
            Fill(Factions, source.Factions);
            Fill(OpposingFactions, source.OpposingFactions);
            Fill(RelatedQuests, source.RelatedQuests);

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(ErrorResults));
            OnPropertyChanged(nameof(SubtitleLine));
            OnPropertyChanged(nameof(HasMob));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasUrl));
            OnPropertyChanged(nameof(HasSubtitle));
            OnPropertyChanged(nameof(HasStats));
            OnPropertyChanged(nameof(HasSpecials));
            OnPropertyChanged(nameof(HasKnownLoot));
            OnPropertyChanged(nameof(HasFactions));
            OnPropertyChanged(nameof(HasOpposingFactions));
            OnPropertyChanged(nameof(HasRelatedQuests));
        }

        // A stat the wiki page does not carry is left out entirely. Showing an
        // empty cell for it would fill the window with labels that say nothing.
        private void AddStat(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            Stats.Add(new MobInfoStatViewModel(label, value.Trim()));
        }

        private static void Fill<TSource>(
            ObservableCollection<MobInfoLinkViewModel> target,
            ObservableCollection<TSource> incoming)
            where TSource : TestUriViewModel
        {
            foreach (var row in target)
                row.Dispose();

            target.Clear();

            foreach (var item in incoming)
                target.Add(new MobInfoLinkViewModel(item));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            source.PropertyChanged -= OnSourceChanged;

            DisposeAll(Specials);
            DisposeAll(KnownLoot);
            DisposeAll(Factions);
            DisposeAll(OpposingFactions);
            DisposeAll(RelatedQuests);
        }

        private static void DisposeAll(ObservableCollection<MobInfoLinkViewModel> rows)
        {
            foreach (var row in rows)
                row.Dispose();

            rows.Clear();
        }
    }
}
