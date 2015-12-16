using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class ConcurrencyTests
    {
        private bool _waitForEverStarted = false;

        private int _value1 = 0;
        private int _value2 = 0;

        [Fact]
        public async void ConcurrencyUtilityBlocksInProc()
        {
            // Arrange
            string fileId = nameof(ConcurrencyUtilityBlocksInProc);
            var cts = new CancellationTokenSource();

            var tasks = new Task<int>[4];

            // Act
            tasks[0] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForever1, CancellationToken.None);

            while (!_waitForEverStarted) { } // spinwait

            // We should now be blocked, so the value returned from here should not be returned until the token is cancelled.
            tasks[1] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            _value1 = 1;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            cts.Cancel();

            await tasks[2]; // let the first tasks pass

            _value1 = 2;

            tasks[3] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            await Task.WhenAll(tasks);

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(1, tasks[1].Result);
            Assert.Equal(1, tasks[2].Result);
            Assert.Equal(2, tasks[3].Result);
        }

        [Fact]
        public async void ConcurrencyUtilityBlocksOutOfProc()
        {
            // Arrange
            string fileId = Guid.NewGuid().ToString();

            var cts = new CancellationTokenSource();

            var tasks = new Task<int>[3];

            // Act
            var result = Run(fileId, shouldThrow: false, shareProcessObject: true, debug: false);

            // Make sure the process is locked
            while (!result.Item2.StartsWith("Locked"))
            {
                if (result.Process.HasExited)
                {
                    throw new InvalidOperationException("Process failed: " + result.Item3);
                }

                Thread.Sleep(1);
            }

            _value1 = 0;

            // We should now be blocked, so the value returned from here should not be returned until the process has terminated.
            tasks[0] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            _value1 = 1;

            tasks[1] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            Assert.False(result.Process.HasExited);

            using (result.Process)
            {
                if (!result.Process.WaitForExit(10000))
                {
                    throw new TimeoutException("Process timed out.");
                }
            }

            var fileIdReturned = result.Item2.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[1];
            Assert.Equal(fileId, fileIdReturned.Trim());

            await tasks[0]; // let the first tasks pass
            await tasks[1]; // let the second tasks pass

            _value1 = 2;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt1, CancellationToken.None);

            await Task.WhenAll(tasks);

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(1, tasks[0].Result);
            Assert.Equal(1, tasks[1].Result);
            Assert.Equal(2, tasks[2].Result);
        }

        [Fact]
        public async void ConcurrencyUtilityDoesntBlocksInProc()
        {
            // Arrange
            string fileId = nameof(ConcurrencyUtilityDoesntBlocksInProc);
            var cts = new CancellationTokenSource();

            var tasks = new Task<int>[4];

            // Act
            tasks[0] = ConcurrencyUtilities.ExecuteWithFileLocked("x" + fileId, WaitForever, CancellationToken.None);

            _value2 = 0;

            // We should now be blocked, so the value returned from here should not be returned until the token is cancelled.
            tasks[1] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt2, CancellationToken.None);

            await tasks[1];

            _value2 = 1;

            tasks[2] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt2, CancellationToken.None);

            await tasks[2]; // let the first tasks pass we get a deadlock if there is a lock applied by the first task

            cts.Cancel();

            _value2 = 2;

            tasks[3] = ConcurrencyUtilities.ExecuteWithFileLocked(fileId, WaitForInt2, CancellationToken.None);

            await Task.WhenAll(tasks);

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(0, tasks[1].Result);
            Assert.Equal(1, tasks[2].Result);
            Assert.Equal(2, tasks[3].Result);
        }

        [Fact]
        public void CrashingCommand()
        {
            // Arrange

            // Use a dummy file name so the whole system doesn't get locked
            var dummyFileName = Guid.NewGuid().ToString();

            // Act && Assert
            var r = Run(dummyFileName, shouldThrow: true);

            // Verify that the process crashed
            Assert.True(1 == r.Item1, $"Failed to crash: {r.Item2} {r.Item3}");
            Assert.StartsWith("System.InvalidOperationException: Aborted", r.Item3);

            // Try to acquire the lock again with the same file name
            try
            {
                r = Run(dummyFileName, shouldThrow: false);
            }
            catch (TimeoutException)
            {
                Assert.True(false, "Failed to acquire the lock and timed out");
            }

            Assert.True(0 == r.Item1, $"Failed To get lock: {r.Item2} {r.Item3}");
        }

        private CommandRunnerResult Run(string fileName, bool shouldThrow, bool shareProcessObject = false, bool debug = false)
        {
            var runtimePath = PlatformServices.Default.Runtime.RuntimePath;
            var dnxPath = Path.Combine(runtimePath, "dnx.exe");
            var appPath = PlatformServices.Default.Application.ApplicationBasePath;
            var basePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(appPath)));
            var testAppName = nameof(SynchronizationTestApp);
            var testAppPath = Path.Combine(basePath, "src", "NuGet.Core", testAppName);

            var throwFlag = shouldThrow ? "-throw" : string.Empty;
            var debugFlag = debug ? "--Debug" : string.Empty;

            // Use a dummy file name so the whole system doesn't get locked
            var dummyFileName = Guid.NewGuid().ToString();

            // Act && Assert
            var r = CommandRunner.Run(
                dnxPath,
                Directory.GetCurrentDirectory(),
                $"-p \"{testAppPath}\" run {debugFlag} {fileName} {throwFlag}",
                waitForExit: !shareProcessObject,
                timeOutInMilliseconds: 100000,
                shareProcessObject: shareProcessObject);

            return r;
        }

        private Task<int> WaitForever1(CancellationToken token)
        {
            _waitForEverStarted = true;

            return Task.Run(() =>
            {
                Task.Delay(-1, token);
                return 0;
            });
        }

        private Task<int> WaitForever(CancellationToken token)
        {
            return Task.Run(() =>
            {
                Task.Delay(-1, token);
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

        private Task<int> WaitForInt2(CancellationToken token)
        {
            int i = _value2;

            return Task.Run(() =>
            {
                return Task.FromResult(i);
            });
        }
    }
}
