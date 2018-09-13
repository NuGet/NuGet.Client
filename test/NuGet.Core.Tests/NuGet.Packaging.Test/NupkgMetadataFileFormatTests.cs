// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class NupkgMetadataFileFormatTests
    {
        [Fact]
        public void NupkgMetadataFileFormat_Read()
        {
            var nupkgMetadataFileContent = @"{
                ""version"": 1,
                ""contentHash"": ""NhfNp80eWq5ms7fMrjuRqpwhL1H56IVzXF9d+OIDcEfQ92m1DyE0c+ufUE1ogB09+sYLd58IO4eJ8jyn7AifbA==""
            }";

            using (var reader = new StringReader(nupkgMetadataFileContent))
            {
                var file = NupkgMetadataFileFormat.Read(reader, NullLogger.Instance, string.Empty);

                Assert.NotNull(file);
                Assert.Equal(1, file.Version);
                Assert.Equal("NhfNp80eWq5ms7fMrjuRqpwhL1H56IVzXF9d+OIDcEfQ92m1DyE0c+ufUE1ogB09+sYLd58IO4eJ8jyn7AifbA==", file.ContentHash);
            }
        }

        [Fact]
        public void NupkgMetadataFileFormat_Write()
        {
            var nupkgMetadataFileContent = @"{
                ""version"": 1,
                ""contentHash"": ""NhfNp80eWq5ms7fMrjuRqpwhL1H56IVzXF9d+OIDcEfQ92m1DyE0c+ufUE1ogB09+sYLd58IO4eJ8jyn7AifbA==""
            }";

            NupkgMetadataFile metadataFile = null;

            using (var reader = new StringReader(nupkgMetadataFileContent))
            {
                metadataFile = NupkgMetadataFileFormat.Read(reader, NullLogger.Instance, string.Empty);
            }

            using (var writer = new StringWriter())
            {
                NupkgMetadataFileFormat.Write(writer, metadataFile);
                var output = JObject.Parse(writer.ToString());
                var expected = JObject.Parse(nupkgMetadataFileContent);

                Assert.Equal(expected.ToString(), output.ToString());
            }
        }
    }
}
