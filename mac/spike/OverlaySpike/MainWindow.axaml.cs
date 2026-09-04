using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OverlaySpike;

public partial class MainWindow : Window
{
    private int _clickCount;

    public MainWindow()
    {
        InitializeComponent();

        // Place the overlay in the middle of the primary screen so it visibly
        // overlaps a typical winecfg window without needing user aim.
        Opened += OnOpenedApplyInterop;
        Opened += OnOpenedPosition;

        PointerPressed += OnPointerPressed;

        LevelText.Text = $"level={Program.WindowLevel}";
        ModeText.Text = Program.ClickThrough
            ? "mode=click-through (ignoresMouseEvents=YES)"
            : "mode=interactive (ignoresMouseEvents=NO)";
    }

    private void OnOpenedPosition(object? sender, EventArgs e)
    {
        // Overlap winecfg's central body (Windows Version dropdown, OK/Cancel/Apply
        // row) so click-through can be verified against a known-interactive target.
        // winecfg opens at logical (~10, ~30) with its dropdown around (270, 425).
        Position = new PixelPoint(100, 300);
    }

    private void OnOpenedApplyInterop(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsMacOS())
        {
            ModeText.Text = "mode=non-macOS (interop skipped)";
            return;
        }

        try
        {
            MacOSWindowInterop.SetWindowLevel(this, Program.WindowLevel);

            ulong behavior = MacOSWindowInterop.GetCollectionBehavior(this);
            if (Program.JoinAllSpaces)
                behavior |= MacOSWindowInterop.NSWindowCollectionBehaviorCanJoinAllSpaces;
            behavior |= MacOSWindowInterop.NSWindowCollectionBehaviorStationary;
            behavior |= MacOSWindowInterop.NSWindowCollectionBehaviorFullScreenAuxiliary;
            MacOSWindowInterop.SetCollectionBehavior(this, behavior);

            MacOSWindowInterop.SetIgnoresMouseEvents(this, Program.ClickThrough);
        }
        catch (Exception ex)
        {
            LevelText.Text = $"interop FAILED: {ex.GetType().Name}";
            ModeText.Text = ex.Message;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _clickCount++;
        ClickState.Text = $"clicks: {_clickCount}";
        MarkerBorder.Background = _clickCount % 2 == 0
            ? new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x3B, 0x30))
            : new SolidColorBrush(Color.FromArgb(0xCC, 0x34, 0xC7, 0x59));
    }
}
