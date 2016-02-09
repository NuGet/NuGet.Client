using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Common;
using NuGet.Test.Utility;
using SynchronizationTestApp;
using Xunit;

namespace NuGet.Commands.Test
{
    public class SynchronizationTests
    {
        private int _value1 = 0;
        private int _value2 = 0;
        private SemaphoreSlim _waitForEverStarted = new SemaphoreSlim(0, 1);

        private readonly int DefaultTimeOut = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;

        [Fact]
        public async Task ConcurrencyUtilities_WaitAcquiresLock()
        {
            // Arrange
            string fileId = Guid.NewGuid().ToString();

            var ctsA = new CancellationTokenSource(DefaultTimeOut);
            var ctsB = new CancellationTokenSource(TimeSpan.Zero);
            var expected = 3;

            // Act
            var actual = await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                fileId,
                async tA =>
                {
                    try
                    {
                        await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                            fileId,
                            tB => Task.FromResult(0),
                            ctsB.Token);
                        Assert.False(true, "Waiting with a timeout for a lock that has not been released should fail.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    return expected;
                },
                ctsA.Token);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task ConcurrencyUtilities_ZeroTimeoutStillGetsLock()
        {
            // Arrange
            string fileId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource(TimeSpan.Zero);
            var expected = 3;

            // Act
            var actual = await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                fileId,
                token => Task.FromResult(expected),
                cts.Token);

            // Assert
            Assert.Equal(actual, expected);
        }

        [Fact]
        public async Task ConcurrencyUtilityBlocksInProc()
        {
            // Arrange
            string fileId = nameof(ConcurrencyUtilityBlocksInProc);

            var timeout = new CancellationTokenSource(DefaultTimeOut * 2);
            var cts = new CancellationTokenSource(DefaultTimeOut);

            var tasks = new Task<int>[4];

            // Act
            tasks[0] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForever1, cts.Token);

            await _waitForEverStarted.WaitAsync();

            // We should now be blocked, so the value returned from here should not be returned until the token is cancelled.
            tasks[1] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

            Assert.False(tasks[0].IsCompleted, $"task status: {tasks[0].Status}");

            _value1 = 1;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

            Assert.False(cts.Token.IsCancellationRequested);
            cts.Cancel();

            await tasks[1]; // let the first tasks pass
            await tasks[2]; // let the first tasks pass

            _value1 = 2;

            tasks[3] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

            await tasks[3];

            // Assert
            Assert.Equal(TaskStatus.Canceled, tasks[0].Status);
            Assert.Equal(1, tasks[1].Result);
            Assert.Equal(1, tasks[2].Result);
            Assert.Equal(2, tasks[3].Result);

            Assert.False(timeout.IsCancellationRequested);
        }

        [Fact]
        public async Task ConcurrencyUtilityBlocksOutOfProc()
        {
            // Arrange
            string fileId = Guid.NewGuid().ToString();

            var timeout = new CancellationTokenSource(DefaultTimeOut);

            var tasks = new Task<int>[3];

            // Act
            using (var sync = await Run(fileId, shouldAbandon: false, token: timeout.Token, shareProcessObject: true, debug: false))
            {
                var result = sync.Result;

                await WaitForLockToEngage(sync);

                _value1 = 0;

                // We should now be blocked, so the value returned from here should not be returned until the process has terminated.
                tasks[0] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

                _value1 = 1;

                tasks[1] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

                Assert.False(result.Process.HasExited);

                await ReleaseLock(sync);

                using (result.Process)
                {
                    if (!result.Process.WaitForExit(10000))
                    {
                        throw new TimeoutException("Process timed out.");
                    }
                }

                var fileIdReturned = result.Item2.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];

                Assert.Equal(fileId, fileIdReturned.Trim());
            }

            await tasks[0]; // let the first tasks pass
            await tasks[1]; // let the second tasks pass

            _value1 = 2;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt1, timeout.Token);

            await Task.WhenAll(tasks);

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(1, tasks[0].Result);
            Assert.Equal(1, tasks[1].Result);
            Assert.Equal(2, tasks[2].Result);

            Assert.False(timeout.IsCancellationRequested);
        }

        [Fact]
        public async Task ConcurrencyUtilityDoesntBlocksInProc()
        {
            // Arrange
            string fileId = nameof(ConcurrencyUtilityDoesntBlocksInProc);

            var timeout = new CancellationTokenSource(DefaultTimeOut * 2);
            var cts = new CancellationTokenSource(DefaultTimeOut);

            var tasks = new Task<int>[4];

            // Act
            tasks[0] = ConcurrencyUtilities.ExecuteWithFileLockedAsync("x" + fileId, WaitForever, cts.Token);

            _value2 = 0;

            // We should now be blocked, so the value returned from here should not be
            // returned until the token is cancelled.
            tasks[1] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt2, timeout.Token);

            await tasks[1];

            _value2 = 1;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt2, timeout.Token);

            await tasks[2]; // let the first tasks pass we get a deadlock if there is a lock applied by the first task

            Assert.False(cts.IsCancellationRequested);
            cts.Cancel();

            _value2 = 2;

            tasks[3] = ConcurrencyUtilities.ExecuteWithFileLockedAsync(fileId, WaitForInt2, timeout.Token);

            await tasks[3];

            await tasks[0];
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(0, tasks[1].Result);
            Assert.Equal(1, tasks[2].Result);
            Assert.Equal(2, tasks[3].Result);

            Assert.False(timeout.IsCancellationRequested);
        }

        [Fact]
        public async Task CrashingCommand()
        {
            // Arrange

            // Use a dummy file name so the whole system doesn't get locked
            var dummyFileName = Guid.NewGuid().ToString();

            var timeout = new CancellationTokenSource(DefaultTimeOut);

            // Act && Assert
            using (var run = (await
                Run(dummyFileName,
                    shouldAbandon: true,
                    token: timeout.Token,
                    debug: false,
                    shareProcessObject: true)))
            {
                await WaitForLockToEngage(run);

                var r1 = run.Result;

                await ReleaseLock(run);

                var exited = r1.Process.WaitForExit(1000);

                Assert.True(exited, "Timeout waiting for crashing process to exit.");

                // Verify that the process crashed
                if (PlatformServices.Default.Runtime.OperatingSystem.Equals("windows", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(-1 == r1.Process.ExitCode,
                        $"Failed to self kill, exitcode {r1.Process.ExitCode} {r1.Item2} {r1.Item3}");
                }
                else if (PlatformServices.Default.Runtime.OperatingSystem.Equals("linux", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(137 == r1.Process.ExitCode,
                        $"Failed to self kill, exitcode {r1.Process.ExitCode} {r1.Item2} {r1.Item3}");
                }
                else if (PlatformServices.Default.Runtime.OperatingSystem.Equals("mac", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(146 == r1.Process.ExitCode,
                        $"Failed to self kill, exitcode {r1.Process.ExitCode} {r1.Item2} {r1.Item3}");
                }

                Assert.Empty(r1.Item3);

                // Try to acquire the lock again with the same file name
                using (var run2 = await
                    Run(dummyFileName,
                        shouldAbandon: false,
                        token: timeout.Token,
                        shareProcessObject: true,
                        debug: false))
                {
                    await WaitForLockToEngage(run2);

                    var r2 = run2.Result;

                    await ReleaseLock(run2);

                    exited = r2.Process.WaitForExit(DefaultTimeOut);

                    Assert.True(exited, "Timeout waiting for second process to exit/failed to get lock.");

                    Assert.True(0 == r2.Process.ExitCode, $"Failed To get lock: {r2.Item2} {r2.Item3}");
                }
            }
        }

        private async Task<SyncdRunResult> Run(string fileName, bool shouldAbandon, CancellationToken token, bool shareProcessObject = false, bool debug = false)
        {
            var runtimePath = PlatformServices.Default.Runtime.RuntimePath;
            var dnxPath = Path.Combine(runtimePath, "dnx");
            var appPath = PlatformServices.Default.Application.ApplicationBasePath;
            var basePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(appPath)));
            var testAppName = nameof(SynchronizationTestApp);
            var testAppPath = Path.Combine(basePath, "src", "NuGet.Core", testAppName);

            var throwFlag = shouldAbandon ? Program.AbandonSwitch : string.Empty;
            var debugFlag = debug ? Program.DebugSwitch : string.Empty;

            // Use a dummy file name so the whole system doesn't get locked
            var dummyFileName = Guid.NewGuid().ToString();

            var result = new SyncdRunResult();

            result.Start();

            // Act && Assert
            var r = CommandRunner.Run(
                dnxPath,
                Directory.GetCurrentDirectory(),
                $"-p \"{testAppPath}\" run {debugFlag} {fileName} {result.Port} {throwFlag}",
                waitForExit: false,
                timeOutInMilliseconds: 100000,
                inputAction: null,
                shareProcessObject: shareProcessObject);

            result.Result = r;

            await result.Connect(token);

            return result;
        }

        private async Task WaitForLockToEngage(SyncdRunResult result)
        {
            var data = await result.Reader.ReadLineAsync();

            // data will be null on Mac, skip the check on Mac
            if (!RuntimeEnvironmentHelper.IsMacOSX && data.Trim() != "Locked")
            {
                throw new InvalidOperationException($"Unexpected output from process: {data}");
            }
        }

        private async Task ReleaseLock(SyncdRunResult result)
        {
            await result.Writer.WriteLineAsync("Go");
            await result.Writer.FlushAsync();
        }

        private async Task<int> WaitForever1(CancellationToken token)
        {
            _waitForEverStarted.Release();

            Assert.NotEqual(token, CancellationToken.None);

            return await Task.Run(async () =>
            {
                await Task.Delay(-1, token);
                return 123;
            });
        }

        private async Task<int> WaitForever(CancellationToken token)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(-1, token);
                }
                catch (TaskCanceledException)
                {
                }

                return 0;
            });
        }

        private Task<int> WaitForInt1(CancellationToken token)
        {
            int i = _value1;

            return Task.Run(() =>
            {
                return Task.FromResult(i);
            });
        }

        private async Task<int> WaitForInt2(CancellationToken token)
        {
            int i = _value2;

            return await Task.Run(() =>
            {
                return Task.FromResult(i);
            });
        }

        private class SyncdRunResult : IDisposable
        {
            public CommandRunnerResult Result { get; set; }

            private TcpListener Listener { get; set; }
            private TcpClient Client { get; set; }
            public StreamReader Reader { get; private set; }
            public StreamWriter Writer { get; private set; }

            public int Port { get; private set; }

            public void Start()
            {
                Port = 2224;
                bool done = false;
                while (!done)
                {
                    try
                    {
                        Listener = new TcpListener(IPAddress.Loopback, Port);
                        Listener.Start();
                        done = true;
                    }
                    catch
                    {
                        Port++;
                    }
                }
            }

            public async Task Connect(CancellationToken ct)
            {
                ct.Register(() => Listener.Stop());

                Client = await Listener.AcceptTcpClientAsync();

                var stream = Client.GetStream();
                Reader = new StreamReader(stream);
                Writer = new StreamWriter(stream);
            }

            public void Dispose()
            {
                using (Client) { }

                Listener.Stop();
            }
        }
    }
}
