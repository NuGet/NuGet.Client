// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginFactoryTests
    {
        [Fact]
        public void Constructor_ThrowsForTimeSpanBelowMinimum()
        {
            var timeout = TimeSpan.FromMilliseconds(Timeout.InfiniteTimeSpan.TotalMilliseconds - 1);

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PluginFactory(timeout));

            Assert.Equal("pluginIdleTimeout", exception.ParamName);
            Assert.Equal(timeout, exception.ActualValue);
        }

        [Fact]
        public void Constructor_AcceptsInfiniteTimeSpan()
        {
            new PluginFactory(Timeout.InfiniteTimeSpan);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var factory = new PluginFactory(Timeout.InfiniteTimeSpan))
            {
                factory.Dispose();
                factory.Dispose();
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetOrCreateAsync_ThrowsForNullOrEmptyFilePath(string filePath)
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => factory.GetOrCreateAsync(
                    filePath,
                    PluginConstants.PluginArguments,
                    new RequestHandlers(),
                    ConnectionOptions.CreateDefault(),
                    CancellationToken.None));

            Assert.Equal("filePath", exception.ParamName);
        }

        [Fact]
        public async Task GetOrCreateAsync_ThrowsForNullArguments()
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => factory.GetOrCreateAsync(
                    filePath: "a",
                    arguments: null,
                    requestHandlers: new RequestHandlers(),
                    options: ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("arguments", exception.ParamName);
        }

        [Fact]
        public async Task GetOrCreateAsync_ThrowsForNullRequestHandlers()
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => factory.GetOrCreateAsync(
                    filePath: "a",
                    arguments: PluginConstants.PluginArguments,
                    requestHandlers: null,
                    options: ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("requestHandlers", exception.ParamName);
        }

        [Fact]
        public async Task GetOrCreateAsync_ThrowsForNullConnectionOptions()
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => factory.GetOrCreateAsync(
                    filePath: "a",
                    arguments: PluginConstants.PluginArguments,
                    requestHandlers: new RequestHandlers(),
                    options: null,
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        public async Task GetOrCreateAsync_ThrowsIfCancelled()
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => factory.GetOrCreateAsync(
                    filePath: "a",
                    arguments: PluginConstants.PluginArguments,
                    requestHandlers: new RequestHandlers(),
                    options: ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task GetOrCreateAsync_ThrowsIfDisposed()
        {
            var factory = new PluginFactory(Timeout.InfiniteTimeSpan);

            factory.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => factory.GetOrCreateAsync(
                    filePath: "a",
                    arguments: PluginConstants.PluginArguments,
                    requestHandlers: new RequestHandlers(),
                    options: ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal(nameof(PluginFactory), exception.ObjectName);
        }

        [PlatformFact(Platform.Windows)]
        public async Task GetOrCreateNetPluginAsync_CreatesPluginAndSendsHandshakeRequest()
        {
            using TestDirectory testDirectory = TestDirectory.Create();
            string pluginPath = Path.Combine(testDirectory.Path, "nuget-plugin-batFile.bat");

            string outputFilePath = Path.Combine(testDirectory.Path, "output.txt");
            File.WriteAllText(outputFilePath, "");

            // Create the .bat file that simulates a plugin
            // Simply waits for a json request that looks like`{"RequestId":"ff52f0ae-28c9-4a19-957d-78db5b68f3f2","Type":"Request","Method":"Handshake","Payload":{"ProtocolVersion":"2.0.0","MinimumProtocolVersion":"1.0.0"}} `
            // and sends it back with the type changed to `Response`
            string batFileContent = $@"
@echo off
setlocal enabledelayedexpansion

set outputFile={outputFilePath}

:: Clear the output file initially (if it exists)
echo. > %outputFile%

:: Initialize the counter for '}}' characters
set closingBraceCount=0

:loop

:: Read from stdin (standard input)
set /p input=""""

:: Count occurrences of '}}' in the input
for /l %%i in (0,1,1023) do (
    if ""!input:~%%i,1!""==""}}"" (
        set /a closingBraceCount+=1
    )
)

:: Write the input to the output file
echo !input! >> %outputFile%

:: If '}}' has appeared twice (even across multiple inputs), process the output file
if !closingBraceCount! geq 2 (
    goto processOutput
)

:: Go back to the loop to accept more input
goto loop

:processOutput

for /f ""delims="" %%a in (%outputFile%) do (
    set ""singleLine=!singleLine! %%a""
)

:: Perform string replacement (""Request"" -> ""Response"") in the single line
set modifiedLine=!singleLine:""Type"":""Request""=""Type"":""Response""!

echo !modifiedLine!
";
            File.WriteAllText(pluginPath, batFileContent);

            var args = PluginConstants.PluginArguments;
            var reqHandler = new RequestHandlers();
            var options = ConnectionOptions.CreateDefault();

            var pluginFactory = new PluginFactory(Timeout.InfiniteTimeSpan);

            // Act
            var plugin = Assert.ThrowsAsync<PluginException>(() => pluginFactory.GetOrCreateNetToolsPluginAsync(pluginPath, args, reqHandler, options, CancellationToken.None));
            await Task.Delay(10);

            // Assert
            Assert.NotNull(plugin);
            string jsonOutput = File.ReadAllText(outputFilePath);
            var jsonDocument = JsonDocument.Parse(jsonOutput);
            var rootElement = jsonDocument.RootElement;
            if (rootElement.TryGetProperty("Method", out JsonElement methodElement))
            {
                Assert.Equal("Handshake", methodElement.GetString());
            }
            else
            {
                Assert.Fail("The Method property was not found in the JSON output.");
            }

            if (rootElement.TryGetProperty("Type", out JsonElement typeElement))
            {
                Assert.Equal("Request", typeElement.GetString());
            }
            else
            {
                Assert.Fail("The Type property was not found in the JSON output.");
            }
        }

        [Fact]
        public async Task CreateFromCurrentProcessAsync_ThrowsForNullRequestHandlers()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PluginFactory.CreateFromCurrentProcessAsync(
                    requestHandlers: null,
                    options: ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("requestHandlers", exception.ParamName);
        }

        [Fact]
        public async Task CreateFromCurrentProcessAsync_ThrowsForNullConnectionOptions()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PluginFactory.CreateFromCurrentProcessAsync(
                    requestHandlers: new RequestHandlers(),
                    options: null,
                    sessionCancellationToken: CancellationToken.None));

            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        public async Task CreateFromCurrentProcessAsync_ThrowsIfCancelled()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => PluginFactory.CreateFromCurrentProcessAsync(
                    new RequestHandlers(),
                    ConnectionOptions.CreateDefault(),
                    sessionCancellationToken: new CancellationToken(canceled: true)));
        }
    }
}
