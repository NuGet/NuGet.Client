// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.Test;
using NuGet.Test.Utility;
using System;
using System.IO;
using System.Net;
using System.Threading;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    public class PushCommandTest
    {
        /// <summary>
        /// 100 seconds is significant because that is the default timeout on <see cref="HttpClient"/>.
        /// Related to https://github.com/NuGet/Home/issues/2785.
        /// </summary>
        [Fact]
        public void PushCommand_AllowsTimeoutToBeSpecifiedHigherThan100Seconds()
        {
            // Arrange
            using (var packageDirectory = TestDirectory.Create())
            {
                var nuget = Util.GetNuGetExePath();
                var sourcePath = Util.CreateTestPackage("PackageA", "1.1.0", packageDirectory);
                var outputPath = Path.Combine(packageDirectory, "pushed.nupkg");

                using (var server = new MockServer())
                {
                    server.Put.Add("/push", r =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(101));

                        byte[] buffer = MockServer.GetPushedPackage(r);
                        using (var outputStream = new FileStream(outputPath, FileMode.Create))
                        {
                            outputStream.Write(buffer, 0, buffer.Length);
                        }

                        return HttpStatusCode.Created;
                    });

                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 110",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    // Assert
                    server.Stop();
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Your package was pushed.", result.Item2);
                    Assert.True(File.Exists(outputPath), "The package should have been pushed");
                    Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(outputPath));
                }
            }
        }

        [Fact]
        public void PushCommand_AllowsTimeoutToBeSpecifiedLowerThan100Seconds()
        {
            // Arrange
            using (var packageDirectory = TestDirectory.Create())
            {
                var nuget = Util.GetNuGetExePath();
                var sourcePath = Util.CreateTestPackage("PackageA", "1.1.0", packageDirectory);
                var outputPath = Path.Combine(packageDirectory, "pushed.nupkg");

                using (var server = new MockServer())
                {
                    server.Put.Add("/push", r =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));

                        byte[] buffer = MockServer.GetPushedPackage(r);
                        using (var outputStream = new FileStream(outputPath, FileMode.Create))
                        {
                            outputStream.Write(buffer, 0, buffer.Length);
                        }

                        return HttpStatusCode.Created;
                    });

                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 1",
                        waitForExit: true,
                        timeOutInMilliseconds: 20 * 1000); // 20 seconds

                    // Assert
                    server.Stop();
                    Assert.True(1 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.DoesNotContain("Your package was pushed.", result.Item2);
                    Assert.False(File.Exists(outputPath), "The package should not have been pushed");
                }
            }
        }
    }
}
