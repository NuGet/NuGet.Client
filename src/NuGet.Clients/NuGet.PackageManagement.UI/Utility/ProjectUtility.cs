// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal static class ProjectUtility
    {
        internal static async ValueTask<IEnumerable<string>> GetSortedProjectIdsAsync(
            IEnumerable<IProjectContextInfo> projects,
            CancellationToken cancellationToken)
        {
            if (projects is null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<Task<IProjectMetadataContextInfo>> tasks = projects.Select(
                project => project.GetMetadataAsync(cancellationToken).AsTask());

            IProjectMetadataContextInfo[] projectMetadatas = await Task.WhenAll(tasks);

            return projectMetadatas
                .OrderBy(projectMetadata => projectMetadata.UniqueName)
                .Select(projectMetadata => projectMetadata.ProjectId);
        }
    }
}
