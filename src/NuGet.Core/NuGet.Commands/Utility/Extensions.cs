// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    /// <summary>
    /// Internal extension helpers for NuGet.Commands
    /// </summary>
    internal static class Extensions
    {
        public static ISet<LibraryDependency> GetAllPackageDependencies(this PackageSpec project)
        {
            // Remove non-package dependencies such as framework assembly references.
            return new HashSet<LibraryDependency>(
                project.Dependencies.Concat(project.TargetFrameworks.SelectMany(e => e.Dependencies))
                                    .Where(e => e.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)));
        }

        public static ISet<LibraryDependency> GetPackageDependenciesForFramework(this PackageSpec project, NuGetFramework framework)
        {
            // Remove non-package dependencies such as framework assembly references.
            return new HashSet<LibraryDependency>(
                project.Dependencies.Concat(project.GetTargetFramework(framework).Dependencies)
                                    .Where(e => e.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)));
        }

        /// <summary>
        /// Search on Key.Name for the given package/project id.
        /// </summary>
        public static GraphItem<RemoteResolveResult> GetItemById(this IEnumerable<GraphItem<RemoteResolveResult>> items, string id)
        {
            return items.FirstOrDefault(e => e.Key.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Log all messages.
        /// </summary>
        public static Task LogMessagesAsync(this ILogger logger, params ILogMessage[] messages)
        {
            return logger.LogMessagesAsync(messages);
        }

        /// <summary>
        /// Log all messages.
        /// </summary>
        public static async Task LogMessagesAsync(this ILogger logger, IEnumerable<ILogMessage> messages)
        {
            foreach (var message in messages)
            {
                await logger.LogAsync(message);
            }
        }
    }
}
