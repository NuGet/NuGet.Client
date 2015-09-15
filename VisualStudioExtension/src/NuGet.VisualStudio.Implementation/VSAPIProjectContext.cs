// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Xml.Linq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    internal sealed class VSAPIProjectContext : IMSBuildNuGetProjectContext
    {
        public VSAPIProjectContext()
            : this(false, false, true)
        {
        }

        public VSAPIProjectContext(bool skipAssemblyReferences, bool bindingRedirectsDisabled, bool useLegacyInstallPaths = true)
        {
            PackageExtractionContext = new PackageExtractionContext();

            // many templates depend on legacy paths, for the VS API and template wizard we unfortunately need to keep them
            PackageExtractionContext.UseLegacyPackageInstallPath = useLegacyInstallPaths;

            SourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
            SkipAssemblyReferences = skipAssemblyReferences;
            BindingRedirectsDisabled = bindingRedirectsDisabled;
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            // No logging needed when using the API
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public bool SkipAssemblyReferences { get; }

        public bool BindingRedirectsDisabled { get; }

        public bool SkipBindingRedirects { get; set; }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
            // no-op
            Debug.Fail(message);
        }
    }
}
