// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using NuGet.Shared;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests.Plugins
{
    public class PluginCacheEntryTests
    {
        [Fact]
        public void PluginCacheEntry_DoesNotThrowWithNoFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var entry = new PluginCacheEntry(testDirectory.Path, "a", "b");
                entry.LoadFromFile();
                Assert.Null(entry.OperationClaims);
            }
        }

        [Theory]
        [MemberData(nameof(GetsRoundTripsValuesData))]
        public async Task PluginCacheEntry_RoundTripsValuesAsync(string[] values)
        {
            var list = new List<OperationClaim>();
            foreach (var val in values)
            {
                Enum.TryParse(val, out OperationClaim result);
                list.Add(result);
            }

            using (var testDirectory = TestDirectory.Create())
            {
                var entry = new PluginCacheEntry(testDirectory.Path, "a", "b");
                entry.LoadFromFile();
                entry.AddOrUpdateOperationClaims(list);
                await entry.UpdateCacheFileAsync();

                var newEntry = new PluginCacheEntry(testDirectory.Path, "a", "b");
                newEntry.LoadFromFile();

                Assert.True(EqualityUtility.SequenceEqualWithNullCheck(entry.OperationClaims, newEntry.OperationClaims));
            }
        }

        [Fact]
        public async Task PluginCacheEntry_DoesNotDeleteAnOpenedFile()
        {
            var list = new List<OperationClaim>() { OperationClaim.Authentication };

            using (var testDirectory = TestDirectory.Create())
            {
                var entry = new PluginCacheEntry(testDirectory.Path, "a", "b");
                entry.LoadFromFile();
                entry.AddOrUpdateOperationClaims(list);
                await entry.UpdateCacheFileAsync();

                var CacheFileName = Path.Combine(Path.Combine(testDirectory.Path, CachingUtility.RemoveInvalidFileNameChars(CachingUtility.ComputeHash("a"))), CachingUtility.RemoveInvalidFileNameChars("b") + ".dat");

                Assert.True(File.Exists(CacheFileName));

                using (var fileStream = new FileStream(
                   CacheFileName,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None,
                   CachingUtility.BufferSize,
                   useAsync: true))
                {
                    list.Add(OperationClaim.DownloadPackage);
                    entry.AddOrUpdateOperationClaims(list);
                    await entry.UpdateCacheFileAsync(); // this should not update
                }

                entry.LoadFromFile();
                Assert.True(EqualityUtility.SequenceEqualWithNullCheck(entry.OperationClaims, new List<OperationClaim>() { OperationClaim.Authentication }));
            }
        }

        public static IEnumerable<object[]> GetsRoundTripsValuesData()
        {
            yield return new object[] { new string[] { "Authentication", "DownloadPackage" } };
            yield return new object[] { new string[] { "Authentication" } };
            yield return new object[] { new string[] { "DownloadPackage" } };
            yield return new object[] { new string[] { } };
        }
    }
}
