using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public class ProjectReferenceCache
    {
        private Dictionary<string, Dictionary<string, ExternalProjectReference>> _cache 
            = new Dictionary<string, Dictionary<string, ExternalProjectReference>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, PackageSpec> _projectJsonCache
            = new Dictionary<string, PackageSpec>(StringComparer.OrdinalIgnoreCase);

        public ProjectReferenceCache(IEnumerable<string> msbuildOutputLines)
        {
            var lookup = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

            string entryPoint = null;

            foreach (var line in msbuildOutputLines)
            {
                if (line.StartsWith("#:", StringComparison.Ordinal))
                {
                    entryPoint = line.Substring(2, line.Length - 2);
                    continue;
                }

                var parts = line.TrimEnd().Split('|');

                if (parts.Length == 2)
                {
                    var parent = parts[0];
                    var child = parts[1];

                    Dictionary<string, HashSet<string>> projectReferences;
                    if (!lookup.TryGetValue(entryPoint, out projectReferences))
                    {
                        projectReferences = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                        lookup.Add(entryPoint, projectReferences);
                    }

                    HashSet<string> children;
                    if (!projectReferences.TryGetValue(parent, out children))
                    {
                        children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        projectReferences.Add(parent, children);
                    }

                    children.Add(child);
                }
                else
                {
                    Debug.Fail("Invalid: " + line);
                }
            }

            foreach (var entryPointKey in lookup.Keys)
            {
                var references = GetExternalProjectReferences(lookup[entryPointKey]);

                _cache.Add(entryPointKey, references);
            }
        }

        public List<ExternalProjectReference> GetReferences(string entryPointPath)
        {
            var results = new List<ExternalProjectReference>();

            Dictionary <string, ExternalProjectReference> lookup;
            if (_cache.TryGetValue(entryPointPath, out lookup))
            {
                results.AddRange(lookup.Values);
            }

            return results;
        }

        public PackageSpec GetProjectJson(string path)
        {
            PackageSpec projectJson;
            _projectJsonCache.TryGetValue(path, out projectJson);

            return projectJson;
        }

        /// <summary>
        /// MSBuild project -> ExternalProjectReference
        /// </summary>
        private Dictionary<string, ExternalProjectReference> GetExternalProjectReferences(
            Dictionary<string, HashSet<string>> projectReferences)
        {
            var results = new Dictionary<string, ExternalProjectReference>(StringComparer.OrdinalIgnoreCase);

            // Get the set of all possible projects
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allProjects.UnionWith(projectReferences.Keys);
            allProjects.UnionWith(projectReferences.Values.SelectMany(children => children));

            // Load up all package specs
            foreach (var projectPath in allProjects)
            {
                var uniqueName = Path.GetFileNameWithoutExtension(projectPath);

                var projectJson = GetPackageSpec(projectPath);

                var childProjectNames = new List<string>();
                HashSet<string> children;
                if (projectReferences.TryGetValue(projectPath, out children))
                {
                    var fixedNames = children.Select(child => Path.GetFileNameWithoutExtension(child));
                    childProjectNames.AddRange(fixedNames);
                }

                var projectReference = new ExternalProjectReference(
                    uniqueName,
                    projectJson,
                    projectPath,
                    childProjectNames);

                Debug.Assert(!results.ContainsKey(uniqueName), "dupe: " + uniqueName);

                results.Add(uniqueName, projectReference);
            }

            return results;
        }

        /// <summary>
        /// Load a project.json for an msbuild project file.
        /// This allows projectName.project.json for csproj but not for xproj.
        /// </summary>
        private PackageSpec GetPackageSpec(string msbuildProjectPath)
        {
            PackageSpec result = null;
            string path = null;
            var directory = Path.GetDirectoryName(msbuildProjectPath);
            var projectName = Path.GetFileNameWithoutExtension(msbuildProjectPath);

            if (msbuildProjectPath.EndsWith(XProjUtility.XProjExtension, StringComparison.OrdinalIgnoreCase))
            {
                // Only project.json is allowed
                path = Path.Combine(
                    directory,
                    BuildIntegratedProjectUtility.ProjectConfigFileName);
            }
            else
            {
                // Allow project.json or projectName.project.json
                path = BuildIntegratedProjectUtility.GetProjectConfigPath(directory, projectName);
            }

            // Read the file if it exists and is not cached already
            if (!_projectJsonCache.TryGetValue(path, out result))
            {
                if (File.Exists(path))
                {
                    result = JsonPackageSpecReader.GetPackageSpec(projectName, path);
                }

                _projectJsonCache.Add(path, result);
            }

            return result;
        }
    }
}
