// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class VersionListSourceMapTests
    {
        [Fact]
        public void Record_WhenOpenedFromDisk_StoresHttpCache()
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromDisk);

            Assert.True(map.TryGetValue("x", out VersionListFetchKind kind));
            Assert.Equal(VersionListFetchKind.HttpCache, kind);
            Assert.Equal(1, map.Count);
        }

        [Theory]
        [InlineData(HttpSourceResultStatus.OpenedFromNetwork)]
        [InlineData(HttpSourceResultStatus.NotFound)]
        [InlineData(HttpSourceResultStatus.NoContent)]
        public void Record_WhenNotOpenedFromDisk_StoresNetwork(HttpSourceResultStatus status)
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "x", status);

            Assert.True(map.TryGetValue("x", out VersionListFetchKind kind));
            Assert.Equal(VersionListFetchKind.Network, kind);
        }

        [Fact]
        public void Record_WhenNetworkFollowsHttpCache_StoresNetwork()
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromDisk);
            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromNetwork);

            Assert.True(map.TryGetValue("x", out VersionListFetchKind kind));
            Assert.Equal(VersionListFetchKind.Network, kind);
            Assert.Equal(1, map.Count);
        }

        [Fact]
        public void Record_WhenHttpCacheFollowsNetwork_KeepsNetwork()
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromNetwork);
            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromDisk);

            Assert.True(map.TryGetValue("x", out VersionListFetchKind kind));
            Assert.Equal(VersionListFetchKind.Network, kind);
        }

        [Fact]
        public void Record_WhenSameIdDiffersByCase_SharesEntry()
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "x", HttpSourceResultStatus.OpenedFromDisk);
            VersionListSourceMap.Record(map, "X", HttpSourceResultStatus.OpenedFromNetwork);

            Assert.Equal(1, map.Count);
            Assert.True(map.TryGetValue("x", out VersionListFetchKind kind));
            Assert.Equal(VersionListFetchKind.Network, kind);
        }

        [Fact]
        public void Record_WhenTwoIds_DoesNotMergeKinds()
        {
            var map = NewMap();

            VersionListSourceMap.Record(map, "a", HttpSourceResultStatus.OpenedFromDisk);
            VersionListSourceMap.Record(map, "b", HttpSourceResultStatus.OpenedFromNetwork);

            Assert.True(map.TryGetValue("a", out VersionListFetchKind kindA));
            Assert.True(map.TryGetValue("b", out VersionListFetchKind kindB));
            Assert.Equal(VersionListFetchKind.HttpCache, kindA);
            Assert.Equal(VersionListFetchKind.Network, kindB);
            Assert.Equal(2, map.Count);
        }

        private static ConcurrentDictionary<string, VersionListFetchKind> NewMap()
        {
            return new ConcurrentDictionary<string, VersionListFetchKind>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
