// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace Msbuild.Integration.Test
{
    public class MsbuildIntegrationTestFixture : IDisposable
    {
        internal readonly string _testDir;
        private readonly Dictionary<string, string> _processEnvVars = new Dictionary<string, string>();

        public MsbuildIntegrationTestFixture()
        {
            _testDir = Directory.GetCurrentDirectory();
            _processEnvVars.Add("UseSharedCompilation", "false");
            _processEnvVars.Add("DOTNET_MULTILEVEL_LOOKUP", "0");
            _processEnvVars.Add("MSBUILDDISABLENODEREUSE ", "true");
        }

        /// <summary>
        /// msbuild.exe args
        /// </summary>
        internal CommandRunnerResult RunMsBuild(string workingDirectory, string args, bool ignoreExitCode = false)
        {

            var msBuildExe = Path.Combine(_testDir, "MSBuild.exe");
            var restoreDllPath = Path.Combine(_testDir, "NuGet.Build.Tasks.dll");
            var nugetRestoreTargetsPath = Path.Combine(_testDir, "NuGet.targets");
            // Uncomment to debug the msbuild call
            // _processEnvVars.Add("DEBUG_RESTORE_TASK", "true");
            _processEnvVars["UNIT_TEST_RESTORE_TASK"] = bool.TrueString;
            var result = CommandRunner.Run(msBuildExe,
                workingDirectory,
                $"/p:NuGetRestoreTargets={nugetRestoreTargetsPath} /p:RestoreTaskAssemblyFile={restoreDllPath} /p:ImportNuGetBuildTasksPackTargetsFromSdk=\"true\" {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);

            if (!ignoreExitCode)
            {
                Assert.True(result.ExitCode == 0, $"msbuild.exe {args} command failed with following log information :\n {result.AllOutput}");
            }

            return result;
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer or used by another process
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
            catch
            {

            }
        }
    }
}
