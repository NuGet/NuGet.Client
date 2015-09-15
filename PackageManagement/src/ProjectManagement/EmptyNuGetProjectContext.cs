// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using NuGet.Packaging;

namespace NuGet.ProjectManagement
{
    public class EmptyNuGetProjectContext : INuGetProjectContext
    {
        public void Log(MessageLevel level, string message, params object[] args)
        {
            // No-op
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return null; }
        }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
        }
    }
}
