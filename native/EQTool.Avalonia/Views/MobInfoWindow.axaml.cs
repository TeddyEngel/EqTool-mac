using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using System;

namespace EQTool.Avalonia.Views
{
    public partial class MobInfoWindow : Window
    {
        private readonly MobInfoWindowViewModel viewModel;

        public MobInfoWindow()
        {
            InitializeComponent();

            viewModel = new MobInfoWindowViewModel();
            DataContext = viewModel;

            WindowPreferences.Attach(this, AppServices.Initialize().Bootstrap.Settings.MobWindowState);

            this.FindControl<Button>("OpenWikiButton").Click += OnWikiClicked;
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

        private void OnWikiClicked(object sender, RoutedEventArgs e)
        {
            Open(viewModel.Url);
        }

        // Both the item name and its price link through here, so the button
        // carries its own destination in Tag rather than the handler guessing
        // which property of the row was clicked.
        private void OnLinkClicked(object sender, RoutedEventArgs e)
        {
            Open((sender as Button)?.Tag as string);
        }

        private static void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            WindowManager.OpenUrl(url);
        }
    }
}
