using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.ViewModels;

namespace EQTool.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            this.FindControl<Button>("ChooseLogFolderButton").Click += OnChooseLogFolderClicked;
            this.FindControl<Button>("OpenMapButton").Click += (_, _) => ShowSingleInstance(() => new MapWindow());
            this.FindControl<Button>("OpenDpsButton").Click += (_, _) => ShowSingleInstance(() => new DpsWindow());
        }

        private readonly Dictionary<Type, Window> openWindows = new Dictionary<Type, Window>();

        // Each secondary window subscribes to log events and owns a timer, so a
        // second instance would double every update rather than just look untidy.
        private void ShowSingleInstance<TWindow>(Func<TWindow> create) where TWindow : Window
        {
            if (openWindows.TryGetValue(typeof(TWindow), out var existing))
            {
                existing.Activate();
                return;
            }

            var window = create();
            openWindows[typeof(TWindow)] = window;
            window.Closed += (_, _) => openWindows.Remove(typeof(TWindow));
            window.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.Dispose();
            base.OnClosed(e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // An exception escaping an async void event handler is unobservable and
        // takes the process with it, so the picker is guarded here rather than
        // trusting every platform backend to behave.
        private async void OnChooseLogFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                await viewModel.ChooseLogFolderAsync(StorageProvider);
            }
            catch (Exception pickerFailure)
            {
                Console.Error.WriteLine("[pigparse] log folder picker failed: " + pickerFailure);
            }
        }
    }
}
