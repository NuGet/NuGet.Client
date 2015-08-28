// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestNuGetProjectContext : IMSBuildNuGetProjectContext
    {
        public TestExecutionContext TestExecutionContext { get; set; }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; set; }

        public ExecutionContext ExecutionContext
        {
            get { return TestExecutionContext; }
        }

        public bool SkipAssemblyReferences { get; set; }

        public bool BindingRedirectsDisabled { get; set; }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
        }
    }

    public class TestExecutionContext : ExecutionContext
    {
        public TestExecutionContext(PackageIdentity directInstall)
        {
            FilesOpened = new HashSet<string>();
            DirectInstall = directInstall;
        }

        public HashSet<string> FilesOpened { get; }

        public override Task OpenFile(string fullPath)
        {
            FilesOpened.Add(fullPath);
            return Task.FromResult(0);
        }
    }
}
