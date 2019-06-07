// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading;
using NuGet.CommandLine.Test;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    public class PushCommandTest
    {
        private const string MESSAGE_EXISTING_PACKAGE = "already exists at feed"; //Derived from resx: AddPackage_PackageAlreadyExists
        private const string MESSAGE_RESPONSE_NO_SUCCESS = "Response status code does not indicate success";
        private const string MESSAGE_PACKAGE_PUSHED = "Your package was pushed.";
        private const string TEST_PACKAGE_SHOULD_NOT_PUSH = "The package should not have been pushed";
        private const string TEST_PACKAGE_SHOULD_PUSH = "The package should have been pushed";
        private const string ADVERTISE_SKIPDUPLICATE_OPTION = "See help for push option to automatically skip duplicates.";

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
                    Assert.Contains(MESSAGE_PACKAGE_PUSHED, result.Item2);
                    Assert.True(File.Exists(outputPath), TEST_PACKAGE_SHOULD_PUSH);
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
                    Assert.DoesNotContain(MESSAGE_PACKAGE_PUSHED, result.Item2);
                    Assert.False(File.Exists(outputPath), TEST_PACKAGE_SHOULD_NOT_PUSH);
                }
            }
        }

        [Fact]
        public void PushCommand_Server_SkipDuplicate_NotSpecified_PushHalts()
        {
            // Arrange
            using (var packageDirectory = TestDirectory.Create())
            {
                var nuget = Util.GetNuGetExePath();
                var sourcePath = Util.CreateTestPackage("PackageA", "1.1.0", packageDirectory);
                var outputPath = Path.Combine(packageDirectory, "pushed.nupkg");

                var sourcePath2 = Util.CreateTestPackage("PackageB", "1.1.0", packageDirectory);
                var outputPath2 = Path.Combine(packageDirectory, "pushed2.nupkg");

                using (var server = new MockServer())
                {
                    SetupMockServerForSkipDuplicate(server,
                                                      FuncOutputPath_SwitchesOnThirdPush(outputPath, outputPath2),
                                                      FuncStatusDuplicate_OccursOnSecondPush());

                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 110",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    //Run again so that it will be a duplicate push.
                    var result2 = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 110",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    var result3 = CommandRunner.Run(
                       nuget,
                       packageDirectory,
                       $"push {sourcePath2} -Source {server.Uri}push -Timeout 110",
                       waitForExit: true,
                       timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    // Assert
                    server.Stop();
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains(MESSAGE_PACKAGE_PUSHED, result.Item2);
                    Assert.True(File.Exists(outputPath), TEST_PACKAGE_SHOULD_PUSH);
                    Assert.DoesNotContain(MESSAGE_RESPONSE_NO_SUCCESS, result.AllOutput);
                    Assert.DoesNotContain(MESSAGE_EXISTING_PACKAGE, result.AllOutput);
                    Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(outputPath));

                    // Second run of command is the duplicate.
                    Assert.False(0 == result2.Item1, result2.AllOutput);
                    Assert.Contains(MESSAGE_RESPONSE_NO_SUCCESS, result2.AllOutput);
                    Assert.DoesNotContain(MESSAGE_EXISTING_PACKAGE, result2.AllOutput);
                    Assert.Contains(ADVERTISE_SKIPDUPLICATE_OPTION, result2.AllOutput);
                    Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(outputPath));
                }
            }
        }

        [Fact]
        public void PushCommand_Server_SkipDuplicate_IsSpecified_PushProceeds()
        {
            // Arrange
            using (var packageDirectory = TestDirectory.Create())
            {
                var nuget = Util.GetNuGetExePath();
                var sourcePath = Util.CreateTestPackage("PackageA", "1.1.0", packageDirectory);
                var outputPath = Path.Combine(packageDirectory, "pushed.nupkg");

                var sourcePath2 = Util.CreateTestPackage("PackageB", "1.1.0", packageDirectory);
                var outputPath2 = Path.Combine(packageDirectory, "pushed2.nupkg");

                using (var server = new MockServer())
                {
                    SetupMockServerForSkipDuplicate(server,
                                                      FuncOutputPath_SwitchesOnThirdPush(outputPath, outputPath2),
                                                      FuncStatusDuplicate_OccursOnSecondPush());

                    server.Start();

                    // Act
                    var result = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 110 -SkipDuplicate",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    //Run again so that it will be a duplicate push but use the option to skip duplicate packages.
                    var result2 = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath} -Source {server.Uri}push -Timeout 110 -SkipDuplicate",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    //Third run with a different package.
                    var result3 = CommandRunner.Run(
                        nuget,
                        packageDirectory,
                        $"push {sourcePath2} -Source {server.Uri}push -Timeout 110 -SkipDuplicate",
                        waitForExit: true,
                        timeOutInMilliseconds: 120 * 1000); // 120 seconds

                    // Assert
                    server.Stop();
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains(MESSAGE_PACKAGE_PUSHED, result.AllOutput);
                    Assert.True(File.Exists(outputPath), TEST_PACKAGE_SHOULD_PUSH);
                    Assert.DoesNotContain(MESSAGE_RESPONSE_NO_SUCCESS, result.AllOutput);
                    Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(outputPath));

                    // Second run of command is the duplicate.
                    Assert.True(0 == result2.Item1, result2.AllOutput);
                    Assert.DoesNotContain(MESSAGE_PACKAGE_PUSHED, result2.AllOutput);
                    Assert.Contains(MESSAGE_EXISTING_PACKAGE, result2.AllOutput);
                    Assert.DoesNotContain(MESSAGE_RESPONSE_NO_SUCCESS, result2.AllOutput);

                    // Third run after a duplicate should be successful with the SkipDuplicate flag.
                    Assert.True(0 == result3.Item1, $"{result3.Item2} {result3.Item3}");
                    Assert.Contains(MESSAGE_PACKAGE_PUSHED, result3.AllOutput);
                    Assert.True(File.Exists(outputPath2), TEST_PACKAGE_SHOULD_PUSH);

                    Assert.Equal(File.ReadAllBytes(sourcePath2), File.ReadAllBytes(outputPath2));
                }
            }
        }

        #region Helpers
        /// <summary>
        /// Sets up the server for the steps of running 3 Push commands. First is the initial push, followed by a duplicate push, followed by a new package push.
        /// Depending on the options of the push, the duplicate will either be a warning or an error and permit or prevent the third push.
        /// </summary>
        /// <param name="server">Server object to modify.</param>
        /// <param name="outputPathFunc">Function to determine path to output package.</param>
        /// <param name="responseCodeFunc">Function to determine which HttpStatusCode to return.</param>
        private static void SetupMockServerForSkipDuplicate(MockServer server,
                                                              Func<int, string> outputPathFunc,
                                                              Func<int, HttpStatusCode> responseCodeFunc)
        {
            int packageCounter = 0;
            server.Put.Add("/push", (Func<HttpListenerRequest, object>)(r =>
            {
                packageCounter++;

                byte[] buffer = MockServer.GetPushedPackage(r);

                var outputPath = outputPathFunc(packageCounter);

                using (var outputStream = new FileStream((string)outputPath, FileMode.Create))
                {
                    outputStream.Write(buffer, 0, buffer.Length);
                }

                return responseCodeFunc(packageCounter);
            }));
        }

        /// <summary>
        /// Switches to the second path on the 3rd count.
        /// </summary>
        private static Func<int, string> FuncOutputPath_SwitchesOnThirdPush(string outputPath, string outputPath2)
        {
            return (count) =>
            {
                if (count >= 3)
                {
                    return outputPath2;
                }
                return outputPath;
            };
        }

        private static Func<int, string> FuncOutputPath_UnchangedAlways(string outputPath)
        {
            return (count) =>
            {
                return outputPath;
            };
        }

        /// <summary>
        /// Status is Created except for 2nd count which is fixed as a Conflict.
        /// </summary>
        private static Func<int, HttpStatusCode> FuncStatusDuplicate_OccursOnSecondPush()
        {
            return (count) =>
            {
                //Second run will be treated as duplicate.
                if (count == 2)
                {
                    return HttpStatusCode.Conflict;
                }
                else
                {
                    return HttpStatusCode.Created;
                }
            };
        }

        #endregion
    }
}
