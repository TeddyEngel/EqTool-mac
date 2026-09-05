using EQTool.Avalonia.Services;
using EQTool.Models;
using EQTool.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EQTool.Avalonia.ViewModels
{
    // One row in the trigger list.
    public class TriggerRowViewModel : INotifyPropertyChanged
    {
        private readonly Trigger trigger;
        private readonly Action save;

        public TriggerRowViewModel(Trigger trigger, Action save)
        {
            this.trigger = trigger;
            this.save = save;
        }

        public Trigger Source => trigger;

        public string Name => string.IsNullOrWhiteSpace(trigger.TriggerName) ? "(unnamed)" : trigger.TriggerName;

        public string Category => string.IsNullOrWhiteSpace(trigger.Category) ? "Default" : trigger.Category;

        public bool IsBuiltIn => trigger.IsBuiltIn;

        // Built-in triggers that the user has edited are worth marking: they stop
        // receiving upstream's fixes for that trigger.
        public string Badge => trigger.IsBuiltIn ? (trigger.Customized ? "edited" : "built in") : "custom";

        public bool Enabled
        {
            get => trigger.TriggerEnabled;
            set
            {
                if (trigger.TriggerEnabled == value)
                    return;

                trigger.TriggerEnabled = value;
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

    public class TriggerEditorViewModel : INotifyPropertyChanged
    {
        private readonly EQToolSettings settings;
        private readonly Action save;
        private readonly ITextToSpeach speech;
        private readonly IAudioService audio;

        private TriggerRowViewModel selected;
        private string filterText;

        public TriggerEditorViewModel() : this(AppServices.Initialize())
        {
        }

        public TriggerEditorViewModel(AppServices services)
            : this(services.Bootstrap.Settings,
                   () => services.Bootstrap.Loader.Save(services.Bootstrap.Settings),
                   services.Resolve<ITextToSpeach>(),
                   services.Resolve<IAudioService>())
        {
        }

        // Persistence arrives as an action rather than the concrete loader: what
        // this needs is "a way to save", and expressing it that way lets the
        // editing rules be exercised without writing to disk.
        public TriggerEditorViewModel(
            EQToolSettings settings,
            Action save,
            ITextToSpeach speech,
            IAudioService audio)
        {
            this.settings = settings;
            this.save = save ?? (() => { });
            this.speech = speech;
            this.audio = audio;

            AudioTypes = Enum.GetValues(typeof(TriggerAudioType)).Cast<TriggerAudioType>().ToList();
            TimerTypes = Enum.GetValues(typeof(TimerType)).Cast<TimerType>().ToList();

            Rebuild();
        }

        public ObservableCollection<TriggerRowViewModel> Triggers { get; }
            = new ObservableCollection<TriggerRowViewModel>();

        public IReadOnlyList<TriggerAudioType> AudioTypes { get; }

        public IReadOnlyList<TimerType> TimerTypes { get; }

        public string FilterText
        {
            get => filterText;
            set { filterText = value; OnPropertyChanged(); Rebuild(); }
        }

        public string TriggerCountText => Triggers.Count + " of " + settings.Triggers.Count + " shown";

        public TriggerRowViewModel Selected
        {
            get => selected;
            set
            {
                selected = value;
                OnPropertyChanged();
                RaiseDetailChanged();
            }
        }

        public bool HasSelection => selected != null;

        private Trigger Current => selected?.Source;

        private TriggerOutput CurrentOutput
        {
            get
            {
                if (Current == null)
                    return null;

                if (Current.Basic == null)
                    Current.Basic = new TriggerOutput();

                return Current.Basic;
            }
        }

        private TriggerTimer CurrentTimer
        {
            get
            {
                if (Current == null)
                    return null;

                if (Current.Timer == null)
                    Current.Timer = new TriggerTimer();

                return Current.Timer;
            }
        }

        public string TriggerName
        {
            get => Current?.TriggerName;
            set { if (Current == null) return; Current.TriggerName = value; MarkCustomised(); }
        }

        public string SearchText
        {
            get => Current?.SearchText;
            set { if (Current == null) return; Current.SearchText = value; MarkCustomised(); }
        }

        public bool UseRegex
        {
            get => Current?.EffectiveUseRegex ?? true;
            set { if (Current == null) return; Current.UseRegex = value; MarkCustomised(); }
        }

        public bool DisplayTextEnabled
        {
            get => CurrentOutput?.DisplayTextEnabled ?? false;
            set { if (CurrentOutput == null) return; CurrentOutput.DisplayTextEnabled = value; MarkCustomised(); }
        }

        public string DisplayText
        {
            get => CurrentOutput?.DisplayText;
            set { if (CurrentOutput == null) return; CurrentOutput.DisplayText = value; MarkCustomised(); }
        }

        public TriggerAudioType AudioType
        {
            get => CurrentOutput?.AudioType ?? TriggerAudioType.None;
            set
            {
                if (CurrentOutput == null)
                    return;

                CurrentOutput.AudioType = value;
                MarkCustomised();
                OnPropertyChanged(nameof(IsTextToSpeech));
                OnPropertyChanged(nameof(IsSoundFile));
            }
        }

        public bool IsTextToSpeech => AudioType == TriggerAudioType.TextToSpeech;

        public bool IsSoundFile => AudioType == TriggerAudioType.SoundFile;

        public string TtsText
        {
            get => CurrentOutput?.TtsText;
            set { if (CurrentOutput == null) return; CurrentOutput.TtsText = value; MarkCustomised(); }
        }

        public string SoundFile
        {
            get => CurrentOutput?.SoundFile;
            set { if (CurrentOutput == null) return; CurrentOutput.SoundFile = value; MarkCustomised(); }
        }

        public TimerType TimerType
        {
            get => CurrentTimer?.TimerType ?? TimerType.NoTimer;
            set
            {
                if (CurrentTimer == null)
                    return;

                CurrentTimer.TimerType = value;
                MarkCustomised();
                OnPropertyChanged(nameof(HasTimer));
            }
        }

        public bool HasTimer => TimerType != TimerType.NoTimer;

        public string TimerName
        {
            get => CurrentTimer?.TimerName;
            set { if (CurrentTimer == null) return; CurrentTimer.TimerName = value; MarkCustomised(); }
        }

        public int TimerMinutes
        {
            get => CurrentTimer?.Minutes ?? 0;
            set { if (CurrentTimer == null) return; CurrentTimer.Minutes = value; MarkCustomised(); }
        }

        public int TimerSeconds
        {
            get => CurrentTimer?.Seconds ?? 0;
            set { if (CurrentTimer == null) return; CurrentTimer.Seconds = value; MarkCustomised(); }
        }

        // Previewing is the only way to tell whether an alert reads well out loud
        // or whether a sound file is the one you meant.
        public void PreviewOutput()
        {
            var output = CurrentOutput;
            if (output == null)
                return;

            if (output.AudioType == TriggerAudioType.TextToSpeech && !string.IsNullOrWhiteSpace(output.TtsText))
                speech.Say(output.TtsText);
            else if (output.AudioType == TriggerAudioType.SoundFile && !string.IsNullOrWhiteSpace(output.SoundFile))
                audio.Play(output.SoundFile);
        }

        // Editing a built-in stops it tracking upstream's future fixes, which is
        // why upstream records the same flag.
        private void MarkCustomised()
        {
            if (Current != null && Current.IsBuiltIn)
                Current.Customized = true;

            save();
            RaiseDetailChanged();
        }

        private void Rebuild()
        {
            var filter = filterText?.Trim();

            var matching = settings.Triggers
                .Where(a => string.IsNullOrWhiteSpace(filter)
                    || (a.TriggerName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.SearchText ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(a => a.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.TriggerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Triggers.Clear();
            foreach (var trigger in matching)
                Triggers.Add(new TriggerRowViewModel(trigger, save));

            OnPropertyChanged(nameof(TriggerCountText));
        }

        private void RaiseDetailChanged()
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(TriggerName));
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(UseRegex));
            OnPropertyChanged(nameof(DisplayTextEnabled));
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(AudioType));
            OnPropertyChanged(nameof(IsTextToSpeech));
            OnPropertyChanged(nameof(IsSoundFile));
            OnPropertyChanged(nameof(TtsText));
            OnPropertyChanged(nameof(SoundFile));
            OnPropertyChanged(nameof(TimerType));
            OnPropertyChanged(nameof(HasTimer));
            OnPropertyChanged(nameof(TimerName));
            OnPropertyChanged(nameof(TimerMinutes));
            OnPropertyChanged(nameof(TimerSeconds));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
