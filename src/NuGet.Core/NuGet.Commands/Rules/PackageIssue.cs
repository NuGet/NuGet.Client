// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Commands.Rules
{
    public class PackageIssue
    {
        public string Description { get; }
        public LogLevel Level { get; }
        public string Solution { get; }
        public string Title { get; }

        public PackageIssue(string title, string description, string solution)
            : this(title, description, solution, LogLevel.Information)
        {
        }
        public PackageIssue(string title, string description, string solution, LogLevel level)
        {
            Title = title;
            Description = description;
            Solution = solution;
            Level = level;
        }
    }
}
