// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    public class DependencyGraphSpec
    {
        /// <summary>
        /// Allows a user to enable the legacy SHA512 hash function for dgSpec files which is used by no-op.
        /// </summary>
        private static readonly bool UseLegacyHashFunction = string.Equals(Environment.GetEnvironmentVariable("NUGET_ENABLE_LEGACY_DGSPEC_HASH_FUNCTION"), bool.TrueString, StringComparison.OrdinalIgnoreCase);

        private const string DGSpecFileNameExtension = "{0}.nuget.dgspec.json";

        private readonly SortedSet<string> _restore = new(PathUtility.GetStringComparerBasedOnOS());
        private readonly SortedDictionary<string, PackageSpec> _projects = new(PathUtility.GetStringComparerBasedOnOS());

        private const int Version = 1;

        private readonly bool _isReadOnly;
        // Internal for testing purposes
        internal Dictionary<string, string>? _projectNameToHashCodeCache;
        internal Dictionary<string, string>? _projectNameToHashCodeCacheForChildSpecs;

        internal void SetProjectNameToHashCodeCache(Dictionary<string, string> projectNameToHashCodeCache)
        {
            if (projectNameToHashCodeCache == null) throw new ArgumentNullException(nameof(projectNameToHashCodeCache));
            _projectNameToHashCodeCache = projectNameToHashCodeCache;
        }

        public static string GetDGSpecFileName(string projectName)
        {
            return string.Format(CultureInfo.InvariantCulture, DGSpecFileNameExtension, projectName);
        }

        public DependencyGraphSpec()
            : this(isReadOnly: false)
        {
        }

        public DependencyGraphSpec(bool isReadOnly)
        {
            _isReadOnly = isReadOnly;
        }

        private bool _refreshRestoreList = false;
        private IReadOnlyList<string>? _restoreList = null;

        /// <summary>
        /// Projects to restore.
        /// </summary>
        public IReadOnlyList<string> Restore
        {
            get
            {
                if (_restoreList == null || _refreshRestoreList)
                {
                    _restoreList = _restore.ToList();
                    _refreshRestoreList = false;
                }
                return _restoreList;
            }
        }

        private bool _refreshProjectsList = false;
        private IReadOnlyList<PackageSpec>? _restoreProjectList = null;

        /// <summary>
        /// All project specs.
        /// </summary>
        public IReadOnlyList<PackageSpec> Projects
        {
            get
            {
                if (_restoreProjectList == null || _refreshProjectsList)
                {
                    _restoreProjectList = _projects.Values.ToList();
                    _refreshProjectsList = false;
                }
                return _restoreProjectList;
            }
        }

        public PackageSpec? GetProjectSpec(string? projectUniqueName)
        {
            if (projectUniqueName == null)
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            PackageSpec? project;
            _projects.TryGetValue(projectUniqueName, out project);

            return project;
        }

        public IReadOnlyList<string> GetParents(string? rootUniqueName)
        {
            if (rootUniqueName == null) throw new ArgumentNullException(nameof(rootUniqueName));

            var parents = new List<PackageSpec>();

            foreach ((string name, PackageSpec project) in _projects.NoAllocEnumerate())
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
                // Clone the PackageSpec unless the caller has indicated that the objects won't be modified
                projectDependencyGraphSpec.AddProject(_isReadOnly ? spec : spec.Clone());
            }

            return projectDependencyGraphSpec;
        }

        /// <summary>
        /// Creates a new <see cref="DependencyGraphSpec" /> from a project name and project closure.
        /// </summary>
        /// <param name="projectUniqueName">The project's unique name.</param>
        /// <param name="closure">The project's closure</param>
        /// <returns>A <see cref="DependencyGraphSpec" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="projectUniqueName" />
        /// is <see langword="null" /> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="closure" /> is <see langword="null" />.</exception>
        public DependencyGraphSpec CreateFromClosure(string projectUniqueName, IReadOnlyList<PackageSpec> closure)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentException(Strings.ArgumentNullOrEmpty, nameof(projectUniqueName));
            }

            if (closure == null)
            {
                throw new ArgumentNullException(nameof(closure));
            }

            var dgSpec = new DependencyGraphSpec();

            dgSpec.AddRestore(projectUniqueName);

            foreach (PackageSpec packageSpec in closure)
            {
                dgSpec.AddProject(_isReadOnly ? packageSpec : packageSpec.Clone());
            }

            if (_isReadOnly)
            {
                _projectNameToHashCodeCacheForChildSpecs ??= new Dictionary<string, string>(PathUtility.GetStringComparerBasedOnOS());
                dgSpec.SetProjectNameToHashCodeCache(_projectNameToHashCodeCacheForChildSpecs);
            }

            return dgSpec;
        }

        /// <summary>
        /// Retrieve the full project closure including the root project itself.
        /// </summary>
        /// <remarks>Results are not sorted in any form.</remarks>
        public IReadOnlyList<PackageSpec> GetClosure(string? rootUniqueName)
        {
            if (rootUniqueName == null)
            {
                throw new ArgumentNullException(nameof(rootUniqueName));
            }

            var closure = new List<PackageSpec>();

            var added = new SortedSet<string>(PathUtility.GetStringComparerBasedOnOS());
            var toWalk = new Stack<PackageSpec>();

            // Start with the root
            var rootSpec = GetProjectSpec(rootUniqueName);
            if (rootSpec != null)
            {
                toWalk.Push(rootSpec);
            }

            while (toWalk.Count > 0)
            {
                var spec = toWalk.Pop();

                if (spec != null)
                {
                    // Add every spec to the closure
                    closure.Add(spec);

                    // Find children
                    foreach (var projectName in GetProjectReferenceNames(spec, _projects))
                    {
                        if (added.Add(projectName))
                        {
                            var projectSpec = GetProjectSpec(projectName);
                            if (projectSpec != null)
                            {
                                toWalk.Push(projectSpec);
                            }
                        }
                    }
                }
            }

            return closure;
        }

        private static IEnumerable<string> GetProjectReferenceNames(PackageSpec spec, SortedDictionary<string, PackageSpec> projectsByUniqueName)
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
            if (projectUniqueName == null) throw new ArgumentNullException(nameof(projectUniqueName));

            _refreshRestoreList = true;
            _restore.Add(projectUniqueName);
        }

        public void AddProject(PackageSpec projectSpec)
        {
            if (projectSpec == null) throw new ArgumentNullException(nameof(projectSpec));

            // Find the unique name in the spec, otherwise generate a new one.
            string projectUniqueName = projectSpec.RestoreMetadata?.ProjectUniqueName
                ?? Guid.NewGuid().ToString();

            if (!_projects.ContainsKey(projectUniqueName))
            {
                _refreshProjectsList = true;
                _projects.Add(projectUniqueName, projectSpec);
            }
        }

        [Obsolete("This is unused in production code and as such will be removed in a future release.")]
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
            if (path == null) throw new ArgumentNullException(nameof(path));

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);

            var dgspec = new DependencyGraphSpec();
            bool wasObjectRead;

            try
            {
                wasObjectRead = jsonReader.ReadObject(propertyName =>
                {
                    switch (propertyName)
                    {
                        case "restore":
                            jsonReader.ReadObject(restorePropertyName =>
                            {
                                if (!string.IsNullOrEmpty(restorePropertyName))
                                {
                                    dgspec._restore.Add(restorePropertyName);
                                }
                            });
                            break;

                        case "projects":
                            jsonReader.ReadObject(projectsPropertyName =>
                            {
#pragma warning disable CS0612 // Type or member is obsolete
                                PackageSpec packageSpec = JsonPackageSpecReader.GetPackageSpec(jsonReader, name: null, path, EnvironmentVariableWrapper.Instance);
#pragma warning restore CS0612 // Type or member is obsolete
                                dgspec._projects.Add(projectsPropertyName, packageSpec);
                            });
                            break;

                        default:
                            jsonReader.Skip();
                            break;
                    }
                });
            }
            catch (JsonReaderException ex)
            {
                throw FileFormatException.Create(ex, path);
            }

            if (!wasObjectRead || jsonReader.TokenType != JsonToken.EndObject)
            {
                throw new InvalidDataException();
            }

            return dgspec;
        }

        public void Save(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                Save(fileStream);
            }
        }

        public void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
#if NET5_0_OR_GREATER
            using (var textWriter = new StreamWriter(stream))
#else
            using (var textWriter = new NoAllocNewLineStreamWriter(stream))
#endif
            using (var jsonWriter = new JsonTextWriter(textWriter))
            using (var writer = new RuntimeModel.JsonObjectWriter(jsonWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                Write(writer, hashing: false, PackageSpecWriter.Write);
            }
        }

        public string GetHash()
        {
            using (IHashFunction hashFunc = UseLegacyHashFunction ? new Sha512HashFunction() : new FnvHash64Function())
            using (var writer = new HashObjectWriter(hashFunc))
            {
                if (_projectNameToHashCodeCache != null)
                {
                    Write(writer, hashing: true, PackageSpecWriter.Write, _projectNameToHashCodeCache);
                }
                else
                {
                    Write(writer, hashing: true, PackageSpecWriter.Write);
                }
                return writer.GetHash();
            }
        }

        private void Write(IObjectWriter writer, bool hashing, Action<PackageSpec, IObjectWriter, bool, IEnvironmentVariableReader> writeAction, Dictionary<string, string>? projectNameToHashCode = null)
        {
            writer.WriteObjectStart();
            writer.WriteNameValue("format", Version);

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
                WriteProject(writer, hashing, writeAction, project, projectNameToHashCode);
            }

            writer.WriteObjectEnd();
            writer.WriteObjectEnd();
        }

        private void WriteProject(IObjectWriter writer, bool hashing, Action<PackageSpec, IObjectWriter, bool, IEnvironmentVariableReader> writeAction, PackageSpec project, Dictionary<string, string>? projectNameToHashCode)
        {
            if (hashing && projectNameToHashCode != null)
            {
                string? projectHash = null;

                lock (projectNameToHashCode)
                {
                    projectNameToHashCode.TryGetValue(project.RestoreMetadata.ProjectUniqueName, out projectHash);
                }

                if (projectHash == null)
                {
                    using IHashFunction hashFunc = UseLegacyHashFunction ? new Sha512HashFunction() : new FnvHash64Function();
                    using var projectWriter = new HashObjectWriter(hashFunc);
                    writeAction.Invoke(project, projectWriter, hashing, EnvironmentVariableWrapper.Instance);
                    projectHash = projectWriter.GetHash();

                    lock (projectNameToHashCode)
                    {
                        if (!projectNameToHashCode.ContainsKey(project.RestoreMetadata.ProjectUniqueName))
                        {
                            projectNameToHashCode[project.RestoreMetadata.ProjectUniqueName] = projectHash;
                        }
                    }
                }

                writer.WriteObjectStart(projectHash);
                writer.WriteObjectEnd();
            }
            else
            {
                writer.WriteObjectStart(project.RestoreMetadata.ProjectUniqueName);
                writeAction.Invoke(project, writer, hashing, EnvironmentVariableWrapper.Instance);
                writer.WriteObjectEnd();
            }
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

            foreach ((string _, PackageSpec project) in _projects.NoAllocEnumerate())
            {
                newSpec.AddProject(project);
            }

            return newSpec;
        }

        public DependencyGraphSpec WithReplacedSpec(PackageSpec project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var newSpec = new DependencyGraphSpec();
            newSpec.AddProject(project);
            newSpec.AddRestore(project.RestoreMetadata.ProjectUniqueName);

            foreach ((string _, PackageSpec child) in _projects.NoAllocEnumerate())
            {
                newSpec.AddProject(child);
            }

            return newSpec;
        }

        public DependencyGraphSpec WithPackageSpecs(IEnumerable<PackageSpec> packageSpecs)
        {
            var newSpec = new DependencyGraphSpec();

            foreach (var packageSpec in packageSpecs)
            {
                newSpec.AddProject(packageSpec);
                newSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
            }

            foreach ((string _, PackageSpec child) in _projects.NoAllocEnumerate())
            {
                newSpec.AddProject(child);
            }

            return newSpec;
        }

        [Obsolete("This is unused in production code and as such will be removed in a future release.")]
        public DependencyGraphSpec WithoutTools()
        {
            var newSpec = new DependencyGraphSpec();

            foreach ((string _, PackageSpec project) in _projects.NoAllocEnumerate())
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
