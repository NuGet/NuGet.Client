// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NuGet.Frameworks;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.RuntimeModel.Test
{
    public class JsonRuntimeFormatTests
    {
        private const string SimpleRuntimeGraphContent = "{\"runtimes\":{\"any\":{}}}";

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"runtimes\":{}}")]
        public void CanParseEmptyRuntimeJsons(string content)
        {
            Assert.Equal(RuntimeGraph.Empty, ParseRuntimeJsonString(content));
        }

        [Fact]
        public void CanParseSupportsSection()
        {
            const string content = @"
{
    ""supports"": {
        ""windows-frob"": {
            ""netcore50"": [ ""winfrob-x86"", ""winfrob-x64"" ]
        }
    }
}";
            Assert.Equal(
                new RuntimeGraph(new[]
                    {
                        new CompatibilityProfile("windows-frob", new []
                            {
                                new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.NetCore50, "winfrob-x86"),
                                new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.NetCore50, "winfrob-x64")
                            })
                    }),
                ParseRuntimeJsonString(content));
        }

        [Fact]
        public void CanParseSupportsAsFoundInProjectFiles()
        {
            const string content = @"
{
    ""supports"": {
        ""windows-frob"": {}
    }
}";
            Assert.Equal(
                new RuntimeGraph(new[]
                    {
                        new CompatibilityProfile("windows-frob")
                    }),
                ParseRuntimeJsonString(content));
        }

        [Fact]
        public void CanParseCompatProfilesWithoutRuntimeIDs()
        {
            const string content = @"
{
    ""supports"": {
        ""windows-phone-8"": {
            ""wp8"": """"
        }
    }
}";
            Assert.Equal(
                new RuntimeGraph(new[]
                    {
                        new CompatibilityProfile("windows-phone-8", new [] {
                            new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.WP8, null)
                        })
                    }),
                ParseRuntimeJsonString(content));
        }

        [Fact]
        public void CanParseSimpleRuntimeJson()
        {
            const string content = @"
{
    ""runtimes"": {
        ""any"": {},
        ""win8-x86"": {
            ""#import"": [
                ""win8"",
                ""win7-x86""
            ],
            ""Some.Package"": {
                ""Some.Package.For.win8-x86"": ""4.2""
            }
        },
        ""win8"": {
            ""#import"": [
                ""win7""
            ]
        }
    }
}";

            Assert.Equal(
                new RuntimeGraph(new[]
                    {
                        new RuntimeDescription("any"),
                        new RuntimeDescription("win8-x86", new[]
                            {
                                "win8",
                                "win7-x86"
                            }, new[]
                                {
                                    new RuntimeDependencySet("Some.Package", new[]
                                        {
                                            new RuntimePackageDependency("Some.Package.For.win8-x86", new VersionRange(new NuGetVersion("4.2")))
                                        })
                                }),
                        new RuntimeDescription("win8", new[] { "win7" })
                    }), ParseRuntimeJsonString(content));
        }

        [Fact]
        public void ReadRuntimeGraph_WithStream_ParsesRuntimeGraph()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleRuntimeGraphContent)))
            {
                Assert.Equal(CreateSimpleRuntimeGraph(), JsonRuntimeFormat.ReadRuntimeGraph(stream));
            }
        }

        [Fact]
        public void ReadRuntimeGraph_WithEnvironmentOptIn_ParsesWithSystemTextJson()
        {
            var environmentVariableReader = new TestEnvironmentVariableReader(
                new Dictionary<string, string>
                {
                    ["NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION"] = "true"
                });

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleRuntimeGraphContent)))
            {
                Assert.Equal(
                    CreateSimpleRuntimeGraph(),
                    JsonRuntimeFormat.ReadRuntimeGraph(stream, environmentVariableReader));
            }
        }

        [Fact]
        public void ReadRuntimeGraphWithSystemTextJson_WithTextReader_DisposesReader()
        {
            var reader = new StringReader(SimpleRuntimeGraphContent);

            JsonRuntimeFormat.ReadRuntimeGraphWithSystemTextJson(reader);

            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }

        [Fact]
        public void ReadRuntimeGraphWithSystemTextJson_WithUtf8Bom_ParsesRuntimeGraph()
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                bufferSize: 1024,
                leaveOpen: true))
            {
                writer.Write(SimpleRuntimeGraphContent);
            }
            stream.Position = 0;

            Assert.Equal(CreateSimpleRuntimeGraph(), JsonRuntimeFormat.ReadRuntimeGraphWithSystemTextJson(stream));
        }

        [Fact]
        public void ReadRuntimeGraphWithSystemTextJson_WithDuplicateProperties_UsesLastValue()
        {
            const string content = """
                {
                    "runtimes": {
                        "win": null,
                        "win": { "#import": [ "win8" ] }
                    }
                }
                """;
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            RuntimeGraph graph = JsonRuntimeFormat.ReadRuntimeGraphWithSystemTextJson(stream);

            RuntimeDescription runtime = Assert.Single(graph.Runtimes).Value;
            Assert.Equal("win8", Assert.Single(runtime.InheritedRuntimes));
        }

        [Fact]
        public void ReadRuntimeGraphWithSystemTextJson_WithDuplicateRootProperties_UsesLastValue()
        {
            const string content = """
                {
                    "runtimes": "invalid",
                    "runtimes": {
                        "win": { "#import": [ "win8" ] }
                    }
                }
                """;
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            RuntimeGraph graph = JsonRuntimeFormat.ReadRuntimeGraphWithSystemTextJson(stream);

            Assert.Equal("win", Assert.Single(graph.Runtimes).Key);
        }

        [Fact]
        public void ReadRuntimeGraph_WithCommentsAndTrailingCommas_ParsesRuntimeGraph()
        {
            const string content = """
                {
                    // Runtime identifiers
                    "runtimes": {
                        "any": {
                            "#import": [],
                        },
                    },
                }
                """;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                Assert.Equal(CreateSimpleRuntimeGraph(), JsonRuntimeFormat.ReadRuntimeGraphWithSystemTextJson(stream));
            }
        }

        private static RuntimeGraph CreateSimpleRuntimeGraph()
        {
            return new RuntimeGraph(new[] { new RuntimeDescription("any") });
        }

        private RuntimeGraph ParseRuntimeJsonString(string content)
        {
            using (var reader = new StringReader(content))
            {
                return JsonRuntimeFormat.ReadRuntimeGraph(reader);
            }
        }
    }
}
