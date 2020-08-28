// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal static class ProjectUtility
    {
        internal static async ValueTask<IEnumerable<string>> GetProjectIdsAsync(
            IEnumerable<IProjectContextInfo> projects,
            CancellationToken cancellationToken)
        {
            var projectsWithTasks = projects.Select(project =>
                new
                {
                    Project = project,
                    UniqueNameTask = project.GetMetadataAsync<string>(
                            NuGetProjectMetadataKeys.UniqueName,
                            cancellationToken)
                        .AsTask(),
                    ProjectIdTask = project.GetMetadataAsync<string>(
                            NuGetProjectMetadataKeys.ProjectId,
                            cancellationToken)
                        .AsTask()
                });

            IEnumerable<Task<string>> tasks = projectsWithTasks.Select(projectWithTasks => projectWithTasks.UniqueNameTask)
                .Concat(projectsWithTasks.Select(projectWithTasks => projectWithTasks.ProjectIdTask));

            await Task.WhenAll(tasks);

            return projectsWithTasks
                .OrderBy(projectWithTasks => projectWithTasks.UniqueNameTask.Result)
                .Select(projectWithTasks => projectWithTasks.ProjectIdTask.Result);
        }
    }
}
