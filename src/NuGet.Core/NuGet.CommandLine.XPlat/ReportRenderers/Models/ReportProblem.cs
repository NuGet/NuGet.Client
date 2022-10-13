// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat
{
    internal class ReportProblem
    {
        public string Project { get; private set; }
        public string Message { get; private set; }
        public ProblemType ProblemType { get; }

        public ReportProblem(string project, string message, ProblemType problemType)
        {
            Project = project;
            Message = message;
            ProblemType = problemType;
        }
    }
}
