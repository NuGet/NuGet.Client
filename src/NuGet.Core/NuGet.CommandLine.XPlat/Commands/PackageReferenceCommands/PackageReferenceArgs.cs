// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine.XPlat
{
    public class PackageReferenceArgs
    {
        public string DotnetPath { get; }
        public string ProjectPath { get; }
        public ILogger Logger { get; }
        public PackageDependency PackageDependency { get; set; }
        public string[] Frameworks { get; set; }
        public string[] Sources { get; set; }
        public string PackageDirectory { get; set; }
        public bool NoRestore { get; set; }
        public bool NoVersion { get; set; }

        public PackageReferenceArgs(string dotnetPath, string projectPath, PackageDependency packageDependency, ILogger logger)
        {
            ValidateArgument(dotnetPath);
            ValidateArgument(projectPath);
            ValidateArgument(packageDependency);
            ValidateArgument(logger);

            ProjectPath = projectPath;
            PackageDependency = packageDependency;
            Logger = logger;
            DotnetPath = dotnetPath;
        }

        private void ValidateArgument(object arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }
        }
    }
}