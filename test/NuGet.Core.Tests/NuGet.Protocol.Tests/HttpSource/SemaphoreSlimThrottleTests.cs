// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class SemaphoreSlimThrottleTests
    {
        [Fact]
        public async Task SemaphoreSlimThrottle_RespectsInnerSemaphore()
        {
            // Arrange
            var semaphoreSlim = new SemaphoreSlim(2);
            var target = new SemaphoreSlimThrottle(semaphoreSlim);

            // Act
            await target.WaitAsync();
            var countA = semaphoreSlim.CurrentCount;
            await target.WaitAsync();
            var countB = semaphoreSlim.CurrentCount;
            target.Release();
            var countC = semaphoreSlim.CurrentCount;
            target.Release();
            var countD = semaphoreSlim.CurrentCount;

            // Assert
            Assert.Equal(1, countA);
            Assert.Equal(0, countB);
            Assert.Equal(1, countC);
            Assert.Equal(2, countD);
        }

        [Fact]
        public async Task SemaphoreSlimThrottle_CreateBinarySemaphore_HasInitialCountOfOne()
        {
            // Arrange
            var target = SemaphoreSlimThrottle.CreateBinarySemaphore();
            await target.WaitAsync();

            // Act
            var task = Task.Run(target.WaitAsync);
            var acquiredBeforeRelease = task.Wait(TimeSpan.FromMilliseconds(10));
            target.Release();
            var acquiredAfterRelease = task.Wait(TimeSpan.FromSeconds(10));

            // Assert
            Assert.False(acquiredBeforeRelease, "The binary semaphore should only allow a count of one.");
            Assert.True(acquiredAfterRelease, "The binary semaphore should have released back to a count of one.");
        }
    }
}
