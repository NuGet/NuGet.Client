// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.Build.Tasks.Test
{
    /// <summary>
    /// MSBuild logger -> TestLogger
    /// </summary>
    public class TestBuildEngine : IBuildEngine
    {
        /// <summary>
        /// Test logger
        /// </summary>
        public TestLogger TestLogger = new TestLogger();

        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            return true;
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            // ignored
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            var message = new RestoreLogMessage(LogLevel.Error, e.Message)
            {
                FilePath = e.File,
                ProjectPath = e.ProjectFile
            };

            TestLogger.Log(message);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            var level = LogLevel.Debug;

            if (e.Importance == MessageImportance.High)
            {
                level = LogLevel.Minimal;
            }

            if (e.Importance == MessageImportance.Normal)
            {
                level = LogLevel.Information;
            }

            var message = new RestoreLogMessage(level, e.Message)
            {
                FilePath = e.File,
                ProjectPath = e.ProjectFile
            };

            TestLogger.Log(message);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            var message = new RestoreLogMessage(LogLevel.Warning, e.Message)
            {
                FilePath = e.File,
                ProjectPath = e.ProjectFile
            };

            TestLogger.Log(message);
        }
    }
}
