// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    public class GlobalJsonReaderTests
    {
        public static string WriteGlobalJson(string directory, Dictionary<string, string> sdkVersions, string additionalcontent = "")
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

                if (!string.IsNullOrWhiteSpace(additionalcontent))
                {
                    writer.Write(additionalcontent);
                }

                writer.WriteLine("}");
            }

            return path;
        }

        [Fact]
        public void EmptyGlobalJson()
        {
            using (var testEnvironment = TestEnvironment.Create())
            {
                var folder = testEnvironment.CreateFolder().FolderPath;

                File.WriteAllText(Path.Combine(folder, GlobalJsonReader.GlobalJsonFileName), @" { } ");

                var context = new MockSdkResolverContext(Path.Combine(folder, "foo.proj"));

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().BeNull();
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

            using (var testEnvironment = TestEnvironment.Create())
            {
                var testFolder = testEnvironment.CreateFolder();
                var projectFile = testEnvironment.CreateFile(testFolder, ".proj");


                var globalJsonPath = WriteGlobalJson(testFolder.FolderPath, expectedVersions, additionalcontent: ", abc");

                var context = new MockSdkResolverContext(projectFile.Path);

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().BeNull();

                context.MockSdkLogger.LoggedMessages.Count.Should().Be(1);
                context.MockSdkLogger.LoggedMessages.First().Key.Should().Be(
                    $"Failed to parse \"{globalJsonPath}\". Invalid JavaScript property identifier character: }}. Path \'msbuild-sdks\', line 6, position 5.");
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
            using (var testEnvironment = TestEnvironment.Create())
            {
                var testFolder = testEnvironment.CreateFolder();
                var projectFile = testEnvironment.CreateFile(testFolder, ".proj");

                WriteGlobalJson(testFolder.FolderPath, expectedVersions);

                var context = new MockSdkResolverContext(projectFile.Path);

                GlobalJsonReader.GetMSBuildSdkVersions(context).Should().Equal(expectedVersions);
            }
        }
    }
}
