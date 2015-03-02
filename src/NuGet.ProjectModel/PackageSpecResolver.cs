// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace NuGet.ProjectModel
{
    public class PackageSpecResolver : IPackageSpecResolver
    {
        private HashSet<string> _searchPaths = new HashSet<string>();
        private Dictionary<string, PackageSpecInformation> _projects = new Dictionary<string, PackageSpecInformation>();

        public PackageSpecResolver(string packageSpecPath)
        {
            var rootPath = ResolveRootDirectory(packageSpecPath);
            Initialize(packageSpecPath, rootPath);
        }

        public PackageSpecResolver(string packageSpecPath, string rootPath)
        {
            Initialize(packageSpecPath, rootPath);
        }

        public IEnumerable<string> SearchPaths
        {
            get
            {
                return _searchPaths;
            }
        }

        public bool TryResolvePackageSpec(string name, out PackageSpec project)
        {
            project = null;

            PackageSpecInformation projectInfo;
            if (_projects.TryGetValue(name, out projectInfo))
            {
                project = projectInfo.PackageSpec;
                return true;
            }

            return false;
        }

        private void Initialize(string projectPath, string rootPath)
        {
            _searchPaths.Add(Path.GetDirectoryName(projectPath));

            GlobalSettings global;

            if (GlobalSettings.TryGetGlobalSettings(rootPath, out global))
            {
                foreach (var sourcePath in global.ProjectPaths)
                {
                    _searchPaths.Add(Path.Combine(rootPath, sourcePath));
                }
            }

            // Resolve all of the potential projects
            foreach (var searchPath in _searchPaths)
            {
                var directory = new DirectoryInfo(searchPath);

                if (!directory.Exists)
                {
                    continue;
                }

                foreach (var projectDirectory in directory.EnumerateDirectories())
                {
                    // The name of the folder is the project
                    _projects[projectDirectory.Name] = new PackageSpecInformation
                    {
                        Name = projectDirectory.Name,
                        FullPath = projectDirectory.FullName
                    };
                }
            }
        }

        public static PackageSpecResolver ForPackageSpecDirectory(string packageSpecDirectory)
        {
            var packageSpecFile = Path.Combine(packageSpecDirectory, PackageSpec.PackageSpecFileName);
            return new PackageSpecResolver(
                packageSpecFile,
                ResolveRootDirectory(packageSpecFile));
        }

        public static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            while (di.Parent != null)
            {
                var globalJsonPath = Path.Combine(di.FullName, GlobalSettings.GlobalFileName);

                if (File.Exists(globalJsonPath))
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            // If we don't find any files then make the project folder the root
            return projectPath;
        }

        private class PackageSpecInformation
        {
            private PackageSpec _packageSpec;
            private bool _initialized;
            private object _lockObj = new object();

            public string Name { get; set; }

            public string FullPath { get; set; }

            public PackageSpec PackageSpec
            {
                get
                {
                    return LazyInitializer.EnsureInitialized(ref _packageSpec, ref _initialized, ref _lockObj, () =>
                    {
                        PackageSpec project = null;

                        if (File.Exists(FullPath))
                        {
                            using (var stream = File.OpenRead(FullPath))
                            {
                                // TODO: does this need more error handling?
                                project = JsonPackageSpecReader.GetPackageSpec(stream, Name, FullPath);
                            }
                        }

                        return project;
                    });
                }
            }
        }
    }
}
