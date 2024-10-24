// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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

            // Create the .bat file that simulates a plugin
            // Simply waits for a json request that looks like`{"RequestId":"ff52f0ae-28c9-4a19-957d-78db5b68f3f2","Type":"Request","Method":"Handshake","Payload":{"ProtocolVersion":"2.0.0","MinimumProtocolVersion":"1.0.0"}} `
            // It then creates a response with the same RequestId and Method, but with the Type changed to `Response` and Adds a Payload with a ResponseCode of 0
            // It finally sends a handshake request back to the caller. This completes the handshake process.
            string batFileContent = $@"
@echo off
setlocal EnableDelayedExpansion

:InputLoop
set /p jsonLine=

set quote=""
set comma=,
set colon=:
set openCurlyBracket={{
set closeCurlyBracket=}}
set key=
set value=
set dot=.
set onKeySearch=true
set onValueSearch=false
set enteredQuotes=false

set pos=0
:NextChar
    set index=%pos%
    set char=!jsonLine:~%pos%,1!
    if ""!char!""=="""" goto endLoop
    if %onKeySearch%==true (
        if %enteredQuotes%==true (
            if !char!==!quote! (
                set onKeySearch=false
                set onValueSearch=true
                set enteredQuotes=false
            ) else (
                set key=!key!!char!
            )
        ) else if !char!==!quote! (
            set enteredQuotes=true
        )
    ) else if %onValueSearch%==true (
        if %enteredQuotes%==true (
            if !char!==!quote! (
                set onKeySearch=true
                set onValueSearch=true
                set enteredQuotes=false
                set ""!key!=!value!""
                set value=
                set /a pos=pos+1
                goto ClearKey
            ) else (
                set value=!value!!char!
            )
        ) else if !char!==!quote! (
            set enteredQuotes=true
        ) else if !char!==!openCurlyBracket! (
            set onKeySearch=true
            set onValueSearch=false
            set key=!key!.
        )
    ) else (
        echo neither
    )
    set /a pos=pos+1
    if ""!jsonLine:~%pos%,1!"" NEQ """" goto NextChar

:ClearKey
    if ""!key!""=="""" (
        goto NextChar
    )
    set lastChar=!key:~-1!
    if !lastChar!==!dot! (
        goto NextChar
    ) else (
        set  key=!key:~0,-1!
        goto ClearKey
    )
    goto NextChar


:endLoop
if ""!RequestId!"" == """" (
    goto InputLoop
) else (
    set HandshakeReponseJsonString={{""RequestId"":""!RequestId!"",""Type"":""Response"",""Method"":""Handshake"",""Payload"":{{""ProtocolVersion"":""2.0.0"",""ResponseCode"":0}}}}
    set HandshakeRequestJsonString={{""RequestId"":""!RequestId!"",""Type"":""Request"",""Method"":""Handshake"",""Payload"":{{""ProtocolVersion"":""2.0.0"",""MinimumProtocolVersion"":""1.0.0""}}}}
    echo !HandshakeReponseJsonString!
    echo !HandshakeRequestJsonString!
)
goto InputLoop

";
            File.WriteAllText(pluginPath, batFileContent);

            var args = PluginConstants.PluginArguments;
            var reqHandler = new RequestHandlers();
            var options = ConnectionOptions.CreateDefault();

            var pluginFactory = new PluginFactory(Timeout.InfiniteTimeSpan);

            // Act
            var plugin = await pluginFactory.GetOrCreateNetToolsPluginAsync(pluginPath, args, reqHandler, options, CancellationToken.None);

            // Assert
            Assert.NotNull(plugin.Connection);
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
