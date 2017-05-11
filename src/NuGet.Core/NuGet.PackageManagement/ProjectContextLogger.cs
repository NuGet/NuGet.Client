// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLogger : LoggerBase
    {
        private readonly INuGetProjectContext _projectContext;

        public ProjectContextLogger(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public override void Log(ILogMessage message)
        {
            if (DisplayMessage(message.Level))
            {
                var messageLevel = LogLevelToMessageLevel(message.Level);

                _projectContext.Log(messageLevel, message.Message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            if (DisplayMessage(message.Level))
            {
                var messageLevel = LogLevelToMessageLevel(message.Level);

                _projectContext.Log(messageLevel, message.Message);
            }

            return Task.FromResult(0);
        }

        private static MessageLevel LogLevelToMessageLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return MessageLevel.Error;

                case LogLevel.Warning:
                    return MessageLevel.Warning;

                case LogLevel.Information:
                case LogLevel.Minimal:
                    return MessageLevel.Info;

                default:
                    return MessageLevel.Debug;
            }
        }
    }
}
