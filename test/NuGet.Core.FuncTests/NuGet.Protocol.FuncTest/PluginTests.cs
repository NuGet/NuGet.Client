// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;
using PluginProtocolConstants = NuGet.Protocol.Plugins.ProtocolConstants;

namespace NuGet.Protocol.FuncTest
{
    public class PluginTests
    {
        private static readonly FileInfo _pluginFile;
        private static readonly ushort _portNumber = 11000;
        private static readonly IEnumerable<string> _pluginArguments = PluginConstants.PluginArguments
            .Concat(new[] { $"-PortNumber {_portNumber} -TestRunnerProcessId {GetCurrentProcessId()}" });

        static PluginTests()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "TestablePlugin", "Plugin.Testable.exe");

            _pluginFile = new FileInfo(filePath);
        }

        public PluginTests(ITestOutputHelper logger)
        {
            logger.WriteLine($"Plugin file path:  {_pluginFile.FullName}");
        }

        [PlatformFact(Platform.Windows)]
        public async Task GetOrCreateAsync_SuccessfullyHandshakes()
        {
            using (var test = await PluginTest.CreateAsync())
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, test.Plugin.Connection.ProtocolVersion);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task Initialize_Succeeds()
        {
            using (var test = await PluginTest.CreateAsync())
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, test.Plugin.Connection.ProtocolVersion);

                // Send canned response
                var responseSenderTask = Task.Run(() => test.ResponseSender.StartSendingAsync(test.CancellationToken));
                await test.ResponseSender.SendAsync(
                    MessageType.Response,
                    MessageMethod.Initialize,
                    new InitializeResponse(MessageResponseCode.Success));

                var clientVersion = MinClientVersionUtility.GetNuGetClientVersion().ToNormalizedString();
                var culture = CultureInfo.CurrentCulture.Name;
                var payload = new InitializeRequest(
                    clientVersion,
                    culture,
                    PluginConstants.RequestTimeout);

                var response = await test.Plugin.Connection.SendRequestAndReceiveResponseAsync<InitializeRequest, InitializeResponse>(
                    MessageMethod.Initialize,
                    payload,
                    test.CancellationToken);

                Assert.NotNull(response);
                Assert.Equal(MessageResponseCode.Success, response.ResponseCode);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task GetOperationClaims_ReturnsSupportedClaims()
        {
            using (var test = await PluginTest.CreateAsync())
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, test.Plugin.Connection.ProtocolVersion);

                // Send canned response
                var responseSenderTask = Task.Run(() => test.ResponseSender.StartSendingAsync(test.CancellationToken));
                await test.ResponseSender.SendAsync(
                    MessageType.Response,
                    MessageMethod.GetOperationClaims,
                    new GetOperationClaimsResponse(new OperationClaim[] { OperationClaim.DownloadPackage }));

                var serviceIndex = JObject.Parse("{}");
                var payload = new GetOperationClaimsRequest(packageSourceRepository: "a", serviceIndex: serviceIndex);

                var response = await test.Plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                    MessageMethod.GetOperationClaims,
                    payload,
                    test.CancellationToken);

                Assert.NotNull(response);
                Assert.Equal(1, response.Claims.Count);
                Assert.Equal(OperationClaim.DownloadPackage, response.Claims[0]);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task SendRequestAndReceiveResponseAsync_TimesOut()
        {
            using (var test = await PluginTest.CreateAsync())
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, test.Plugin.Connection.ProtocolVersion);

                // Send canned response
                var serviceIndex = JObject.Parse("{}");
                var payload = new GetOperationClaimsRequest(packageSourceRepository: "a", serviceIndex: serviceIndex);

                var stopwatch = Stopwatch.StartNew();

                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => test.Plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                        MessageMethod.Initialize,
                        payload,
                        test.CancellationToken));

                var requestTimeout = test.Plugin.Connection.Options.RequestTimeout;

                var low = requestTimeout.Add(TimeSpan.FromSeconds(-2));
                var high = TimeSpan.FromSeconds(requestTimeout.TotalSeconds * 2);

                Assert.InRange(stopwatch.Elapsed, low, high);
            }
        }

        private static int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }

        private sealed class PluginTest : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private bool _isDisposed;
            private readonly PluginFactory _pluginFactory;

            internal CancellationToken CancellationToken => _cancellationTokenSource.Token;
            internal IPlugin Plugin { get; }
            internal ResponseSender ResponseSender { get; }

            private PluginTest(PluginFactory pluginFactory,
                IPlugin plugin,
                ResponseSender responseSender,
                CancellationTokenSource cancellationTokenSource)
            {
                Plugin = plugin;
                _pluginFactory = pluginFactory;
                ResponseSender = responseSender;
                _cancellationTokenSource = cancellationTokenSource;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    using (_cancellationTokenSource)
                    {
                        _cancellationTokenSource.Cancel();

                        ResponseSender.Dispose();
                        Plugin.Dispose();
                        _pluginFactory.Dispose();
                    }

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<PluginTest> CreateAsync()
            {
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var pluginFactory = new PluginFactory(PluginConstants.IdleTimeout);
                var options = ConnectionOptions.CreateDefault();
                var plugin = await pluginFactory.GetOrCreateAsync(
                    _pluginFile.FullName,
                    _pluginArguments,
                    new RequestHandlers(),
                    options,
                    cancellationTokenSource.Token);
                var responseSender = new ResponseSender(_portNumber);

                return new PluginTest(pluginFactory, plugin, responseSender, cancellationTokenSource);
            }
        }
    }
}