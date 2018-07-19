// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public struct PRPackage
{
    public string package;
    public string requestedVer;
    public string resolvedVer;
    public string suggestedVer;
    public bool deprecated;
    public bool autoRef;
}

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageCommandRunner : IListPackageCommandRunner
    {
        public Task<int> ExecuteCommand(ListPackageArgs listPackageArgs, MSBuildAPIUtility msBuild)
        {
            Debugger.Launch();

            
            var projects = Path.GetExtension(listPackageArgs.Path).Equals(".sln")
                           ?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path)
                           .Where(f => File.Exists(f))
                           :
                           new List<string>(new string[] { listPackageArgs.Path });

            foreach (var project in projects)
            {
               
                var packages = msBuild.GetPackageReferencesFromAssets(project, listPackageArgs.Frameworks, listPackageArgs.Transitive);
            }
            
            return null;
        }

    }
}