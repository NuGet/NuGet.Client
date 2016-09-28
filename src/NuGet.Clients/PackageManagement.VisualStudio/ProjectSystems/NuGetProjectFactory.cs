// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Composite provider chaining calls to other providers.
    /// </summary>
    public class NuGetProjectFactory : IProjectSystemProvider
    {
        private readonly IProjectSystemProvider[] _providers;

        public NuGetProjectFactory(IEnumerable<IProjectSystemProvider> providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            _providers = providers.ToArray();
        }

        public bool TryCreateNuGetProject(EnvDTE.Project dteProject, ProjectSystemProviderContext context, out NuGetProject result)
        {
            if (dteProject == null)
            {
                throw new ArgumentNullException(nameof(dteProject));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            result = _providers
                .Select(p =>
                {
                    try
                    {
                        NuGetProject nuGetProject;
                        if (p.TryCreateNuGetProject(dteProject, context, out nuGetProject))
                        {
                            return nuGetProject;
                        }
                    }
                    catch
                    {
                        // Ignore failures. If this method returns null, the problem falls 
                        // into one of the other NuGet project types.
                    }

                    return null;
                })
                .FirstOrDefault(p => p != null);

            return result != null;
        }
    }
}
