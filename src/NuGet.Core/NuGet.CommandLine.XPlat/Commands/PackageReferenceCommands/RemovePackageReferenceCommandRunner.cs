// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace NuGet.CommandLine.XPlat
{
    public class RemovePackageReferenceCommandRunner : IPackageReferenceCommandRunner
    {
        public Task<int> ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)
        {
            packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.Info_RemovePkgRemovingReference,
                packageReferenceArgs.PackageDependency.Id,
                packageReferenceArgs.ProjectPath));

            // Remove reference from the project
            var result = msBuild.RemovePackageReference(packageReferenceArgs.ProjectPath,
                packageReferenceArgs.PackageDependency);

            return Task.FromResult(result);
        }
    }
}