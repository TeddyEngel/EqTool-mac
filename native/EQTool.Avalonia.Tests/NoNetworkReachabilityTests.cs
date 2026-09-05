using EQTool.Core.Platform;
using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EQTool.Avalonia.Tests
{
    // PigParseApi posts character data to pigparse.azurewebsites.net, and in this
    // build it is reachable from startup. LogParser takes an
    // IEnumerable<BaseHandler> for the stated purpose of forcing every handler
    // into existence; SlainHandler takes a PlayerTrackerService; and that service
    // enables a twenty second timer inside its own constructor which calls
    // SendPlayerData. The Sharing setting travels inside the payload instead of
    // gating the send, so none of it is opt-in.
    //
    // The first group of tests pins that reachability in place rather than
    // asserting it away. It is real, it cannot be removed without editing
    // upstream files, and the guard is what stands in front of it. If a future
    // change makes these fail, the guard may no longer be load-bearing and the
    // decision should be revisited deliberately.
    //
    // The second group covers the guard itself. No test here opens a socket: the
    // handler is driven directly with a recording stub underneath it.
    [TestClass]
    public class NoNetworkReachabilityTests
    {
        private static Assembly CoreAssembly => typeof(LogParser).Assembly;

        private static Type PigParseApiType => CoreAssembly.GetType("EQTool.Services.PigParseApi");

        private static IEnumerable<Type> ScannedTypes()
        {
            var types = CoreAssembly.GetTypes();

            foreach (var type in types.Where(a => a.IsClass && !a.IsAbstract))
            {
                if (type.GetInterfaces().Contains(typeof(IEqLogParser)))
                    yield return type;
            }

            foreach (var type in types.Where(a => !a.IsAbstract))
            {
                if (type.IsSubclassOf(typeof(BaseHandler)))
                    yield return type;
            }
        }

        private static bool DependsOn(Type root, Type unwanted, out string path)
        {
            var trail = new List<string>();
            var found = Walk(root, unwanted, new HashSet<Type>(), trail);
            path = string.Join(" -> ", trail);
            return found;
        }

        private static bool Walk(Type current, Type unwanted, HashSet<Type> seen, List<string> trail)
        {
            if (current == unwanted)
            {
                trail.Add(current.Name);
                return true;
            }

            if (current == null || !seen.Add(current) || current.Assembly != CoreAssembly)
                return false;

            foreach (var constructor in current.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    var parameterType = parameter.ParameterType;

                    if (parameterType.IsGenericType)
                        parameterType = parameterType.GetGenericArguments().FirstOrDefault() ?? parameterType;

                    if (Walk(parameterType, unwanted, seen, trail))
                    {
                        trail.Insert(0, current.Name);
                        return true;
                    }
                }
            }

            return false;
        }

        [TestMethod]
        public void PigParseApi_IsStillCompiledIn()
        {
            Assert.IsNotNull(PigParseApiType, "PigParseApi was not found; the tests below would pass vacuously.");
        }

        [TestMethod]
        public void PlayerTrackerService_StillReachesPigParseApi()
        {
            // Arrange
            var tracker = CoreAssembly.GetType("EQTool.Services.PlayerTrackerService");
            Assert.IsNotNull(tracker);

            // Act
            var reachable = DependsOn(tracker, PigParseApiType, out var path);

            // Assert
            Assert.IsTrue(reachable, "PlayerTrackerService no longer takes a PigParseApi. Re-check whether the guard is still needed.");
            StringAssert.Contains(path, "PigParseApi");
        }

        [TestMethod]
        public void HandlersForcedByLogParser_StillReachPigParseApi()
        {
            // Arrange
            var offenders = new List<string>();

            // Act
            foreach (var type in ScannedTypes().Distinct())
            {
                if (DependsOn(type, PigParseApiType, out var path))
                    offenders.Add(path);
            }

            // Assert
            // LogParser's IEnumerable<BaseHandler> parameter exists to construct
            // all of these, so every chain here is walked at startup.
            Assert.AreNotEqual(
                0,
                offenders.Count,
                "No registered handler reaches PigParseApi any more. The guard may no longer be load-bearing.");
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private static (HttpResponseMessage Response, int InnerCalls) Send(string url)
        {
            var inner = new RecordingHandler();
            var guard = new PigParseNetworkGuard(inner);
            using (var client = new HttpClient(guard))
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                return (response, inner.Calls);
            }
        }

        [TestMethod]
        [DataRow("https://pigparse.azurewebsites.net/api/player/upsertplayers")]
        [DataRow("https://pigparse.azurewebsites.net/api/player/getbynames")]
        [DataRow("https://pigparse.azurewebsites.net/api/zone/npcactivity")]
        [DataRow("https://pigparse.azurewebsites.net/api/zone/quakev2/Green")]
        [DataRow("https://pigparse.azurewebsites.net/api/boat/seen")]
        [DataRow("https://pigparse.azurewebsites.net/api/item/postmultiple")]
        [DataRow("https://pigparse.azurewebsites.net/api/rolltimer/timers/Green")]
        public void Guard_RefusesEverythingExceptTheWiki(string url)
        {
            // Act
            var (response, innerCalls) = Send(url);

            // Assert
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.AreEqual(0, innerCalls, "The request reached the inner handler, so it would have left the machine.");
        }

        [TestMethod]
        public void Guard_AllowsTheMobInfoWikiLookup()
        {
            // Act
            var (response, innerCalls) = Send("https://pigparse.azurewebsites.net/api/item/wiki");

            // Assert
            // Mob info is wired up and working, and this lookup sends no
            // character data.
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, innerCalls);
        }

        [TestMethod]
        public void Guard_LeavesOtherHostsAlone()
        {
            // Act
            var (response, innerCalls) = Send("https://wiki.project1999.com/Some_Mob");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, innerCalls);
        }

        [TestMethod]
        public void Guard_MatchesTheHostCaseInsensitively()
        {
            // Act
            var (response, innerCalls) = Send("https://PigParse.AzureWebsites.NET/api/player/upsertplayers");

            // Assert
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.AreEqual(0, innerCalls);
        }

        [TestMethod]
        public void Guard_RefusesRatherThanThrowing()
        {
            // Arrange
            // PlayerTrackerService calls this every twenty seconds inside a
            // try/catch that logs. Throwing would work but would fill the log
            // with stack traces, so the guard answers instead.
            var inner = new RecordingHandler();

            // Act
            using (var client = new HttpClient(new PigParseNetworkGuard(inner)))
            {
                var response = client
                    .PostAsync("https://pigparse.azurewebsites.net/api/player/upsertplayers", new StringContent("{}"))
                    .GetAwaiter()
                    .GetResult();

                // Assert
                Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.AreEqual(string.Empty, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void Guard_IsInstalledOnTheClientTheApiActuallyUses()
        {
            // Arrange
            // The tests above prove the handler works. This one proves it is
            // actually attached to App.httpclient, which is what PigParseApi
            // calls, rather than merely existing.
            var appType = CoreAssembly.GetType("EQTool.App");
            Assert.IsNotNull(appType);
            var client = (HttpClient)appType.GetField("httpclient", BindingFlags.Public | BindingFlags.Static).GetValue(null);

            var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handlerField, "Could not read the client's handler; the check below would be vacuous.");

            // Act
            var handler = handlerField.GetValue(client);

            // Assert
            Assert.IsInstanceOfType(handler, typeof(PigParseNetworkGuard),
                "App.httpclient is not guarded, so character data would be posted on a twenty second timer.");
        }
    }
}
