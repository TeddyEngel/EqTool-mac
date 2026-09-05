using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Services;
using EQTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace EQTool.Avalonia.ViewModels
{
    public class ConsoleLineViewModel
    {
        public ConsoleLineViewModel(ConsoleLine line)
        {
            Line = line?.Line;
            Foreground = ShimBrushMap.Resolve(line?.Brush);
        }

        public string Line { get; }

        public IBrush Foreground { get; }
    }

    public class ConsoleWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ConsoleViewModel source;
        private readonly DebugOutput debugOutput;
        private readonly bool restoreLogMapping;
        private readonly bool restoreLogSpells;

        public ConsoleWindowViewModel() : this(AppServices.Initialize())
        {
        }

        public ConsoleWindowViewModel(AppServices services)
        {
            source = services.Resolve<ConsoleViewModel>();
            debugOutput = services.Resolve<DebugOutput>();

            // Nothing reaches the console unless one of these is on. Upstream
            // puts them behind two checkboxes in the settings window, which this
            // client does not have, so opening the console is what turns them
            // on and closing it puts them back. Leaving them on for a window
            // nobody is looking at is work done for no reader.
            restoreLogMapping = debugOutput.LogMapping;
            restoreLogSpells = debugOutput.LogSpells;
            debugOutput.LogMapping = true;
            debugOutput.LogSpells = true;

            foreach (var line in source.ConsoleOutput)
                Lines.Add(new ConsoleLineViewModel(line));

            source.ConsoleOutput.CollectionChanged += OnSourceChanged;
        }

        public ObservableCollection<ConsoleLineViewModel> Lines { get; }
            = new ObservableCollection<ConsoleLineViewModel>();

        public bool HasLines => Lines.Count > 0;

        public bool IsEmpty => Lines.Count == 0;

        public bool LogMapping
        {
            get => debugOutput.LogMapping;
            set { debugOutput.LogMapping = value; OnPropertyChanged(); }
        }

        public bool LogSpells
        {
            get => debugOutput.LogSpells;
            set { debugOutput.LogSpells = value; OnPropertyChanged(); }
        }

        public string CountLine => Lines.Count == 1 ? "1 LINE" : Lines.Count.ToString("N0") + " LINES";

        // The 1000-line cap lives on ConsoleViewModel, which trims from the
        // front. Mirroring the change rather than rebuilding keeps the list
        // identical to the source without re-mapping a thousand brushes on every
        // new line.
        private void OnSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnSourceChanged(sender, e));
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                Lines.Clear();
            }
            else
            {
                if (e.OldItems != null)
                {
                    for (var removed = 0; removed < e.OldItems.Count; removed++)
                        Lines.RemoveAt(e.OldStartingIndex);
                }

                if (e.NewItems != null)
                {
                    var insertAt = e.NewStartingIndex;
                    foreach (ConsoleLine line in e.NewItems)
                        Lines.Insert(insertAt++, new ConsoleLineViewModel(line));
                }
            }

            OnPropertyChanged(nameof(HasLines));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CountLine));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            source.ConsoleOutput.CollectionChanged -= OnSourceChanged;
            debugOutput.LogMapping = restoreLogMapping;
            debugOutput.LogSpells = restoreLogSpells;
        }
    }
}
