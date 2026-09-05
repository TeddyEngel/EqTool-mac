using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using System;
using System.Collections.Specialized;

namespace EQTool.Avalonia.Views
{
    public partial class ConsoleWindow : Window
    {
        // A line already at the bottom can sit a fraction of a pixel short of the
        // extent after layout rounding, so "at the bottom" allows one pixel.
        private const double BottomTolerance = 1.0;

        private readonly ConsoleWindowViewModel viewModel;
        private readonly ScrollViewer scroller;

        // Newest lines are what the console is for, so it follows the tail. It
        // stops following the moment the reader scrolls away, because yanking
        // them back to the bottom every 100 ms poll would make the window
        // unreadable exactly when they are trying to read it.
        private bool followTail = true;

        public ConsoleWindow()
        {
            InitializeComponent();

            viewModel = new ConsoleWindowViewModel();
            DataContext = viewModel;

            WindowPreferences.Attach(this, AppServices.Initialize().Bootstrap.Settings.ConsoleWindowState);

            scroller = this.FindControl<ScrollViewer>("ConsoleScroller");
            scroller.ScrollChanged += OnScrollChanged;
            viewModel.Lines.CollectionChanged += OnLinesChanged;

            ScrollToTail();
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.Lines.CollectionChanged -= OnLinesChanged;
            scroller.ScrollChanged -= OnScrollChanged;
            viewModel.Dispose();
            base.OnClosed(e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Growing the content raises ScrollChanged with the offset unmoved, and
        // reading "not at the bottom" from that would switch following off on
        // the first line that arrives. Only a change in offset says anything
        // about where the reader wants to be.
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OffsetDelta.Y == 0)
                return;

            followTail = scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y
                <= BottomTolerance;
        }

        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!followTail)
                return;

            ScrollToTail();
        }

        // The new line has no extent until layout has run, so the scroll is
        // queued behind it rather than issued against a stale extent.
        private void ScrollToTail()
        {
            Dispatcher.UIThread.Post(() => scroller.ScrollToEnd(), DispatcherPriority.Background);
        }
    }
}
