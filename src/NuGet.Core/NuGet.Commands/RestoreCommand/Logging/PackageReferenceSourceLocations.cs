// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    internal static class PackageReferenceSourceLocations
    {
        internal static void Apply(IEnumerable<TargetFrameworkInformation> frameworks, IRestoreLogMessage message)
        {
            (string File, int Line, int Column)? selected = null;
            if (message.LibraryId is not null && message.TargetGraphs.Count > 0)
            {
                foreach (string graph in message.TargetGraphs)
                {
                    var matches = frameworks.Where(f =>
                    {
                        string name = string.IsNullOrEmpty(f.TargetAlias) ? f.FrameworkName.ToString() : f.TargetAlias;
                        return StringComparer.OrdinalIgnoreCase.Equals(graph, name)
                            || graph.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase)
                            || message.Code == NuGetLogCode.NU1510 && StringComparer.OrdinalIgnoreCase.Equals(graph, f.FrameworkName.ToString());
                    }).ToList();
                    if (matches.Count != 1)
                    {
                        selected = null;
                        break;
                    }

                    var dependencies = matches[0].Dependencies.Where(d =>
                        StringComparer.OrdinalIgnoreCase.Equals(d.Name, message.LibraryId)).ToList();
                    if (dependencies.Count != 1 || dependencies[0] is not { AutoReferenced: false, ReferenceType: LibraryDependencyReferenceType.Direct } dependency
                        || dependency.VersionCentrallyManaged && message.Code != NuGetLogCode.NU1510
                        || dependency.SourceLocation is not { } location
                        || selected.HasValue && selected.Value != location)
                    {
                        selected = null;
                        break;
                    }

                    selected = location;
                }
            }

            message.FilePath = selected?.File ?? message.ProjectPath;
            message.StartLineNumber = selected?.Line ?? 0;
            message.StartColumnNumber = selected?.Column ?? 0;
            message.EndLineNumber = 0;
            message.EndColumnNumber = 0;
        }
    }
}
