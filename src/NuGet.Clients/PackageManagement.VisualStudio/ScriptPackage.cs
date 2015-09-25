// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace NuGet.PackageManagement.VisualStudio
{
    // this scriptPackage is a package inferface for script executor
    // it provides an IPackage like interface to make sure all install.ps scripts which depend on IPackage keep working
    public class ScriptPackage : IScriptPackage
    {
        private string _id;
        private string _version;
        private string _installPath;
        private IList<IPackageAssemblyReference> _assemblyReferences;

        public ScriptPackage(string id, string version, string installPath)
        {
            _id = id;
            _version = version;
            _installPath = installPath;
        }

        public string Id
        {
            get { return _id; }
        }

        public string Version
        {
            get { return _version; }
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get
            {
                if (_assemblyReferences == null)
                {
                    _assemblyReferences = GetAssemblyReferencesCore().ToList();
                }

                return _assemblyReferences;
            }
        }

        private IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore()
        {
            var result = new List<PackageAssemblyReference>();
            if (Directory.Exists(_installPath))
            {
                var nupkg = new DirectoryInfo(_installPath).EnumerateFiles("*.nupkg").FirstOrDefault();
                if (nupkg != null)
                {
                    var reader = new PackageReader(nupkg.OpenRead());
                    var referenceItems = reader.GetReferenceItems();
                    var files = NuGetFrameworkUtility.GetNearest<FrameworkSpecificGroup>(referenceItems,
                                                                                         NuGetFramework.AnyFramework);
                    if (files != null)
                    {
                        result = files.Items.Select(file => new PackageAssemblyReference(file)).ToList();
                    }
                }
            }

            return result;
        }
    }
}
