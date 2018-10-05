// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class NupkgMetadataFileTests
    {
        [Fact]
        public void NupkgMetadataFile_Equals()
        {
            Func<NupkgMetadataFile> getMetadataFile = () =>
            {
                var metadataFile = new NupkgMetadataFile()
                {
                    Version = 1,
                    ContentHash = "tempContentHash"
                };

                return metadataFile;
            };

            var self = getMetadataFile();
            var other = getMetadataFile();

            Assert.NotSame(self, other);
            Assert.Equal(self, other);
        }

        [Fact]
        public void NupkgMetadataFile_NotEquals()
        {
            Func<NupkgMetadataFile> getMetadataFile = () =>
            {
                var metadataFile = new NupkgMetadataFile()
                {
                    Version = 1,
                    ContentHash = "tempContentHash"
                };

                return metadataFile;
            };

            var self = getMetadataFile();
            var other = getMetadataFile();
            other.ContentHash = "contentHashChanged";

            Assert.NotSame(self, other);
            Assert.NotEqual(self, other);
        }
    }
}
