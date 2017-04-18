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
            .Concat(new[] { $"-PortNumber {_portNumber}" });

        static PluginTests()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "TestablePlugin", "Plugin.Testable.exe");

            _pluginFile = new FileInfo(filePath);
        }

        public PluginTests(ITestOutputHelper logger)
        {
            logger.WriteLine($"Plugin file path:  {_pluginFile.FullName}");

            if (IsPluginDebuggingEnabled())
            {
                WaitForDebuggerAttach();
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task CreateAsyncAndHandshake()
        {
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            using (var pluginFactory = new PluginFactory(PluginConstants.IdleTimeout))
            using (var plugin = await pluginFactory.GetOrCreateAsync(
                _pluginFile.FullName,
                _pluginArguments,
                new RequestHandlers(),
                CreateConnectionOptions(),
                cancellationTokenSource.Token))
            using (var responseSender = new ResponseSender(_portNumber))
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, plugin.Connection.ProtocolVersion);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task Initialize()
        {
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            using (var pluginFactory = new PluginFactory(PluginConstants.IdleTimeout))
            using (var plugin = await pluginFactory.GetOrCreateAsync(
                _pluginFile.FullName,
                _pluginArguments,
                new RequestHandlers(),
                CreateConnectionOptions(),
                cancellationTokenSource.Token))
            using (var responseSender = new ResponseSender(_portNumber))
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, plugin.Connection.ProtocolVersion);

                // Send canned response
                var responseSenderTask = Task.Run(() => responseSender.StartSendingAsync(cancellationTokenSource.Token));
                await responseSender.SendAsync(
                    MessageType.Response,
                    MessageMethod.Initialize,
                    new InitializeResponse(MessageResponseCode.Success));

                var clientVersion = MinClientVersionUtility.GetNuGetClientVersion().ToNormalizedString();
                var culture = CultureInfo.CurrentCulture.Name;
                var payload = new InitializeRequest(clientVersion, culture, Verbosity.Normal, TimeSpan.FromSeconds(30));

                var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<InitializeRequest, InitializeResponse>(
                    MessageMethod.Initialize, payload, cancellationTokenSource.Token);

                Assert.NotNull(response);
                Assert.Equal(MessageResponseCode.Success, response.ResponseCode);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task GetOperationClaims()
        {
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            using (var pluginFactory = new PluginFactory(PluginConstants.IdleTimeout))
            using (var plugin = await pluginFactory.GetOrCreateAsync(
                _pluginFile.FullName,
                _pluginArguments,
                new RequestHandlers(),
                CreateConnectionOptions(),
                cancellationTokenSource.Token))
            using (var responseSender = new ResponseSender(_portNumber))
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, plugin.Connection.ProtocolVersion);

                // Send canned response
                var responseSenderTask = Task.Run(() => responseSender.StartSendingAsync(cancellationTokenSource.Token));
                await responseSender.SendAsync(
                    MessageType.Response,
                    MessageMethod.GetOperationClaims,
                    new GetOperationClaimsResponse(new OperationClaim[] { OperationClaim.DownloadPackage }));

                var serviceIndex = JObject.Parse("{}");
                var payload = new GetOperationClaimsRequest(packageSourceRepository: "a", serviceIndex: serviceIndex);

                var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                    MessageMethod.Initialize, payload, cancellationTokenSource.Token);

                Assert.NotNull(response);
                Assert.Equal(1, response.Claims.Count);
                Assert.Equal(OperationClaim.DownloadPackage, response.Claims[0]);
            }
        }

        private static ConnectionOptions CreateConnectionOptions()
        {
            return new ConnectionOptions(
                PluginProtocolConstants.CurrentVersion,
                PluginProtocolConstants.CurrentVersion,
                PluginProtocolConstants.MaxTimeout,
                PluginProtocolConstants.MaxTimeout);
        }

        private static bool IsPluginDebuggingEnabled()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_PLUGIN_DEBUG"));
        }

        private static void WaitForDebuggerAttach()
        {
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(1000);
            }

            Debugger.Break();
        }
    }
}