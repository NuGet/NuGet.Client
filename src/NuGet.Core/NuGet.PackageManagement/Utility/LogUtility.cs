// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public static class LogUtility
    {
        public static MessageLevel LogLevelToMessageLevel(LogLevel level)
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
