// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGetVSExtension
{
    [Export(typeof(INuGetUIContextFactory))]
    internal class VisualStudioUIContextFactory : INuGetUIContextFactory
    {
        private readonly ISourceRepositoryProvider _repositoryProvider;
        private readonly ISolutionManager _solutionManager;
        private readonly IPackageRestoreManager _restoreManager;
        private readonly IOptionsPageActivator _optionsPage;
        private readonly ISettings _settings;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;

        [ImportingConstructor]
        public VisualStudioUIContextFactory([Import] ISourceRepositoryProvider repositoryProvider,
            [Import] ISolutionManager solutionManager,
            [Import] ISettings settings,
            [Import] IPackageRestoreManager packageRestoreManager,
            [Import] IOptionsPageActivator optionsPage,
            [Import] IDeleteOnRestartManager deleteOnRestartManager)
        {
            _repositoryProvider = repositoryProvider;
            _solutionManager = solutionManager;
            _restoreManager = packageRestoreManager;
            _optionsPage = optionsPage;
            _settings = settings;
            _deleteOnRestartManager = deleteOnRestartManager;
        }

        public INuGetUIContext Create(NuGetPackage package, IEnumerable<NuGetProject> projects)
        {
            if (projects == null
                || !projects.Any())
            {
                throw new ArgumentNullException("projects");
            }

            NuGetPackageManager packageManager = new NuGetPackageManager(
                _repositoryProvider,
                _settings,
                _solutionManager,
                _deleteOnRestartManager);
            UIActionEngine actionEngine = new UIActionEngine(_repositoryProvider, packageManager);

            return new VisualStudioUIContext(
                package,
                _repositoryProvider,
                _solutionManager,
                packageManager,
                actionEngine,
                _restoreManager,
                _optionsPage,
                projects);
        }
    }
}
