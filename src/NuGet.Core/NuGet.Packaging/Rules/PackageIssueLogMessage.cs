// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    public class PackageIssueLogMessage : ILogMessage
    {
        public LogLevel Level { get; set; }
        public WarningLevel WarningLevel { get; set; }
        public NuGetLogCode Code { get; set; }
        public string Message { get; set; }
        public string ProjectPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTimeOffset Time { get; set; }

        public PackageIssueLogMessage(string message, NuGetLogCode code, WarningLevel warningLevel, LogLevel logLevel)
        {
            Code = code;
            WarningLevel = warningLevel;
            Level = logLevel;
            Time = DateTimeOffset.Now;
            Message = message;
        }
    }
}
