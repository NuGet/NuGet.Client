// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Xunit;

namespace NuGet.Packaging.Test.PackageExtraction
{
    public class ZipArchiveExtensionsTests
    {
        [Fact]
        public void UpdateFileTimeFromEntry_FileBusyForShortTime_Retries()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            using CancellationTokenSource cts = new();
            FileStream fileStream = null;
            try
            {
                using MemoryStream memoryStream = new();
                using ZipArchive zipArchive = new(memoryStream, ZipArchiveMode.Create);

                ZipArchiveEntry zipEntry = zipArchive.CreateEntry("test.file");
                DateTime expectedTime = DateTime.UtcNow.AddHours(-5);
                zipEntry.LastWriteTime = expectedTime;

                fileStream = File.Open(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                Task avSimulation = Task.Run(async () =>
                {
                    await Task.Delay(5, cts.Token);
                    fileStream.Dispose();
                });

                // Act
                zipEntry.UpdateFileTimeFromEntry(tempFile, NullLogger.Instance);

                // Assert
                var fileLastWriteTime = File.GetLastWriteTimeUtc(tempFile);
                Assert.Equal(expectedTime, fileLastWriteTime);
            }
            finally
            {
                fileStream?.Dispose();
                cts.Cancel();
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task UpdateFileTimeFromEntry_FileBusyForLongTime_Throws()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            try
            {
                using MemoryStream memoryStream = new();
                using ZipArchive zipArchive = new(memoryStream, ZipArchiveMode.Create);

                ZipArchiveEntry zipEntry = zipArchive.CreateEntry("test.file");
                DateTime expectedTime = DateTime.UtcNow.AddHours(-5);
                zipEntry.LastWriteTime = expectedTime;

                using FileStream fileStream = File.Open(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

                // Act & Assert
                await Assert.ThrowsAsync<IOException>(async () =>
                {
                    Task task = Task.Run(() => zipEntry.UpdateFileTimeFromEntry(tempFile, NullLogger.Instance));

                    // If the above deadlocks until the file is unlocked, then we need another way to fail the test within a reasonable time.
                    using CancellationTokenSource cts = new();
                    Task delay = Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                    var completed = await Task.WhenAny(task, delay);
                    cts.Cancel();
                    if (completed == delay)
                    {
                        throw new TimeoutException();
                    }
                    await completed;
                });
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
