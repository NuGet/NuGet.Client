// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISourceControlManagerProvider))]
    public class VSSourceControlManagerProvider : ISourceControlManagerProvider
    {
        private readonly DTE _dte;
        private readonly IComponentModel _componentModel;
        private const string TfsProviderName = "{4CA58AB2-18FA-4F8D-95D4-32DDF27D184C}";

        [ImportingConstructor]
        public VSSourceControlManagerProvider()
            : this(ServiceLocator.GetInstance<DTE>(),
                ServiceLocator.GetGlobalService<SComponentModel, IComponentModel>())
        {
        }

        public VSSourceControlManagerProvider(DTE dte, IComponentModel componentModel)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }

            if (componentModel == null)
            {
                throw new ArgumentNullException("componentModel");
            }

            _componentModel = componentModel;
            _dte = dte;
        }

        public SourceControlManager GetSourceControlManager()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_dte != null
                        && _dte.SourceControl != null)
                    {
                        var sourceControl = (SourceControl2)_dte.SourceControl;
                        if (sourceControl != null)
                        {
                            SourceControlBindings sourceControlBinding = null;
                            try
                            {
                                // Get the binding for this solution
                                sourceControlBinding = sourceControl.GetBindings(_dte.Solution.FullName);
                            }
                            catch (NotImplementedException)
                            {
                                // Some source control providers don't bother to implement this.
                                // TFS might be the only one using it
                            }

                            if (sourceControlBinding == null
                                || String.IsNullOrEmpty(sourceControlBinding.ProviderName)
                                ||
                                !sourceControlBinding.ProviderName.Equals(TfsProviderName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Return null, if the Source control provider is not TFS
                                return null;
                            }

                            var tfsProviders = _componentModel.GetExtensions<ITFSSourceControlManagerProvider>();
                            if (tfsProviders != null
                                && tfsProviders.Any())
                            {
                                return tfsProviders.Select(provider => provider.GetTFSSourceControlManager(sourceControlBinding))
                                    .FirstOrDefault(tp => tp != null);
                            }
                        }
                    }

                    return null;
                });
        }
    }
}
