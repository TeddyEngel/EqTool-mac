using EQTool.Services;

namespace EQTool.Avalonia.Services
{
    // Upstream's `TextToSpeach` is built on `System.Speech.Synthesis`, which is
    // Windows-only. Trigger handlers take `ITextToSpeach` by constructor
    // injection and call `Say` freely, so the container needs something to bind.
    //
    // This milestone is the timer list; spoken alerts are not part of it. Rather
    // than half-wire `/usr/bin/say` here, the implementation is explicitly a
    // no-op so nothing silently pretends to speak.
    public class SilentTextToSpeach : ITextToSpeach
    {
        public void Say(string text)
        {
        }
    }
}
