// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class RenderProblem
    {
        public string Project { get; private set; }
        public string Message { get; private set; }
        public RenderProblem(string project, string message)
        {
            Project = project;
            Message = message;
        }
    }
}
