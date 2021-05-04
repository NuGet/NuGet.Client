// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    public class GlobalJsonReaderTests
    {
        [Fact]
        public void EmptyGlobalJson()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                File.WriteAllText(Path.Combine(testDirectory.Path, GlobalJsonReader.GlobalJsonFileName), @" { } ");

                var context = new MockSdkResolverContext(Path.Combine(testDirectory.Path, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().BeNull();
            }
        }

        [Fact]
        public void GlobalJsonWithComments()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                File.WriteAllText(
                    Path.Combine(testDirectory, GlobalJsonReader.GlobalJsonFileName),
                    @"{
  // This is a comment
  ""msbuild-sdks"": {
    /* This is another comment */
    ""foo"": ""1.0.0""
  }
}");

                var context = new MockSdkResolverContext(Path.Combine(testDirectory, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().BeEquivalentTo(new Dictionary<string, string>
                {
                    ["foo"] = "1.0.0"
                });
            }
        }

        [Fact]
        public void InvalidJsonLogsMessage()
        {
            var expectedVersions = new Dictionary<string, string>
            {
                {"foo", "1.0.0"},
                {"bar", "2.0.0"}
            };

            using (var testDirectory = TestDirectory.Create())
            {
                var globalJsonPath = WriteGlobalJson(testDirectory, expectedVersions, additionalContent: ", abc");

                var context = new MockSdkResolverContext(Path.Combine(testDirectory.Path, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().BeNull();

                context.MockSdkLogger.LoggedMessages.Count.Should().Be(1);
                context.MockSdkLogger.LoggedMessages.First().Key.Should().Be(
                    $"Failed to parse \"{globalJsonPath}\". Invalid JavaScript property identifier character: }}. Path 'msbuild-sdks', line 6, position 5.");
            }
        }

        [Fact]
        public void SdkVersionsAreSuccessfullyLoaded()
        {
            var expectedVersions = new Dictionary<string, string>
            {
                {"foo", "1.0.0"},
                {"bar", "2.0.0"}
            };

            using (var testDirectory = TestDirectory.Create())
            {
                WriteGlobalJson(testDirectory, expectedVersions);

                var context = new MockSdkResolverContext(Path.Combine(testDirectory.Path, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().Equal(expectedVersions);
            }
        }

        internal static string WriteGlobalJson(string directory, Dictionary<string, string> sdkVersions, string additionalContent = "")
        {
            var path = Path.Combine(directory, GlobalJsonReader.GlobalJsonFileName);

            using (var writer = File.CreateText(path))
            {
                writer.WriteLine("{");
                if (sdkVersions != null)
                {
                    writer.WriteLine("    \"msbuild-sdks\": {");
                    writer.WriteLine(string.Join($",{Environment.NewLine}        ", sdkVersions.Select(i => $"\"{i.Key}\": \"{i.Value}\"")));
                    writer.WriteLine("    }");
                }

                if (!string.IsNullOrWhiteSpace(additionalContent))
                {
                    writer.Write(additionalContent);
                }

                writer.WriteLine("}");
            }

            return path;
        }
    }
}
