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
        public PackageIdentity PackageIdentity { get; }
        public ISettings Settings { get; }
        public ILogger Logger { get; }

        public PackageReferenceArgs(string dotnetPath, string projectPath, PackageIdentity packageIdentity, ISettings settings, ILogger logger)
        {
            if (dotnetPath == null)
            {
                throw new ArgumentNullException(nameof(dotnetPath));
            }
            else if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }
            else if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }
            else if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            else if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            ProjectPath = projectPath;
            PackageIdentity = packageIdentity;
            Settings = settings;
            Logger = logger;
            DotnetPath = dotnetPath;
        }
    }
}