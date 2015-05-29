// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
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
            // TODO: log somewhere?
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            // TODO: is this correct for the API?
            return FileConflictAction.OverwriteAll;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public bool SkipAssemblyReferences { get; }

        public bool BindingRedirectsDisabled { get; }

        public void ReportError(string message)
        {
            // no-op
            Debug.Fail(message);
        }
    }
}
