using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace OverlaySpike;

/// <summary>
/// Minimal Objective-C runtime interop against the NSWindow backing an Avalonia
/// Window on macOS. Avalonia exposes the NSWindow pointer directly via
/// TryGetPlatformHandle().Handle (HandleDescriptor == "NSWindow").
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacOSWindowInterop
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    // --- Objective-C runtime --------------------------------------------------

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

    // Distinct DllImport overloads per signature. objc_msgSend is variadic in C
    // and must be declared per exact call shape for the CLR marshaller.

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nint(IntPtr receiver, IntPtr selector, nint value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_ulong(IntPtr receiver, IntPtr selector, ulong value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

    // --- Cached selectors -----------------------------------------------------

    private static readonly IntPtr SelSetLevel = sel_registerName("setLevel:");
    private static readonly IntPtr SelSetIgnoresMouseEvents = sel_registerName("setIgnoresMouseEvents:");
    private static readonly IntPtr SelSetCollectionBehavior = sel_registerName("setCollectionBehavior:");
    private static readonly IntPtr SelCollectionBehavior = sel_registerName("collectionBehavior");

    // --- NSWindowCollectionBehavior bit flags ---------------------------------

    public const ulong NSWindowCollectionBehaviorCanJoinAllSpaces = 1UL << 0;
    public const ulong NSWindowCollectionBehaviorStationary = 1UL << 4;
    public const ulong NSWindowCollectionBehaviorFullScreenPrimary = 1UL << 7;
    public const ulong NSWindowCollectionBehaviorFullScreenAuxiliary = 1UL << 8;

    // --- Public API -----------------------------------------------------------

    public static void SetWindowLevel(Window window, nint level)
    {
        IntPtr nsWindow = GetNSWindow(window);
        objc_msgSend_void_nint(nsWindow, SelSetLevel, level);
    }

    public static void SetIgnoresMouseEvents(Window window, bool ignore)
    {
        IntPtr nsWindow = GetNSWindow(window);
        objc_msgSend_void_bool(nsWindow, SelSetIgnoresMouseEvents, ignore);
    }

    public static void SetCollectionBehavior(Window window, ulong behavior)
    {
        IntPtr nsWindow = GetNSWindow(window);
        objc_msgSend_void_ulong(nsWindow, SelSetCollectionBehavior, behavior);
    }

    public static ulong GetCollectionBehavior(Window window)
    {
        IntPtr nsWindow = GetNSWindow(window);
        return objc_msgSend_ulong(nsWindow, SelCollectionBehavior);
    }

    // --- Handle extraction ----------------------------------------------------

    private static IntPtr GetNSWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null)
            throw new InvalidOperationException("Window has no platform handle yet - call after OnOpened.");
        if (handle.Handle == IntPtr.Zero)
            throw new InvalidOperationException("Platform handle is null.");
        // On macOS Avalonia reports the NSWindow directly; the descriptor is
        // "NSWindow". If a future Avalonia release changes this, fail loudly.
        if (!string.Equals(handle.HandleDescriptor, "NSWindow", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Expected NSWindow handle, got '{handle.HandleDescriptor}'.");
        return handle.Handle;
    }
}
