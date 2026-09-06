using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EQTool.Avalonia.Services
{
    // Objective-C runtime interop against the NSWindow behind an Avalonia Window.
    //
    // Ported from mac/spike/OverlaySpike, where this was proven to float above a
    // Wine-hosted game window at NSWindow levels 3, 25, 27 and 1000 with working
    // click-through. Avalonia exposes the NSWindow pointer directly from
    // TryGetPlatformHandle(); the descriptor is "NSWindow", not NSView.
    //
    // Avalonia's Topmost maps to NSFloatingWindowLevel (3). Wine puts a fullscreen
    // window at NSStatusWindowLevel + 1 (26), so an overlay that must sit above one
    // needs 27 or higher.
    [SupportedOSPlatform("macos")]
    public static class MacOSWindowInterop
    {
        private const string LibObjC = "/usr/lib/libobjc.A.dylib";

        [DllImport(LibObjC, EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

        // objc_msgSend is variadic in C, so the CLR marshaller needs one declaration
        // per exact call shape rather than a single reusable signature.

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void_nint(IntPtr receiver, IntPtr selector, nint value);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void_ulong(IntPtr receiver, IntPtr selector, ulong value);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern int objc_msgSend_int(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_getClass")]
        private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

        private static readonly IntPtr SelSetLevel = sel_registerName("setLevel:");
        private static readonly IntPtr SelSetIgnoresMouseEvents = sel_registerName("setIgnoresMouseEvents:");
        private static readonly IntPtr SelSetCollectionBehavior = sel_registerName("setCollectionBehavior:");
        private static readonly IntPtr SelCollectionBehavior = sel_registerName("collectionBehavior");
        private static readonly IntPtr SelSharedWorkspace = sel_registerName("sharedWorkspace");
        private static readonly IntPtr SelFrontmostApplication = sel_registerName("frontmostApplication");
        private static readonly IntPtr SelProcessIdentifier = sel_registerName("processIdentifier");

        public const nint NSNormalWindowLevel = 0;
        public const nint NSFloatingWindowLevel = 3;
        public const nint NSStatusWindowLevel = 25;

        // Above Wine's fullscreen level of NSStatusWindowLevel + 1.
        public const nint OverlayWindowLevel = 27;

        public const ulong NSWindowCollectionBehaviorCanJoinAllSpaces = 1UL << 0;
        public const ulong NSWindowCollectionBehaviorStationary = 1UL << 4;
        public const ulong NSWindowCollectionBehaviorFullScreenPrimary = 1UL << 7;
        public const ulong NSWindowCollectionBehaviorFullScreenAuxiliary = 1UL << 8;

        public static bool IsSupported => OperatingSystem.IsMacOS();

        public static void SetWindowLevel(Window window, nint level)
        {
            if (!TryGetNSWindow(window, out var nsWindow))
                return;

            objc_msgSend_void_nint(nsWindow, SelSetLevel, level);
        }

        public static void SetIgnoresMouseEvents(Window window, bool ignore)
        {
            if (!TryGetNSWindow(window, out var nsWindow))
                return;

            objc_msgSend_void_bool(nsWindow, SelSetIgnoresMouseEvents, ignore);
        }

        // Lets the window follow the user across spaces and sit over a fullscreen
        // app instead of being pinned to the desktop space it was created on.
        public static void MakeOverlayBehaviour(Window window)
        {
            if (!TryGetNSWindow(window, out var nsWindow))
                return;

            var behaviour = objc_msgSend_ulong(nsWindow, SelCollectionBehavior);
            behaviour &= ~NSWindowCollectionBehaviorFullScreenPrimary;
            behaviour |= NSWindowCollectionBehaviorCanJoinAllSpaces
                | NSWindowCollectionBehaviorFullScreenAuxiliary
                | NSWindowCollectionBehaviorStationary;

            objc_msgSend_void_ulong(nsWindow, SelSetCollectionBehavior, behaviour);
        }

        // NSWorkspace reports a Wine-hosted program as "wine" with no bundle identifier,
        // so the pid is the only usable identity it offers. The Windows executable name
        // behind that pid comes from proc_name on the Core side.
        public static int? TryGetFrontmostProcessId()
        {
            if (!IsSupported)
                return null;

            var workspaceClass = objc_getClass("NSWorkspace");
            if (workspaceClass == IntPtr.Zero)
                return null;

            var workspace = objc_msgSend_IntPtr(workspaceClass, SelSharedWorkspace);
            if (workspace == IntPtr.Zero)
                return null;

            var frontmost = objc_msgSend_IntPtr(workspace, SelFrontmostApplication);
            if (frontmost == IntPtr.Zero)
                return null;

            var processId = objc_msgSend_int(frontmost, SelProcessIdentifier);
            return processId > 0 ? processId : (int?)null;
        }

        // Returns false rather than throwing when the handle is not there yet, so a
        // caller running before the window is shown degrades to doing nothing
        // instead of taking the app down.
        private static bool TryGetNSWindow(Window window, out IntPtr nsWindow)
        {
            nsWindow = IntPtr.Zero;

            if (!IsSupported || window == null)
                return false;

            var handle = window.TryGetPlatformHandle();
            if (handle == null || handle.Handle == IntPtr.Zero)
                return false;

            if (!string.Equals(handle.HandleDescriptor, "NSWindow", StringComparison.Ordinal))
                return false;

            nsWindow = handle.Handle;
            return true;
        }
    }
}
