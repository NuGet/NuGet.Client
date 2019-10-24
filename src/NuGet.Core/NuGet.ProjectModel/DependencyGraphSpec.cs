// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;

namespace NuGet.ProjectModel
{
    public class DependencyGraphSpec
    {
        private const string DGSpecFileNameExtension = "{0}.nuget.dgspec.json";

        private readonly SortedSet<string> _restore = new SortedSet<string>(PathUtility.GetStringComparerBasedOnOS());
        private readonly SortedDictionary<string, PackageSpec> _projects = new SortedDictionary<string, PackageSpec>(PathUtility.GetStringComparerBasedOnOS());

        private const int _version = 1;

        public static string GetDGSpecFileName(string projectName)
        {
            return string.Format(DGSpecFileNameExtension, projectName);
        }

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
        /// Gets or sets a value indicating if this instance can be treated as read-only and no other threads will update it.
        /// </summary>
        public bool IsReadOnly { get; set; }

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
                    var closure = GetClosure(project.RestoreMetadata.ProjectUniqueName);

                    if (closure.Any(e => StringComparer.OrdinalIgnoreCase.Equals(
                        e.RestoreMetadata.ProjectUniqueName,
                        rootUniqueName)))
                    {
                        parents.Add(project);
                    }
                }
            }

            return parents
                .Select(e => e.RestoreMetadata.ProjectUniqueName)
                .ToList();
        }

        /// <summary>
        /// Retrieve a DependencyGraphSpec with the project closure.
        /// </summary>
        /// <param name="projectUniqueName"></param>
        /// <returns></returns>
        public DependencyGraphSpec WithProjectClosure(string projectUniqueName)
        {
            var projectDependencyGraphSpec = new DependencyGraphSpec();
            projectDependencyGraphSpec.AddRestore(projectUniqueName);
            foreach (var spec in GetClosure(projectUniqueName))
            {
                projectDependencyGraphSpec.AddProject(!IsReadOnly ? spec.Clone() : spec);
            }

            return projectDependencyGraphSpec;
        }

        /// <summary>
        /// Retrieve the full project closure including the root project itself.
        /// </summary>
        /// <remarks>Results are not sorted in any form.</remarks>
        public IReadOnlyList<PackageSpec> GetClosure(string rootUniqueName)
        {
            if (rootUniqueName == null)
            {
                throw new ArgumentNullException(nameof(rootUniqueName));
            }

            var projectsByUniqueName = _projects
                .ToDictionary(t => t.Value.RestoreMetadata.ProjectUniqueName, t => t.Value, PathUtility.GetStringComparerBasedOnOS());

            var closure = new List<PackageSpec>();

            var added = new SortedSet<string>(PathUtility.GetStringComparerBasedOnOS());
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
                    foreach (var projectName in GetProjectReferenceNames(spec, projectsByUniqueName))
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

        private static IEnumerable<string> GetProjectReferenceNames(PackageSpec spec, Dictionary<string, PackageSpec> projectsByUniqueName)
        {
            // Handle projects which may not have specs, and which may not have references
            return spec?.RestoreMetadata?
                .TargetFrameworks
                .SelectMany(e => e.ProjectReferences)
                .Where(project => projectsByUniqueName.ContainsKey(project.ProjectUniqueName))
                .Select(project => project.ProjectUniqueName)
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
                    .GroupBy(e => e.RestoreMetadata.ProjectUniqueName, PathUtility.GetStringComparerBasedOnOS())
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

            Write(writer, PackageSpecWriter.Write);

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
            using (var reader = new StreamReader(stream))
            {
                try
                {
                    json = JsonUtility.LoadJson(reader);
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
                Write(writer, PackageSpecWriter.Write);
                return writer.GetHash();
            }
        }

        private void Write(RuntimeModel.IObjectWriter writer, Action<PackageSpec, RuntimeModel.IObjectWriter> writeAction)
        {
            writer.WriteNameValue("format", _version);

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
                writeAction.Invoke(project, writer);
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
            return TopologicalSortUtility.SortPackagesByDependencyOrder(
                items: packages,
                comparer: PathUtility.GetStringComparerBasedOnOS(),
                getId: GetPackageSpecId,
                getDependencies: GetPackageSpecDependencyIds);
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

        /// <summary>
        /// PackageSpec -> id
        /// </summary>
        private static string GetPackageSpecId(PackageSpec spec)
        {
            return spec.RestoreMetadata.ProjectUniqueName;
        }

        /// <summary>
        /// PackageSpec -> Project dependency ids
        /// </summary>
        private static string[] GetPackageSpecDependencyIds(PackageSpec spec)
        {
            return spec.RestoreMetadata
                .TargetFrameworks
                .SelectMany(r => r.ProjectReferences)
                .Select(r => r.ProjectUniqueName)
                .Distinct(PathUtility.GetStringComparerBasedOnOS())
                .ToArray();
        }
    }
}
