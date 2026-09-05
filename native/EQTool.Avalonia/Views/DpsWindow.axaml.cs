using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.ViewModels;
using System;

namespace EQTool.Avalonia.Views
{
    public partial class DpsWindow : Window
    {
        private readonly DpsWindowViewModel viewModel;

        public DpsWindow()
        {
            InitializeComponent();

            viewModel = new DpsWindowViewModel();
            DataContext = viewModel;
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
    }
}
