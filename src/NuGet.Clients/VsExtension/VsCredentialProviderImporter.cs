﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.Credentials;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    /// <summary>
    /// Find all MEF imports for IVsCredentialProvider, and handle inserting fallback provider
    /// for Dev14
    /// </summary>
    public class VsCredentialProviderImporter
    {
        private readonly DTE _dte;
        private readonly Action<Exception, string> _errorDelegate;
        private readonly Func<ICredentialProvider> _fallbackProviderFactory;
        private readonly Action _initializer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dte">DTE instance, used to determine the Visual Studio version.</param>
        /// <param name="fallbackProviderFactory">Factory method used to create a fallback provider for
        /// Dev14 in case a VSTS credential provider can not be imported.</param>
        /// <param name="errorDelegate">Used to write error messages to the user.</param>
        public VsCredentialProviderImporter(
            DTE dte, 
            Func<ICredentialProvider> fallbackProviderFactory,
            Action<Exception, string> errorDelegate)
            : this(dte, fallbackProviderFactory, errorDelegate, initializer: null)
        {
        }

        /// <summary>
        /// Constructor, allows changing initializer Action for testing purposes
        /// </summary>
        /// <param name="dte">DTE instance, used to determine the Visual Studio version.</param>
        /// <param name="fallbackProviderFactory">Factory method used to create a fallback provider for
        /// Dev14 in case a VSTS credential provider can not be imported.</param>
        /// <param name="errorDelegate">Used to write error messages to the user.</param>
        /// <param name="initializer">Init method used to supply MEF imports. Should only
        /// be supplied by tests.</param>
        public VsCredentialProviderImporter (
            DTE dte,
            Func<ICredentialProvider> fallbackProviderFactory,
            Action<Exception, string> errorDelegate,
            Action initializer)
        {
            if (dte == null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            if (fallbackProviderFactory == null)
            {
                throw new ArgumentNullException(nameof(fallbackProviderFactory));
            }

            if (errorDelegate == null)
            {
                throw new ArgumentNullException(nameof(errorDelegate));
            }

            _dte = dte;
            _errorDelegate = errorDelegate;
            _fallbackProviderFactory = fallbackProviderFactory;
            _initializer = initializer ?? Initialize;
        }

        [ImportMany(typeof(IVsCredentialProvider))]
        public IEnumerable<Lazy<IVsCredentialProvider>> ImportedProviders { get; set; }

        /// <summary>
        /// Plugin providers are entered loaded the same way as other nuget extensions,
        /// matching any extension named CredentialProvider.*.exe.
        /// </summary>
        /// <returns>An enumeration of plugin providers</returns>
        public IReadOnlyCollection<ICredentialProvider> GetProviders()
        {
            var results = new List<ICredentialProvider>();

            _initializer();

            if (ImportedProviders != null)
            {
                foreach (var importedProviderFactory in ImportedProviders)
                {
                    try
                    {
                        var importedProvider = importedProviderFactory.Value;
                        results.Add(new VsCredentialProviderAdapter(importedProvider));
                    }
                    catch (Exception exception)
                    {
                        var targetAssemblyPath = exception.TargetSite.DeclaringType.Assembly.Location;

                        _errorDelegate(
                            exception,
                            string.Format(Resources.CredentialProviderFailed_ImportedProvider, targetAssemblyPath)
                            );
                    }
                }
            }

            // Dev15 + will provide a credential provider for VSTS.
            // If we are in Dev14, and no imported VSTS provider is found,
            // then fallback on the built-in VisualStudioAccountProvider
            if (IsDev14 && !HasImportedVstsProvider(results))
            {
                try
                {
                    var fallbackProvider = _fallbackProviderFactory();
                    if (fallbackProvider != null)
                    {
                        results.Add(fallbackProvider);
                    }
                }
                catch (Exception exception)
                {
                    _errorDelegate(exception, Resources.CredentialProviderFailed_VisualStudioAccountProvider);
                }
            }
            
            // Ensure imported providers ordering is deterministic
            results.Sort((a, b) => a.GetType().FullName.CompareTo(b.GetType().FullName));

            return results;
        }

        private static bool HasImportedVstsProvider(IEnumerable<ICredentialProvider> results)
        {
            return results.FirstOrDefault(p => p.Id.EndsWith(
                ".NuGetCredentialProvider.VisualStudioAccountProvider",
                StringComparison.OrdinalIgnoreCase)) != null;
        }

        private void Initialize()
        {
            var componentModel = ServiceLocator.GetGlobalService<SComponentModel, IComponentModel>();

            // ensure we satisfy our imports and access DTE on the UI thread
            NuGetUIThreadHelper.JoinableTaskFactory.Run(
                async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    componentModel?.DefaultCompositionService.SatisfyImportsOnce(this);
                    Version = _dte.Version;
                });
        }

        public string Version { get; set; } = string.Empty;

        private bool IsDev14 => Version.StartsWith("14.");
    }
}
