// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Common.Test
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
            var fileId = Guid.NewGuid().ToString();

            var ctsA = new CancellationTokenSource(DefaultTimeOut);
            var ctsB = new CancellationTokenSource();
            ctsB.Cancel();
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
        public async Task ConcurrencyUtilities_CancelledTokeDoesNotGetLock()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var expected = 3;

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                fileId,
                token => Task.FromResult(expected),
                cts.Token));
        }

        [Fact]
        public async Task ConcurrencyUtilityBlocksInProc()
        {
            // Arrange
            var fileId = nameof(ConcurrencyUtilityBlocksInProc);

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
        public async Task ConcurrencyUtilityDoesntBlocksInProc()
        {
            // Arrange
            var fileId = nameof(ConcurrencyUtilityDoesntBlocksInProc);

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
        public void KeyedMutex_WaitAcquiresLock()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();

            var cancelledToken = new CancellationTokenSource();
            cancelledToken.Cancel();

            using (var mutex = new KeyedMutex())
            {
                // Act
                mutex.Enter(fileId);
                try
                {
                    // need to use EnterAsync here, because using Enter will deadlock.
                    var asyncTask = mutex.EnterAsync(fileId, cancelledToken.Token);
                    asyncTask.GetAwaiter().GetResult();
                    Assert.True(false, "Async enter appers to have completed, but should have been cancelled.");
                }
                catch (OperationCanceledException)
                {
                    // expected
                }
                finally
                {
                    mutex.Exit(fileId);
                }
            }
        }

        [Fact]
        public async Task KeyedMutex_WaitAsyncAcquiresLock()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();

            var cts1 = new CancellationTokenSource(1000);
            var cancelledToken = new CancellationTokenSource();
            cancelledToken.Cancel();

            using (var mutex = new KeyedMutex())
            {
                // Act
                await mutex.EnterAsync(fileId, cts1.Token);
                try
                {
                    await mutex.EnterAsync(fileId, cancelledToken.Token);
                    Assert.True(false, "Async enter appers to have completed, but should have been cancelled.");
                }
                catch (OperationCanceledException)
                {
                    // expected
                }
                finally
                {
                    mutex.Exit(fileId);
                }
            }
        }

        [Fact]
        public async Task KeyedMutex_CancelledTokenDoesNotEnter()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var entered = false;

            // Act
            using (var mutex = new KeyedMutex())
            {
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    await mutex.EnterAsync(fileId, cts.Token);
                    try
                    {
                        entered = true;
                    }
                    finally
                    {
                        await mutex.ExitAsync(fileId);
                    }
                });
            }

            // Assert
            Assert.False(entered);
        }

        [Fact]
        public async Task KeyedMutex_DifferentKeysDoNotBlockEachOther()
        {
            // Arrange
            var key1 = "key1";
            var key2 = "key2";
            var cts = new CancellationTokenSource(millisecondsDelay: 10000);

            // Act
            using (var mutex = new KeyedMutex())
            {
                await mutex.EnterAsync(key1, cts.Token);
                try
                {
                    await mutex.EnterAsync(key2, cts.Token);
                    await mutex.ExitAsync(key2);
                }
                finally
                {
                    await mutex.ExitAsync(key1);
                }
            }
        }

        [Fact]
        public async Task KeyedMutex_DelegatesRunOneAtATime()
        {
            // Arrange
            var counter = 0;
            var cts = new CancellationTokenSource(millisecondsDelay: 10000);
            var start = new TaskCompletionSource<bool>();
            var key = "key";

            var tasks = new Task[Environment.ProcessorCount * 2];

            using (var mutex = new KeyedMutex()) {
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        // Spin lock. If we await, then task schedulder might not have multiple threads run mutex.EnterAsync at the same time.
                        // We want to maximise chance of finding multithreading/timing issues.
                        while (!start.Task.IsCompleted)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                        }

                        await mutex.EnterAsync(key, cts.Token);
                        try
                        {
                            for (int j = 0; j < 1000; j++)
                            {
                                counter++;
                            }
                        }
                        finally
                        {
                            await mutex.ExitAsync(key);
                        }
                    });
                }

                // Act
                start.SetResult(true);
                await Task.WhenAll(tasks);
            }

            // Assert
            Assert.Equal(Environment.ProcessorCount * 2000, counter);
        }

        [Fact]
        public async Task KeyedMutex_KeyReuseTest()
        {
            // Arrange
            var cts = new CancellationTokenSource(millisecondsDelay: 100);
            var key = "key";

            // Try to catch any timing issues with one thread exiting the mutex when another is entering.
            // More than 2 concurrent tasks makes it likely that the key's counter will be greater than zero
            // and therefore exiting will not try to remove the key from its dictionary.
            var tasks = new Task[2];
            using (var mutex = new KeyedMutex())
            {
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        while (true)
                        {
                            try
                            {
                                await mutex.EnterAsync(key, cts.Token);
                                await mutex.ExitAsync(key);
                            }
                            catch (OperationCanceledException)
                            {
                                // end of test
                                break;
                            }
                        }
                    });
                }

                // Act
                await Task.WhenAll(tasks);
            }
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
            var i = _value1;

            return Task.Run(() =>
            {
                return Task.FromResult(i);
            });
        }

        private async Task<int> WaitForInt2(CancellationToken token)
        {
            var i = _value2;

            return await Task.Run(() =>
            {
                return Task.FromResult(i);
            });
        }
    }
}
