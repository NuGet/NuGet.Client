// Copyright (c) .NET Foundation. All rights reserved.
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
        private GlobalSettings _globalSettings;

        public PackageSpecResolver(PackageSpec packageSpec)
        {
            // Preload the cache with the package spec provided
            _projects[packageSpec.Name] = new PackageSpecInformation()
            {
                Name = packageSpec.Name,
                FullPath = packageSpec.FilePath,
                PackageSpec = packageSpec
            };

            var rootPath = ResolveRootDirectory(packageSpec.FilePath);
            Initialize(packageSpec.FilePath, rootPath);
        }

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
            get { return _searchPaths; }
        }

        public GlobalSettings GlobalSettings
        {
            get { return _globalSettings; }
        }

        public bool TryResolvePackageSpec(string name, out PackageSpec project)
        {
            project = null;

            PackageSpecInformation projectInfo;
            if (_projects.TryGetValue(name, out projectInfo))
            {
                project = projectInfo.PackageSpec;
                return project != null;
            }

            return false;
        }

        private void Initialize(string projectPath, string rootPath)
        {
            _searchPaths.Add(new DirectoryInfo(projectPath).Parent.FullName);

            if (GlobalSettings.TryGetGlobalSettings(rootPath, out _globalSettings))
            {
                foreach (var sourcePath in _globalSettings.ProjectPaths)
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
                    // Create the path to the project.json file.
                    var projectFilePath = Path.Combine(projectDirectory.FullName, PackageSpec.PackageSpecFileName);

                    // We INTENTIONALLY do not do an exists check here because it requires disk I/O
                    // Instead, we'll do an exists check when we try to resolve

                    // Check if we've already added this, just in case it was pre-loaded into the cache
                    if (!_projects.ContainsKey(projectDirectory.Name))
                    {
                        // The name of the folder is the project
                        _projects[projectDirectory.Name] = new PackageSpecInformation
                        {
                            Name = projectDirectory.Name,
                            FullPath = projectFilePath
                        };
                    }
                }
            }
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
                                FileStream stream = null;

                                try
                                {
                                    try
                                    {
                                        stream = File.OpenRead(FullPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        var messsage = Strings.FormatLog_ErrorReadingProjectJson(FullPath, ex.Message);

                                        throw new InvalidOperationException(messsage, ex);
                                    }

                                    project = JsonPackageSpecReader.GetPackageSpec(stream, Name, FullPath);
                                }
                                finally
                                {
                                    if (stream != null)
                                    {
                                        stream.Dispose();
                                    }
                                }
                            }

                            return project;
                        });
                }
                set
                {
                    lock (_lockObj)
                    {
                        _initialized = true;
                        _packageSpec = value;
                    }
                }
            }
        }
    }
}
