// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISourceControlManagerProvider))]
    internal sealed class VSSourceControlManagerProvider : ISourceControlManagerProvider
    {
        private readonly Lazy<EnvDTE.DTE> _dte;
        private readonly Lazy<IComponentModel> _componentModel;

        private const string TfsProviderName = "{4CA58AB2-18FA-4F8D-95D4-32DDF27D184C}";

        [ImportingConstructor]
        public VSSourceControlManagerProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _componentModel = new Lazy<IComponentModel>(
                () => serviceProvider.GetComponentModel());
            _dte = new Lazy<EnvDTE.DTE>(
                () => serviceProvider.GetDTE());
        }

        public SourceControlManager GetSourceControlManager()
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_dte.Value.SourceControl != null)
                    {
                        var sourceControl = (SourceControl2)_dte.Value.SourceControl;
                        if (sourceControl != null)
                        {
                            SourceControlBindings sourceControlBinding = null;
                            try
                            {
                                // Get the binding for this solution
                                sourceControlBinding = sourceControl.GetBindings(_dte.Value.Solution.FullName);
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

                            var tfsProviders = _componentModel.Value.GetExtensions<ITFSSourceControlManagerProvider>();
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
