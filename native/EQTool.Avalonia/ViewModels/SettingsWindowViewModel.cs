using EQTool.Avalonia.Services;
using EQTool.Core.Platform;
using EQTool.Models;
using EQTool.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EQTool.Avalonia.ViewModels
{
    // A window's always-on-top and opacity, surfaced as one row.
    public class WindowPreferenceViewModel : INotifyPropertyChanged
    {
        private readonly WindowState state;
        private readonly System.Action save;

        public WindowPreferenceViewModel(string label, WindowState state, System.Action save)
        {
            Label = label;
            this.state = state;
            this.save = save;
        }

        public string Label { get; }

        public bool AlwaysOnTop
        {
            get => state.AlwaysOnTop;
            set
            {
                if (state.AlwaysOnTop == value)
                    return;

                state.AlwaysOnTop = value;
                OnPropertyChanged();
                save();
            }
        }

        public double Opacity
        {
            get => state.Opacity ?? 1.0;
            set
            {
                if (System.Math.Abs((state.Opacity ?? 1.0) - value) < 0.001)
                    return;

                state.Opacity = value;
                OnPropertyChanged();
                save();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SettingsWindowViewModel : INotifyPropertyChanged
    {
        private const string VoiceSampleText = "Dragon Roar incoming";

        private readonly EQToolSettings settings;
        private readonly EQToolSettingsLoad loader;
        private readonly ITextToSpeach speech;

        public SettingsWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public SettingsWindowViewModel(AppServices services)
        {
            settings = services.Bootstrap.Settings;
            loader = services.Bootstrap.Loader;
            speech = services.Resolve<ITextToSpeach>();

            Voices = MacVoiceCatalog.Available().Select(a => a.Name).ToList();
            TriggerEditor = new TriggerEditorViewModel(services);

            WindowPreferences = new List<WindowPreferenceViewModel>
            {
                new WindowPreferenceViewModel("Timers", settings.SpellWindowState, Save),
                new WindowPreferenceViewModel("Map", settings.MapWindowState, Save),
                new WindowPreferenceViewModel("DPS", settings.DpsWindowState, Save),
                new WindowPreferenceViewModel("Mob Info", settings.MobWindowState, Save),
                new WindowPreferenceViewModel("Console", settings.ConsoleWindowState, Save),
                new WindowPreferenceViewModel("Overlay", settings.OverlayWindowState, Save)
            };
        }

        public TriggerEditorViewModel TriggerEditor { get; }

        public IReadOnlyList<string> Voices { get; }

        public IReadOnlyList<WindowPreferenceViewModel> WindowPreferences { get; }

        public string EqLogDirectory
        {
            get => settings.EqLogDirectory;
            set { settings.EqLogDirectory = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEqLogDirectory)); Save(); }
        }

        public bool HasEqLogDirectory => !string.IsNullOrWhiteSpace(settings.EqLogDirectory);

        public string EqDirectory
        {
            get => settings.DefaultEqDirectory;
            set { settings.DefaultEqDirectory = value; OnPropertyChanged(); Save(); }
        }

        public int FontSize
        {
            get => settings.FontSize ?? 12;
            set { settings.FontSize = value; OnPropertyChanged(); Save(); }
        }

        public int AudioVolume
        {
            get => settings.GlobalAudioVolume ?? 100;
            set { settings.GlobalAudioVolume = value; OnPropertyChanged(); Save(); }
        }

        public string SelectedVoice
        {
            get => settings.SelectedVoice;
            set { settings.SelectedVoice = value; OnPropertyChanged(); Save(); }
        }

        public bool LogArchiveEnabled
        {
            get => settings.LogArchiveEnabled;
            set { settings.LogArchiveEnabled = value; OnPropertyChanged(); Save(); }
        }

        public bool YouOnlySpells
        {
            get => settings.YouOnlySpells;
            set { settings.YouOnlySpells = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowRandomRolls
        {
            get => settings.ShowRandomRolls;
            set { settings.ShowRandomRolls = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowRing8RollTime
        {
            get => settings.ShowRing8RollTime ?? true;
            set { settings.ShowRing8RollTime = value; OnPropertyChanged(); Save(); }
        }

        public bool ShowScoutRollTime
        {
            get => settings.ShowScoutRollTime ?? true;
            set { settings.ShowScoutRollTime = value; OnPropertyChanged(); Save(); }
        }

        public bool OverlayClickThrough
        {
            get => settings.OverlayClickThrough;
            set { settings.OverlayClickThrough = value; OnPropertyChanged(); Save(); }
        }

        // Speaking a sample is the only way to tell whether a voice is the one you
        // want; the names give no clue.
        public void PreviewVoice()
        {
            speech.Say(VoiceSampleText);
        }

        private void Save()
        {
            loader.Save(settings);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
