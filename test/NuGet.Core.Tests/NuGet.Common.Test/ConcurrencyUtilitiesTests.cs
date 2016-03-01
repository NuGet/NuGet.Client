using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Common.Test
{
    public class ConcurrencyUtilitiesTests
    {
        [Fact]
        public async Task ConcurrencyUtilities_LockStress()
        {
            // Arrange
            var sem = new ManualResetEventSlim();
            var threads = 1000;
            var tasks = new Stack<Task<bool>>(threads);
            var path = Path.Combine(Path.GetTempPath(), "ConcurrencyUtilities_LockStress");
            var token = CancellationToken.None;
            Func<CancellationToken, Task<bool>> action = (ct) => {
                // Wait till all threads are ready
                sem.Wait();
                return Task.FromResult(true);
            };

            while (tasks.Count < threads)
            {
                var task = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<bool>(
                    path,
                    action,
                    token));

                tasks.Push(task);
            }

            // Act
            // Release all the threads at once
            sem.Set();
            await Task.WhenAll(tasks);

            // Assert
            while (tasks.Count > 0)
            {
                // Verify everything finished without errors
                Assert.True(await tasks.Pop());
            }
        }

        [Fact]
        public async Task ConcurrencyUtilities_LockAllCasings()
        {
            // Arrange
            var token = CancellationToken.None;
            var path1 = "/tmp/packageA/1.0.0";
            var path2 = "/tmp/packagea/1.0.0";
            var action1HitSem = new ManualResetEventSlim();
            var action1Sem = new ManualResetEventSlim();
            var action2Sem = new ManualResetEventSlim();

            Func<CancellationToken, Task<bool>> action1 = (ct) => {
                action1HitSem.Set();
                action1Sem.Wait();
                return Task.FromResult(true);
            };

            Func<CancellationToken, Task<bool>> action2 = (ct) =>
            {
                action2Sem.Set();
                return Task.FromResult(true);
            };

            // Act
            var task1 = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<bool>(
                path1,
                action1,
                token));

            var task2Started = action1HitSem.Wait(60 * 1000 * 5, token);
            Assert.True(task2Started);

            var task2 = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<bool>(
                path2,
                action2,
                token));

            // Wait 1s to verify that task2 has not started
            await Task.Delay(1000);

            var task2blocked = !action2Sem.IsSet;
            action1Sem.Set();

            await task1;
            var result = await task2;

            // Assert
            Assert.True(task2blocked);
            Assert.True(result);
        }

        [Fact]
        public async Task ConcurrencyUtilities_NormalizePaths()
        {
            // Arrange
            var token = CancellationToken.None;
            var path1 = "/tmp/packageA/1.0.0";
            var path2 = "/tmp/sub/../packageA/1.0.0";
            var action1HitSem = new ManualResetEventSlim();
            var action1Sem = new ManualResetEventSlim();
            var action2Sem = new ManualResetEventSlim();

            Func<CancellationToken, Task<bool>> action1 = (ct) => {
                action1HitSem.Set();
                action1Sem.Wait();
                return Task.FromResult(true);
            };

            Func<CancellationToken, Task<bool>> action2 = (ct) =>
            {
                action2Sem.Set();
                return Task.FromResult(true);
            };

            // Act
            var task1 = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<bool>(
                path1,
                action1,
                token));

            var task2Started = action1HitSem.Wait(60 * 1000 * 5, token);
            Assert.True(task2Started);

            var task2 = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<bool>(
                path2,
                action2,
                token));

            // Wait 1s to verify that task2 has not started
            await Task.Delay(1000);

            var task2blocked = !action2Sem.IsSet;
            action1Sem.Set();

            await task1;
            var result = await task2;

            // Assert
            Assert.True(task2blocked);
            Assert.True(result);
        }
    }
}
