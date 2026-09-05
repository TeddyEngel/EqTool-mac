using System;
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
        private readonly System.Action reapply;

        public WindowPreferenceViewModel(string label, WindowState state, System.Action save, System.Action reapply = null)
        {
            Label = label;
            this.state = state;
            this.save = save;
            this.reapply = reapply;
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
                reapply?.Invoke();
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
                reapply?.Invoke();
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
        private readonly Action persist;
        private readonly ITextToSpeach speech;

        public SettingsWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public SettingsWindowViewModel(AppServices services)
            : this(services.Bootstrap.Settings,
                   () => services.Bootstrap.Loader.Save(services.Bootstrap.Settings),
                   services.Resolve<ITextToSpeach>(),
                   new TriggerEditorViewModel(services))
        {
        }

        // Persistence arrives as an action rather than the concrete loader, so the
        // round-trip behaviour can be exercised without writing to disk.
        public SettingsWindowViewModel(
            EQToolSettings settings,
            Action save,
            ITextToSpeach speech,
            TriggerEditorViewModel triggerEditor)
        {
            this.settings = settings;
            this.persist = save ?? (() => { });
            this.speech = speech;

            Voices = MacVoiceCatalog.Available().Select(a => a.Name).ToList();
            TriggerEditor = triggerEditor;

            // The overlay re-applies as an overlay. Without that flag it would be
            // put back to a normal window level and drop behind a Wine fullscreen
            // window, which sits above Avalonia's Topmost.
            WindowPreferences = new List<WindowPreferenceViewModel>
            {
                new WindowPreferenceViewModel("Timers", settings.SpellWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.MainWindow>(settings.SpellWindowState)),
                new WindowPreferenceViewModel("Map", settings.MapWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.MapWindow>(settings.MapWindowState)),
                new WindowPreferenceViewModel("DPS", settings.DpsWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.DpsWindow>(settings.DpsWindowState)),
                new WindowPreferenceViewModel("Mob Info", settings.MobWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.MobInfoWindow>(settings.MobWindowState)),
                new WindowPreferenceViewModel("Console", settings.ConsoleWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.ConsoleWindow>(settings.ConsoleWindowState)),
                new WindowPreferenceViewModel("Overlay", settings.OverlayWindowState, Save,
                    () => WindowManager.ApplyPreferencesTo<Views.EventOverlayWindow>(
                        settings.OverlayWindowState, asOverlay: true))
            };

            RefreshLoggingState();
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
            set
            {
                settings.DefaultEqDirectory = value;
                OnPropertyChanged();
                RefreshLoggingState();
                Save();
            }
        }

        // Whether EverQuest itself is writing a log. The whole client reads that
        // file, so with this off nothing ever appears and there is otherwise no
        // sign of why.
        //
        // TryCheckLoggingEnabled returns null when eqclient.ini cannot be read at
        // all, which is the ordinary case before an install has been located.
        // Only an explicit false is worth warning about; warning on null would
        // fire for everyone who has not set the directory yet.
        public bool EqLoggingIsOff { get; private set; }

        public void RefreshLoggingState()
        {
            var directory = settings.DefaultEqDirectory;
            EqLoggingIsOff = !string.IsNullOrWhiteSpace(directory)
                && FindEq.TryCheckLoggingEnabled(directory) == false;

            OnPropertyChanged(nameof(EqLoggingIsOff));
        }

        public int FontSize
        {
            get => settings.FontSize ?? 12;
            set
            {
                settings.FontSize = value;
                OnPropertyChanged();
                Save();
                TypeScale.Apply(value);
            }
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

        // The model floors this at 1, so the property reads back whatever was
        // stored rather than what was asked for.
        public int LogArchiveSizeMB
        {
            get => settings.LogArchiveSizeMB;
            set
            {
                settings.LogArchiveSizeMB = value;
                OnPropertyChanged();
                Save();
            }
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
            set
            {
                settings.OverlayClickThrough = value;
                OnPropertyChanged();
                Save();
                WindowManager.ApplyOverlayClickThrough(value);
            }
        }

        // Speaking a sample is the only way to tell whether a voice is the one you
        // want; the names give no clue.
        public void PreviewVoice()
        {
            speech.Say(VoiceSampleText);
        }

        private void Save()
        {
            persist();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
