// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace NuGet.ProjectModel
{
    public class PackageSpecResolver : IPackageSpecResolver
    {
        private readonly HashSet<string> _searchPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PackageSpecInformation> _projects
            = new Dictionary<string, PackageSpecInformation>(StringComparer.Ordinal);

        /// <summary>
        /// Create a resolver from a package spec. The spec path will be used to find the root
        /// and global.json paths.
        /// </summary>
        public PackageSpecResolver(PackageSpec packageSpec)
        {
            // Preload the cache with the package spec provided
            _projects[packageSpec.Name] = new PackageSpecInformation()
            {
                Name = packageSpec.Name,
                FullPath = packageSpec.FilePath,
                PackageSpec = packageSpec
            };

            RootPath = GetRootFromProjectJson(packageSpec.FilePath);
            Initialize(RootPath);
        }

        /// <summary>
        /// Use a root path to find projects.
        /// </summary>
        /// <param name="rootPath">Parent directory of the directory containing project.json file.</param>
        public PackageSpecResolver(string rootPath)
        {
            RootPath = rootPath;
            Initialize(rootPath);
        }

        /// <summary>
        /// Create a spec resolver from a project.json path.
        /// This will automatically find the root path.
        /// </summary>
        public static PackageSpecResolver FromPackageSpecPath(string projectJsonPath)
        {
            if (projectJsonPath == null)
            {
                throw new ArgumentNullException(nameof(projectJsonPath));
            }

            var rootPath = GetRootFromProjectJson(projectJsonPath);

            return new PackageSpecResolver(rootPath);
        }

        /// <summary>
        /// Root path used for resolving projects and global.json.
        /// This is typically the parent directory of the project directory.
        /// </summary>
        public string RootPath { get; }

        public IEnumerable<string> SearchPaths
        {
            get { return _searchPaths; }
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

        /// <summary>
        /// Build the search path index.
        /// </summary>
        private void Initialize(string rootPath)
        {
            // Include the root for sibling directories
            _searchPaths.Add(rootPath);

            // Include all paths in global.json
            var globalPaths = GetGlobalPaths(rootPath);
            _searchPaths.UnionWith(globalPaths);

            // Build the index of potential projects
            foreach (var project in GetPotentialProjects(_searchPaths))
            {
                if (_projects.ContainsKey(project.Name))
                {
                    // Remove the existing project if it doesn't have project.json
                    // project.json isn't checked until the project is resolved, but here
                    // we need to check it up front.
                    var otherProject = _projects[project.Name];

                    if (project.FullPath != otherProject.FullPath)
                    {
                        var projectExists = File.Exists(project.FullPath);
                        var otherExists = File.Exists(otherProject.FullPath);

                        if (projectExists != otherExists
                            && projectExists
                            && !otherExists)
                        {
                            // the project currently in the cache does not exist, but this one does
                            // remove the old project and add the current one
                            _projects[project.Name] = project;
                        }
                    }
                }
                else
                {
                    _projects.Add(project.Name, project);
                }
            }
        }

        /// <summary>
        /// Finds the parent directory of the project.json.
        /// </summary>
        /// <param name="projectJsonPath">Full path to project.json.</param>
        private static string GetRootFromProjectJson(string projectJsonPath)
        {
            if (!string.IsNullOrEmpty(projectJsonPath))
            {
                var file = new FileInfo(projectJsonPath);

                // If for some reason we are at the root of the drive this will be null
                // Use the file directory instead.
                if (file.Directory.Parent == null)
                {
                    return file.Directory.FullName;
                }
                else
                {
                    return file.Directory.Parent.FullName;
                }
            }

            return projectJsonPath;
        }

        /// <summary>
        /// Read paths from global.json.
        /// </summary>
        private static List<string> GetGlobalPaths(string rootPath)
        {
            var paths = new List<string>();

            var globalJsonRoot = ResolveRootDirectory(rootPath);

            GlobalSettings globalSettings;
            if (GlobalSettings.TryGetGlobalSettings(globalJsonRoot, out globalSettings))
            {
                foreach (var sourcePath in globalSettings.ProjectPaths)
                {
                    var path = Path.GetFullPath(Path.Combine(globalJsonRoot, sourcePath));

                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Create the list of potential projects from the search paths.
        /// </summary>
        private static List<PackageSpecInformation> GetPotentialProjects(IEnumerable<string> searchPaths)
        {
            var projects = new List<PackageSpecInformation>();

            // Resolve all of the potential projects
            foreach (var searchPath in searchPaths)
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
                    var project = new PackageSpecInformation
                    {
                        Name = projectDirectory.Name,
                        FullPath = projectFilePath
                    };

                    projects.Add(project);
                }
            }

            return projects;
        }

        /// <summary>
        /// Find the nearest global.json file.
        /// </summary>
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
                                        var message = string.Format(
                                            CultureInfo.CurrentCulture,
                                            Strings.Log_ErrorReadingProjectJson,
                                            FullPath,
                                            ex.Message);

                                        throw new InvalidOperationException(message, ex);
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
