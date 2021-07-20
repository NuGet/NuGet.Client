// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Evaluation;

namespace NuGet.CommandLine.XPlat
{
    internal class ProjectCollectionAdapter : IProjectCollectionAdapter
    {
        public void UnloadProject(Project project) =>
            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
    }
}
