// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public class DependencyGraphSpec
    {
        private readonly SortedSet<string> _restore = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, PackageSpec> _projects = new SortedDictionary<string, PackageSpec>(StringComparer.Ordinal);

        public DependencyGraphSpec(JObject json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ParseJson(json);

            Json = json;
        }

        public DependencyGraphSpec()
        {
            Json = new JObject();
        }

        /// <summary>
        /// Projects to restore.
        /// </summary>
        public IReadOnlyList<string> Restore
        {
            get
            {
                return _restore.ToList();
            }
        }

        /// <summary>
        /// All project specs.
        /// </summary>
        public IReadOnlyList<PackageSpec> Projects
        {
            get
            {
                return _projects.Values.ToList();
            }
        }

        /// <summary>
        /// File json.
        /// </summary>
        public JObject Json { get; }

        public PackageSpec GetProjectSpec(string projectUniqueName)
        {
            if (projectUniqueName == null)
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            PackageSpec project;
            _projects.TryGetValue(projectUniqueName, out project);

            return project;
        }

        public IReadOnlyList<string> GetParents(string rootUniqueName)
        {
            var parents = new List<PackageSpec>();

            foreach (var project in Projects)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(
                    project.RestoreMetadata.ProjectUniqueName,
                    rootUniqueName))
                {
                    var closure = GetClosureWithoutSorting(project.RestoreMetadata.ProjectUniqueName);

                    if (closure.Any(e => StringComparer.OrdinalIgnoreCase.Equals(
                        e.RestoreMetadata.ProjectUniqueName,
                        rootUniqueName)))
                    {
                        parents.Add(project);
                    }
                }
            }

            return SortPackagesByDependencyOrder(parents)
                .Select(e => e.RestoreMetadata.ProjectUniqueName)
                .ToList();
        }

        /// <summary>
        /// Retrieve the full project closure including the root project itself.
        /// </summary>
        public IReadOnlyList<PackageSpec> GetClosure(string rootUniqueName)
        {
            if (rootUniqueName == null)
            {
                throw new ArgumentNullException(nameof(rootUniqueName));
            }

            var closure = GetClosureWithoutSorting(rootUniqueName);

            return SortPackagesByDependencyOrder(closure);
        }

        private IReadOnlyList<PackageSpec> GetClosureWithoutSorting(string rootUniqueName)
        {
            if (rootUniqueName == null)
            {
                throw new ArgumentNullException(nameof(rootUniqueName));
            }

            var closure = new List<PackageSpec>();

            var added = new SortedSet<string>(StringComparer.Ordinal);
            var toWalk = new Stack<PackageSpec>();

            // Start with the root
            toWalk.Push(GetProjectSpec(rootUniqueName));

            while (toWalk.Count > 0)
            {
                var spec = toWalk.Pop();

                if (spec != null)
                {
                    // Add every spec to the closure
                    closure.Add(spec);

                    // Find children
                    foreach (var projectName in GetProjectReferenceNames(spec))
                    {
                        if (added.Add(projectName))
                        {
                            toWalk.Push(GetProjectSpec(projectName));
                        }
                    }
                }
            }

            return closure;
        }

        private static IEnumerable<string> GetProjectReferenceNames(PackageSpec spec)
        {
            // Handle projects which may not have specs, and which may not have references
            return spec?.RestoreMetadata?
                .TargetFrameworks
                .SelectMany(e => e.ProjectReferences)
                .Select(project => project.ProjectUniqueName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                ?? Enumerable.Empty<string>();
        }

        public void AddRestore(string projectUniqueName)
        {
            _restore.Add(projectUniqueName);
        }

        public void AddProject(PackageSpec projectSpec)
        {
            // Find the unique name in the spec, otherwise generate a new one.
            var projectUniqueName = projectSpec.RestoreMetadata?.ProjectUniqueName
                ?? Guid.NewGuid().ToString();

            if (!_projects.ContainsKey(projectUniqueName))
            {
                _projects.Add(projectUniqueName, projectSpec);
            }
        }

        public static DependencyGraphSpec Union(IEnumerable<DependencyGraphSpec> dgSpecs)
        {
            var projects =
                dgSpecs.SelectMany(e => e.Projects)
                    .GroupBy(e => e.RestoreMetadata.ProjectUniqueName, StringComparer.Ordinal)
                    .Select(e => e.First())
                    .ToList();

            var newDgSpec = new DependencyGraphSpec();
            foreach (var project in projects)
            {
                newDgSpec.AddProject(project);
            }
            return newDgSpec;
        }

        public static DependencyGraphSpec Load(string path)
        {
            var json = ReadJson(path);

            return Load(json);
        }

        public static DependencyGraphSpec Load(JObject json)
        {
            return new DependencyGraphSpec(json);
        }

        public void Save(string path)
        {
            var json = GetJson();

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var textWriter = new StreamWriter(fileStream))
            {
                textWriter.Write(json);
            }
        }

        private string GetJson()
        {
            var writer = new RuntimeModel.JsonObjectWriter();

            Write(writer);

            return writer.GetJson();
        }

        private void ParseJson(JObject json)
        {
            var restoreObj = json.GetValue<JObject>("restore");
            if (restoreObj != null)
            {
                _restore.UnionWith(restoreObj.Properties().Select(prop => prop.Name));
            }

            var projectsObj = json.GetValue<JObject>("projects");
            if (projectsObj != null)
            {
                foreach (var prop in projectsObj.Properties())
                {
                    var specJson = (JObject)prop.Value;
                    var spec = JsonPackageSpecReader.GetPackageSpec(specJson);

                    _projects.Add(prop.Name, spec);
                }
            }
        }

        private static JObject ReadJson(string packageSpecPath)
        {
            JObject json;

            using (var stream = new FileStream(packageSpecPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                try
                {
                    json = JObject.Load(reader);
                }
                catch (JsonReaderException ex)
                {
                    throw FileFormatException.Create(ex, packageSpecPath);
                }
            }

            return json;
        }

        public string GetHash()
        {
            using (var hashFunc = new Sha512HashFunction())
            using (var writer = new HashObjectWriter(hashFunc))
            {
                Write(writer);

                return writer.GetHash();
            }
        }

        private void Write(RuntimeModel.IObjectWriter writer)
        {
            writer.WriteNameValue("format", 1);

            writer.WriteObjectStart("restore");

            // Preserve default sort order
            foreach (var restoreName in _restore)
            {
                writer.WriteObjectStart(restoreName);
                writer.WriteObjectEnd();
            }

            writer.WriteObjectEnd();

            writer.WriteObjectStart("projects");

            // Preserve default sort order
            foreach (var pair in _projects)
            {
                var project = pair.Value;

                writer.WriteObjectStart(project.RestoreMetadata.ProjectUniqueName);
                PackageSpecWriter.Write(project, writer);
                writer.WriteObjectEnd();
            }

            writer.WriteObjectEnd();
        }

        /// <summary>
        /// Order dependencies by children first.
        /// </summary>
        public static IReadOnlyList<PackageSpec> SortPackagesByDependencyOrder(
            IEnumerable<PackageSpec> packages)
        {
            var sorted = new List<PackageSpec>();
            var toSort = packages.Distinct().ToList();

            while (toSort.Count > 0)
            {
                // Order packages by parent count, take the child with the lowest number of parents
                // and remove it from the list
                var nextPackage = toSort.OrderBy(package => GetParentCount(toSort, package.RestoreMetadata.ProjectUniqueName))
                    .ThenBy(package => package.RestoreMetadata.ProjectUniqueName, StringComparer.Ordinal).First();

                sorted.Add(nextPackage);
                toSort.Remove(nextPackage);
            }

            // the list is ordered by parents first, reverse to run children first
            sorted.Reverse();

            return sorted;
        }

        private static int GetParentCount(List<PackageSpec> packages, string projectUniqueName)
        {
            int count = 0;

            foreach (var package in packages)
            {
                if (package.RestoreMetadata.TargetFrameworks.SelectMany(r => r.ProjectReferences).Any(dependency =>
                    string.Equals(projectUniqueName, dependency.ProjectUniqueName, StringComparison.OrdinalIgnoreCase)))
                {
                    count++;
                }
            }

            return count;
        }

        public DependencyGraphSpec WithoutRestores()
        {
            var newSpec = new DependencyGraphSpec();

            foreach (var project in Projects)
            {
                newSpec.AddProject(project);
            }

            return newSpec;
        }

        public DependencyGraphSpec WithReplacedSpec(PackageSpec project)
        {
            var newSpec = new DependencyGraphSpec();
            newSpec.AddProject(project);
            newSpec.AddRestore(project.RestoreMetadata.ProjectUniqueName);

            foreach (var child in Projects)
            {
                newSpec.AddProject(child);
            }

            return newSpec;
        }

        public DependencyGraphSpec WithoutTools()
        {
            var newSpec = new DependencyGraphSpec();

            foreach (var project in Projects)
            {
                if (project.RestoreMetadata.ProjectStyle != ProjectStyle.DotnetCliTool)
                {
                    // Add all non-tool projects
                    newSpec.AddProject(project);

                    // Add to restore if it existed in the current dg file
                    if (_restore.Contains(project.RestoreMetadata.ProjectUniqueName))
                    {
                        newSpec.AddRestore(project.RestoreMetadata.ProjectUniqueName);
                    }
                }
            }

            return newSpec;
        }
    }
}