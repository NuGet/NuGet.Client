// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
#if !IS_CORECLR
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

                var serviceIndex = JObject.Parse("{}");
                var payload = new GetOperationClaimsRequest(packageSourceRepository: "a", serviceIndex: serviceIndex);

                var stopwatch = Stopwatch.StartNew();

                await Assert.ThrowsAsync<TaskCanceledException>(
                    () => test.Plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                        MessageMethod.GetOperationClaims,
                        payload,
                        test.CancellationToken));

                var requestTimeout = test.Plugin.Connection.Options.RequestTimeout;

                var low = requestTimeout.Add(TimeSpan.FromSeconds(-2));
                var high = TimeSpan.FromSeconds(requestTimeout.TotalSeconds * 2);

                Assert.InRange(stopwatch.Elapsed, low, high);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task Fault_WritesExceptionToConsole()
        {
            using (var test = await PluginTest.CreateAsync())
            using (var closedEvent = new ManualResetEventSlim(initialState: false))
            {
                Assert.Equal(PluginProtocolConstants.CurrentVersion, test.Plugin.Connection.ProtocolVersion);

                test.Plugin.Closed += (object sender, EventArgs e) =>
                    {
                        closedEvent.Set();
                    };

                // Send canned response
                // This response is unexpected and will generate a protocol error.
                var responseSenderTask = Task.Run(() => test.ResponseSender.StartSendingAsync(test.CancellationToken));
                await test.ResponseSender.SendAsync(
                    MessageType.Response,
                    MessageMethod.Initialize,
                    new InitializeResponse(MessageResponseCode.Success));

                var serviceIndex = JObject.Parse("{}");
                var payload = new GetOperationClaimsRequest(packageSourceRepository: "a", serviceIndex: serviceIndex);

                string consoleOutput;

                using (var spy = new ConsoleOutputSpy())
                {
                    var requestTask = Task.Run(
                        () => test.Plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                            MessageMethod.GetOperationClaims,
                            payload,
                            test.CancellationToken));

                    closedEvent.Wait();

                    consoleOutput = spy.GetOutput();
                }

                Assert.Contains(
                    $"Terminating plugin '{test.Plugin.Name}' due to an unrecoverable fault:  NuGet.Protocol.Plugins.ProtocolException: A plugin protocol exception occurred.",
                    consoleOutput);
            }
        }
#endif
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

        private sealed class ConsoleOutputSpy : TextWriter
        {
            private readonly TextWriter _original;
            private readonly StringWriter _spy;

            public override Encoding Encoding => _original.Encoding;

            internal ConsoleOutputSpy()
            {
                _original = Console.Out;
                _spy = new StringWriter();

                Console.SetOut(this);
            }

            protected override void Dispose(bool disposing)
            {
                Console.SetOut(_original);
            }

#if IS_DESKTOP
            public override void Close()
            {
                _original.Close();
            }
#endif

            public override void Write(char value)
            {
                _original.Write(value);
                _spy.Write(value);
            }

            public override void Write(char[] buffer)
            {
                _original.Write(buffer);
                _spy.Write(buffer);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                _original.Write(buffer, index, count);
                _spy.Write(buffer, index, count);
            }

            public override void Write(string value)
            {
                _original.Write(value);
                _spy.Write(value);
            }

            public override async Task WriteAsync(char value)
            {
                await _original.WriteAsync(value);
                await _spy.WriteAsync(value);
            }

            public override async Task WriteAsync(char[] buffer, int index, int count)
            {
                await _original.WriteAsync(buffer, index, count);
                await _spy.WriteAsync(buffer, index, count);
            }

            public override async Task WriteAsync(string value)
            {
                await _original.WriteAsync(value);
                await _spy.WriteAsync(value);
            }

            public override async Task WriteLineAsync()
            {
                await _original.WriteLineAsync();
                await _spy.WriteLineAsync();
            }

            public override async Task WriteLineAsync(char value)
            {
                await _original.WriteLineAsync(value);
                await _spy.WriteLineAsync(value);
            }

            public override async Task WriteLineAsync(char[] buffer, int index, int count)
            {
                await _original.WriteLineAsync(buffer, index, count);
                await _spy.WriteLineAsync(buffer, index, count);
            }

            public override async Task WriteLineAsync(string value)
            {
                await _original.WriteLineAsync(value);
                await _spy.WriteLineAsync(value);
            }

            public override async Task FlushAsync()
            {
                await _original.FlushAsync();
                await _spy.FlushAsync();
            }

            internal string GetOutput()
            {
                return _spy.ToString();
            }
        }
    }
}