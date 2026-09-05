using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.Controls;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using System;

namespace EQTool.Avalonia.Views
{
    public partial class MapWindow : Window
    {
        private readonly MapWindowViewModel viewModel;

        public MapWindow()
        {
            InitializeComponent();

            viewModel = new MapWindowViewModel();
            DataContext = viewModel;

            WindowPreferences.Attach(this, AppServices.Initialize().Bootstrap.Settings.MapWindowState);

            this.FindControl<Button>("ResetViewButton").Click += OnResetViewClicked;
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

        private void OnResetViewClicked(object sender, RoutedEventArgs e)
        {
            this.FindControl<ZoneMapControl>("MapSurface")?.ResetView();
        }
    }
}
