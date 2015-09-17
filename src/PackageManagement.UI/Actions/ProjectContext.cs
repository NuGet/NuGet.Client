// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public sealed class ProjectContext : INuGetProjectContext
    {
        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            // TODO: log to the console
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            // TODO: prompt

            return FileConflictAction.Ignore;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public XDocument OriginalPackagesConfig { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return null; }
        }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public void ReportError(string message)
        {
        }
    }
}
