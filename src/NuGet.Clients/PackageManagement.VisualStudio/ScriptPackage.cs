// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

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
        private IEnumerable<IScriptPackageFile> _files;

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

        public IEnumerable<IScriptPackageFile> GetFiles()
        {
            if (_files == null)
            {
                var result = new List<ScriptPackageFile>();
                var reader = GetPackageReader(_installPath);

                Debug.Assert(reader != null);

                if (reader != null)
                {
                    result.AddRange(GetPackageFiles(reader.GetLibItems()));
                    result.AddRange(GetPackageFiles(reader.GetToolItems()));
                    result.AddRange(GetPackageFiles(reader.GetContentItems()));
                    result.AddRange(GetPackageFiles(reader.GetBuildItems()));
                    result.AddRange(reader.GetFiles()
                        .Where(path => IsUnknownPath(path))
                        .Select(p => new ScriptPackageFile(p, NuGetFramework.AnyFramework)));
                }

                return result;
            }

            return _files;
        }

        private IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore()
        {
            var result = new List<PackageAssemblyReference>();
            if (Directory.Exists(_installPath))
            {
                var nupkg = new FileInfo(
                    Path.Combine(_installPath, Id + "." + Version + PackagingCoreConstants.NupkgExtension));
                if (nupkg.Exists)
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

        private PackageReader GetPackageReader(string installPath)
        {
            if (Directory.Exists(_installPath))
            {
                var nupkg = new FileInfo(
                        Path.Combine(_installPath, Id + "." + Version + PackagingCoreConstants.NupkgExtension));
                if (nupkg.Exists)
                {
                    return new PackageReader(nupkg.OpenRead());
                }
            }

            return null;
        }

        private IEnumerable<ScriptPackageFile> GetPackageFiles(IEnumerable<FrameworkSpecificGroup> frameworkGroups)
        {
            var result = new List<ScriptPackageFile>();

            foreach (var frameworkGroup in frameworkGroups)
            {
                var framework = frameworkGroup.TargetFramework;
                result.AddRange(frameworkGroup.Items.Select(item => new ScriptPackageFile(item, framework)));
            }

            return result;
        }

        private bool IsUnknownPath(string path)
        {
            return PackageHelper.IsPackageFile(path)
                   && !path.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                   && !path.StartsWith("tools", StringComparison.OrdinalIgnoreCase)
                   && !path.StartsWith("content", StringComparison.OrdinalIgnoreCase)
                   && !path.StartsWith("build", StringComparison.OrdinalIgnoreCase);
        }
    }
}
