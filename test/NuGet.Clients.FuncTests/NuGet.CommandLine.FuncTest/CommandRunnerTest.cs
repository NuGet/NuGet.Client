// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.FuncTest
{
    public class CommandRunnerTest
    {
        [Fact]
        public async Task Run_AlwaysCaptureStdout()
        {
            // Arrange
            var tasks = Enumerable
                .Range(0, 8)
                .Select(taskIndex =>
                {
                    return Task.Run(() =>
                    {
                        for (int i = 0; i < 1000; i++)
                        {
                            VerifyWithCommandRunner();
                        }

                        return true;
                    });
                })
                .ToList();

            // Act
            var successes = await Task.WhenAll(tasks);

            // Assert
            Assert.All(successes, Assert.True);
        }

        private static void VerifyWithCommandRunner()
        {
            // Arrange
            var expected = Guid.NewGuid().ToString();

            string fileName;
            string args;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                fileName = @"C:\Windows\System32\cmd.exe";
                args = $"/c echo {expected}";
            }
            else
            {
                fileName = "/bin/echo";
                args = expected;

            }

            // Act
            var result = CommandRunner.Run(
                fileName,
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Assert.Equal(0, result.Item1);
            Assert.Contains(expected, result.Item2);
        }
    }
}
