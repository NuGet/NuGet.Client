// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace NuGet.Common.Test
{
    [Collection(LocalizedTestCollection.TestName)]
    public class CultureUtilityTests
    {
        [Fact]
        public void CultureUtility_DisablesLocalization()
        {
            try
            {
                // Arrange
                LocalizedTestCollection.EnsureInit();
                var german = new CultureInfo("de-DE");

                CultureInfo.DefaultThreadCurrentCulture = german;
                CultureInfo.DefaultThreadCurrentUICulture = german;

                // Act
                var localized = GetResourceOutput(() => { });
                var invariant = GetResourceOutput(CultureUtility.DisableLocalization);

                // Assert
                Assert.Equal("Über allen Gipfeln ist Ruh.", localized.MainThread);
                Assert.Equal("Über allen Gipfeln ist Ruh.", localized.ExistingThread);
                Assert.Equal("Über allen Gipfeln ist Ruh.", localized.NewThread);
                Assert.Equal("Over all the peaks is silence.", invariant.MainThread);
                Assert.Equal("Over all the peaks is silence.", invariant.ExistingThread);
                Assert.Equal("Over all the peaks is silence.", invariant.NewThread);
            }
            finally
            {
                LocalizedTestCollection.Reset();
            }
        }

        private LocalizedOutput GetResourceOutput(Action action)
        {
            // Prepare.
            var semaphore = new SemaphoreSlim(1);
            semaphore.Wait();

            string existingThreadOutput = null;
            var existingThread = new Thread(() =>
            {
                semaphore.Wait();
                existingThreadOutput = TestResource.Example;
            });

            // Act
            action();

            // Get the results.
            string newThreadOutput = null;
            var newThread = new Thread(() => newThreadOutput = TestResource.Example);

            string mainThreadOutput = TestResource.Example;

            existingThread.Start();
            newThread.Start();

            semaphore.Release();
            existingThread.Join();
            newThread.Join();

            return new LocalizedOutput
            {
                MainThread = mainThreadOutput,
                ExistingThread = existingThreadOutput,
                NewThread = newThreadOutput
            };
        }

        private class LocalizedOutput
        {
            public string MainThread { get; set; }
            public string ExistingThread { get; set; }
            public string NewThread { get; set; }
        }
    }
}
