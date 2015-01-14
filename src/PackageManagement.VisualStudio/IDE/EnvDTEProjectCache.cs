using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Cache that stores project based on multiple names. i.e. EnvDTEProject can be retrieved by name (if non conflicting), unique name and custom unique name.
    /// </summary>
    internal class EnvDTEProjectCache
    {
        // Mapping from project name structure to project instance
        private readonly Dictionary<EnvDTEProjectName, EnvDTEProject> _envDTEProjectCache = new Dictionary<EnvDTEProjectName, EnvDTEProject>();

        // Mapping from all names to a project name structure
        private readonly Dictionary<string, EnvDTEProjectName> _projectNamesCache = new Dictionary<string, EnvDTEProjectName>(StringComparer.OrdinalIgnoreCase);

        // We need another dictionary for short names since there may be more than project name per short name
        private readonly Dictionary<string, HashSet<EnvDTEProjectName>> _shortNameCache = new Dictionary<string, HashSet<EnvDTEProjectName>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Finds a project by short name, unique name or custom unique name.
        /// </summary>
        /// <param name="name">name of the project to retrieve.</param>
        /// <param name="project">project instance</param>
        /// <returns>true if the project with the specified name is cached.</returns>
        public bool TryGetProject(string name, out EnvDTEProject project)
        {
            project = null;
            // First try to find the project name in one of the dictionaries. Then locate the project for that name.
            EnvDTEProjectName EnvDTEProjectName;
            return TryGetProjectName(name, out EnvDTEProjectName) &&
                   _envDTEProjectCache.TryGetValue(EnvDTEProjectName, out project);
        }

        /// <summary>
        /// Finds a project name by short name, unique name or custom unique name.
        /// </summary>
        /// <param name="name">name of the project</param>
        /// <param name="EnvDTEProjectName">project name instance</param>
        /// <returns>true if the project name with the specified name is found.</returns>
        public bool TryGetProjectName(string name, out EnvDTEProjectName EnvDTEProjectName)
        {
            return _projectNamesCache.TryGetValue(name, out EnvDTEProjectName) ||
                   TryGetProjectNameByShortName(name, out EnvDTEProjectName);
        }

        /// <summary>
        /// Removes a project and returns the project name instance of the removed project.
        /// </summary>
        /// <param name="name">name of the project to remove.</param>
        public void RemoveProject(string name)
        {
            EnvDTEProjectName EnvDTEProjectName;
            if (_projectNamesCache.TryGetValue(name, out EnvDTEProjectName))
            {
                // Remove from both caches
                RemoveProjectName(EnvDTEProjectName);
                RemoveShortName(EnvDTEProjectName);
            }
        }

        public bool Contains(string name)
        {
            if (name == null)
            {
                return false;
            }


            return _projectNamesCache.ContainsKey(name) ||
                   _shortNameCache.ContainsKey(name);
        }

        /// <summary>
        /// Returns all cached projects.
        /// </summary>
        public IEnumerable<EnvDTEProject> GetProjects()
        {
            return _envDTEProjectCache.Values;
        }

        /// <summary>
        /// Determines if a short name is ambiguous
        /// </summary>
        /// <param name="shortName">short name of the project</param>
        /// <returns>true if there are multiple projects with the specified short name.</returns>
        public bool IsAmbiguous(string shortName)
        {
            HashSet<EnvDTEProjectName> projectNames;
            if (_shortNameCache.TryGetValue(shortName, out projectNames))
            {
                return projectNames.Count > 1;
            }
            return false;
        }

        /// <summary>
        /// Add a project to the cache.
        /// </summary>
        /// <param name="project">project to add to the cache.</param>
        /// <returns>The project name of the added project.</returns>
        public EnvDTEProjectName AddProject(EnvDTEProject project)
        {
            // First create a project name from the project
            var EnvDTEProjectName = new EnvDTEProjectName(project);

            // Do nothing if we already have an entry
            if (_envDTEProjectCache.ContainsKey(EnvDTEProjectName))
            {
                return EnvDTEProjectName;
            }

            AddShortName(EnvDTEProjectName);

            _projectNamesCache[EnvDTEProjectName.CustomUniqueName] = EnvDTEProjectName;
            _projectNamesCache[EnvDTEProjectName.UniqueName] = EnvDTEProjectName;
            _projectNamesCache[EnvDTEProjectName.FullName] = EnvDTEProjectName;

            // Add the entry mapping project name to the actual project
            _envDTEProjectCache[EnvDTEProjectName] = project;

            return EnvDTEProjectName;
        }

        /// <summary>
        /// Tries to find a project by short name. Returns the project name if and only if it is non-ambiguous.
        /// </summary>
        public bool TryGetProjectNameByShortName(string name, out EnvDTEProjectName EnvDTEProjectName)
        {
            EnvDTEProjectName = null;

            HashSet<EnvDTEProjectName> projectNames;
            if (_shortNameCache.TryGetValue(name, out projectNames))
            {
                // Get the item at the front of the queue
                EnvDTEProjectName = projectNames.Count == 1 ? projectNames.Single() : null;

                // Only return true if the short name is unambiguous
                return EnvDTEProjectName != null;
            }

            return false;
        }

        /// <summary>
        /// Adds an entry to the short name cache returning any conflicting project name.
        /// </summary>
        /// <returns>The first conflicting short name.</returns>
        private void AddShortName(EnvDTEProjectName EnvDTEProjectName)
        {
            HashSet<EnvDTEProjectName> projectNames;
            if (!_shortNameCache.TryGetValue(EnvDTEProjectName.ShortName, out projectNames))
            {
                projectNames = new HashSet<EnvDTEProjectName>();
                _shortNameCache.Add(EnvDTEProjectName.ShortName, projectNames);
            }

            projectNames.Add(EnvDTEProjectName);
        }

        /// <summary>
        /// Removes a project from the short name cache.
        /// </summary>
        /// <param name="EnvDTEProjectName">The short name of the project.</param>
        private void RemoveShortName(EnvDTEProjectName EnvDTEProjectName)
        {
            HashSet<EnvDTEProjectName> projectNames;
            if (_shortNameCache.TryGetValue(EnvDTEProjectName.ShortName, out projectNames))
            {
                projectNames.Remove(EnvDTEProjectName);

                // Remove the item from the dictionary if we've removed the last project
                if (projectNames.Count == 0)
                {
                    _shortNameCache.Remove(EnvDTEProjectName.ShortName);
                }
            }
        }

        /// <summary>
        /// Removes a project from the project name dictionary.
        /// </summary>
        private void RemoveProjectName(EnvDTEProjectName EnvDTEProjectName)
        {
            _projectNamesCache.Remove(EnvDTEProjectName.CustomUniqueName);
            _projectNamesCache.Remove(EnvDTEProjectName.UniqueName);
            _projectNamesCache.Remove(EnvDTEProjectName.FullName);
            _envDTEProjectCache.Remove(EnvDTEProjectName);
        }
    }

}
