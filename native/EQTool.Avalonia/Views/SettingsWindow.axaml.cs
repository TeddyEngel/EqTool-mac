using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using EQTool.Avalonia.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EQTool.Avalonia.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsWindowViewModel viewModel;

        public SettingsWindow()
        {
            InitializeComponent();

            viewModel = new SettingsWindowViewModel();
            DataContext = viewModel;

            this.FindControl<Button>("ChooseLogFolderButton").Click += OnChooseLogFolderClicked;
            this.FindControl<Button>("PreviewVoiceButton").Click += (_, _) => viewModel.PreviewVoice();

            var startTab = Environment.GetEnvironmentVariable("PIGPARSE_SETTINGS_TAB");
            if (int.TryParse(startTab, out var tabIndex))
                this.FindControl<TabControl>("SettingsTabs").SelectedIndex = tabIndex;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // An exception escaping an async void handler is unobservable and takes the
        // process with it, so the picker is guarded rather than trusting every
        // storage backend to behave.
        private async void OnChooseLogFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                await ChooseLogFolderAsync();
            }
            catch (Exception pickerFailure)
            {
                Console.Error.WriteLine("[pigparse] log folder picker failed: " + pickerFailure);
            }
        }

        private async Task ChooseLogFolderAsync()
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose your EverQuest Logs folder",
                AllowMultiple = false
            });

            var chosen = folders?.FirstOrDefault();
            var path = chosen?.TryGetLocalPath();

            if (!string.IsNullOrWhiteSpace(path))
                viewModel.EqLogDirectory = path;
        }
    }
}
