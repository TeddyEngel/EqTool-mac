using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.Services;
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
            this.FindControl<Button>("OpenMapButton").Click += (_, _) => WindowManager.ShowMap();
            this.FindControl<Button>("OpenDpsButton").Click += (_, _) => WindowManager.ShowDps();
            this.FindControl<Button>("OpenConsoleButton").Click += (_, _) => WindowManager.ShowConsole();
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
