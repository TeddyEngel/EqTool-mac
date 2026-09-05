using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using EQTool.Avalonia.Services;
using EQTool.Models;
using EQTool.Avalonia.ViewModels;
using System;

namespace EQTool.Avalonia.Views
{
    public partial class EventOverlayWindow : Window
    {
        private readonly EventOverlayViewModel viewModel;
        private readonly EQToolSettings settings;

        public EventOverlayWindow()
            : this(new EventOverlayViewModel(), AppServices.Initialize().Bootstrap.Settings)
        {
        }

        // Taking both lets the overlay be shown against throwaway state. The
        // parameterless path builds them from AppServices, which reads the real
        // settings file and subscribes to the live log stream.
        public EventOverlayWindow(EventOverlayViewModel viewModel, EQToolSettings settings)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            DataContext = viewModel;

            this.settings = settings;
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
