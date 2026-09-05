using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using System;

namespace EQTool.Avalonia.Views
{
    public partial class EventOverlayWindow : Window
    {
        private readonly EventOverlayViewModel viewModel;

        public EventOverlayWindow()
        {
            InitializeComponent();

            viewModel = new EventOverlayViewModel();
            DataContext = viewModel;

            var settings = AppServices.Initialize().Bootstrap.Settings;
            WindowPreferences.Attach(this, settings.OverlayWindowState, asOverlay: true);

            var handle = this.FindControl<Border>("DragHandle");
            if (handle != null)
                handle.PointerPressed += OnDragHandlePressed;

            Opened += OnOverlayOpened;
        }

        // setIgnoresMouseEvents applies to the whole NSWindow, so turning it on
        // also makes the drag handle unclickable. That is the intended trade: with
        // click-through on the overlay is purely visual and is repositioned by
        // turning the setting off again.
        private void OnOverlayOpened(object sender, EventArgs e)
        {
            var settings = AppServices.Initialize().Bootstrap.Settings;
            WindowPreferences.SetClickThrough(this, settings.OverlayClickThrough);
        }

        private void OnDragHandlePressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            BeginMoveDrag(e);
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
