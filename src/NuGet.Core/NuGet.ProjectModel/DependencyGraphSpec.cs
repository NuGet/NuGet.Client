using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            PackageSpec project;
            _projects.TryGetValue(projectUniqueName, out project);

            return project;
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
            return spec?.RestoreMetadata?.ProjectReferences?.Select(project => project.ProjectUniqueName)
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

            _projects.Add(projectUniqueName, projectSpec);
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
            var json = GetJson(spec: this);

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var textWriter = new StreamWriter(fileStream))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                json.WriteTo(jsonWriter);
            }
        }

        private static JObject GetJson(DependencyGraphSpec spec)
        {
            var json = new JObject();
            var restoreObj = new JObject();
            var projectsObj = new JObject();
            json["restore"] = restoreObj;
            json["projects"] = projectsObj;

            foreach (var restoreName in spec.Restore)
            {
                restoreObj[restoreName] = new JObject();
            }

            foreach (var project in spec.Projects)
            {
                // Convert package spec to json
                var projectObj = new JObject();
                JsonPackageSpecWriter.WritePackageSpec(project, projectObj);

                projectsObj[project.RestoreMetadata.ProjectUniqueName] = projectObj;
            }

            return json;
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
    }
}
