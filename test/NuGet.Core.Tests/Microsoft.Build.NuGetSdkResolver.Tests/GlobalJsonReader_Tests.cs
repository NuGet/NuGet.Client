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
        public void EmptyVersionsAreIgnored()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var context = new MockSdkResolverContext(Path.Combine(testDirectory.Path, "foo.proj"));

                WriteGlobalJson(
                    testDirectory,
                    new Dictionary<string, string>
                    {
                        ["foo"] = "1.0.0",
                        ["bar"] = "",
                        ["baz"] = "  ",
                        ["bax"] = null
                    });

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().Equal(new Dictionary<string, string>
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
                    $"Failed to parse \"{globalJsonPath}\". Encountered unexpected character 'a'.");
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

        [Theory]
        [InlineData("one")]
        [InlineData("one", "two")]
        [InlineData("one", "two", "three")]
        public void TryGetPathOfFileAboveRecursive(params string[] directories)
        {
            const string filename = "test.txt";

            using (var testDirectory = TestDirectory.Create())
            {
                var paths = new List<string>
                {
                    testDirectory.Path
                };

                paths.AddRange(directories);

                var directory = new DirectoryInfo(Path.Combine(paths.ToArray()));

                directory.Create();

                var expected = Path.Combine(testDirectory.Path, filename);

                File.WriteAllText(expected, string.Empty);

                GlobalJsonReader.TryGetPathOfFileAbove(filename, directory, out string result).Should().BeTrue();

                result.Should().Be(expected);
            }
        }

        internal static string WriteGlobalJson(TestDirectory testDirectory, Dictionary<string, string> sdkVersions, string additionalContent = "")
        {
            var path = Path.Combine(testDirectory, GlobalJsonReader.GlobalJsonFileName);

            using (var writer = File.CreateText(path))
            {
                writer.WriteLine("{");
                if (sdkVersions != null)
                {
                    writer.WriteLine("    \"msbuild-sdks\": {");
                    writer.WriteLine(string.Join($",{Environment.NewLine}        ", sdkVersions.Select(i => $"\"{i.Key}\": {(i.Value == null ? "null" : $"\"{i.Value}\"")}")));
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
