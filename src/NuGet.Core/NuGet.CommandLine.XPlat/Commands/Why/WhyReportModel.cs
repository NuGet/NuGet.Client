// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.Commands.Why
{
    internal class WhyReportModel
    {
        internal string ProjectName { get; }
        internal string TargetPackage { get; }
        internal WhyCommandArgs WhyCommandArgs { get; }
        internal Dictionary<string, List<DependencyNode>?> DependencyGraphPerFramework { get; }

        internal WhyReportModel(
            string projectName,
            string targetPackage,
            WhyCommandArgs whyCommandArgs,
            Dictionary<string, List<DependencyNode>?>? dependencyGraphPerFramework)
        {
            ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
            TargetPackage = targetPackage ?? throw new ArgumentNullException(nameof(targetPackage));
            WhyCommandArgs = whyCommandArgs ?? throw new ArgumentNullException(nameof(whyCommandArgs));
            DependencyGraphPerFramework = dependencyGraphPerFramework ?? [];
        }
    }
}
