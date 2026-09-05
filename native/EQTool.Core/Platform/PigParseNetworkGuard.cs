using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EQTool.Core.Platform
{
    // Refuses outbound calls to the PigParse service that were never asked for.
    //
    // PigParseApi is compiled into the Mac build and it is reachable. LogParser
    // takes an IEnumerable<BaseHandler> specifically to force every handler into
    // existence, SlainHandler takes a PlayerTrackerService, and that service
    // starts a twenty second timer in its own constructor which posts to
    // /api/player/upsertplayers. Nothing in that chain consults the Sharing
    // setting: it travels inside the payload rather than gating the send. So
    // simply starting the client is enough to begin uploading character data.
    //
    // Dropping the offending types is not an option, because they arrive through
    // directory-wide includes and the handlers around them are needed for spell
    // and combat parsing. Blocking by host is not an option either, since the
    // mob info window's wiki lookup lives on the same host.
    //
    // What is left is an allow list by path, which is also the safer shape: a
    // new upstream endpoint is refused by default rather than let through.
    public sealed class PigParseNetworkGuard : DelegatingHandler
    {
        public const string PigParseHost = "pigparse.azurewebsites.net";

        // The mob info window's wiki lookup, which the client posts on every con.
        // It is not free of character data: the body is a name and the zone the
        // character is standing in. It is allowed because refusing it removes the
        // mob info window entirely rather than degrading it, and because it
        // carries no character name, guild or coordinates. Worth revisiting if
        // the answer to location sharing is no.
        private static readonly string[] AllowedPaths = new[]
        {
            "/api/item/wiki",
        };

        public PigParseNetworkGuard()
            : base(new HttpClientHandler())
        {
        }

        public PigParseNetworkGuard(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        public static bool IsAllowed(Uri requestUri)
        {
            if (requestUri == null)
                return false;

            // Only the PigParse service is restricted. Nothing else in the core
            // makes outbound calls, and refusing unknown hosts outright would
            // block a future caller for reasons that have nothing to do with
            // this guard.
            if (!string.Equals(requestUri.Host, PigParseHost, StringComparison.OrdinalIgnoreCase))
                return true;

            // Compared whole rather than by prefix. A prefix would also admit a
            // future /api/item/wikiupload, and it would admit
            // /api/item/wiki%2F..%2Fplayer/upsertplayers, because AbsolutePath
            // leaves %2F encoded and so the string still begins with the allowed
            // path. An unencoded ../ is harmless by comparison, since Uri
            // resolves it away before this runs.
            return AllowedPaths.Any(allowed =>
                string.Equals(requestUri.AbsolutePath, allowed, StringComparison.OrdinalIgnoreCase));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request != null && !IsAllowed(request.RequestUri))
                return Task.FromResult(Refuse(request));

            return base.SendAsync(request, cancellationToken);
        }

        // Answered rather than thrown. Every caller wraps these in a try/catch
        // that logs, so an exception would work, but it would also fill the log
        // with stack traces every twenty seconds. An empty body keeps the
        // deserialising callers on their existing empty-result path.
        private static HttpResponseMessage Refuse(HttpRequestMessage request)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                RequestMessage = request,
                ReasonPhrase = "Blocked by PigParseNetworkGuard: not enabled by the user",
                Content = new StringContent(string.Empty),
            };
        }
    }
}
