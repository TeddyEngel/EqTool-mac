using System;

namespace EQTool.Core.Platform
{
    // Bounds how long any single regex match may run.
    //
    // Triggers let a user write their own pattern. Without a timeout a pattern
    // that backtracks catastrophically does not fail, it runs forever, and since
    // matching happens on the log parsing thread the whole client stops updating
    // and stays stopped. Restarting does not help, because the same log line is
    // still there to be matched again.
    //
    // Upstream sets this in the static constructor of its WPF App class. That
    // class is not part of this build, so the value was never set here and every
    // pattern ran unbounded.
    //
    // Regex reads the key once, the first time the type is used anywhere in the
    // process, and caches it forever. Install has to run before anything builds a
    // Regex, which is why it is the first statement in Main rather than something
    // arranged later during startup.
    public static class RegexSafety
    {
        // Upstream's value, and its reasoning: the deadline is compared against
        // Environment.TickCount, which only advances every 15.6ms or so, so
        // anything much smaller aborts early and erratically.
        public const int DefaultMatchTimeoutMilliseconds = 25;

        private const string TimeoutKey = "REGEX_DEFAULT_MATCH_TIMEOUT";

        public static void Install()
        {
            if (AppDomain.CurrentDomain.GetData(TimeoutKey) != null)
                return;

            AppDomain.CurrentDomain.SetData(
                TimeoutKey,
                TimeSpan.FromMilliseconds(DefaultMatchTimeoutMilliseconds));
        }

        public static TimeSpan? Configured => AppDomain.CurrentDomain.GetData(TimeoutKey) as TimeSpan?;
    }
}
