﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Cache that stores project based on multiple names. i.e. EnvDTEProject can be retrieved by name (if non
    /// conflicting), unique name and custom unique name.
    /// Projects are added from the main thread, on solution opened event, so concurrent dictionaries are not
    /// needed
    /// </summary>
    internal class NuGetAndEnvDTEProjectCache
    {
        private readonly Dictionary<EnvDTEProjectName, NuGetProject> _nuGetProjectCache = new Dictionary<EnvDTEProjectName, NuGetProject>();

        // Mapping from project name structure to project instance
        private readonly Dictionary<EnvDTEProjectName, EnvDTEProject> _envDTEProjectCache = new Dictionary<EnvDTEProjectName, EnvDTEProject>();

        // Mapping from all names to a project name structure
        private readonly Dictionary<string, EnvDTEProjectName> _projectNamesCache = new Dictionary<string, EnvDTEProjectName>(StringComparer.OrdinalIgnoreCase);

        // We need another dictionary for short names since there may be more than project name per short name
        private readonly Dictionary<string, HashSet<EnvDTEProjectName>> _shortNameCache = new Dictionary<string, HashSet<EnvDTEProjectName>>(StringComparer.OrdinalIgnoreCase);

        public bool IsInitialized { get; set; }

        public bool TryGetNuGetProject(string name, out NuGetProject nuGetProject)
        {
            nuGetProject = null;
            // First try to find the project name in one of the dictionaries. Then locate the project for that name.
            EnvDTEProjectName envDTEProjectName;

            return TryGetNuGetProjectName(name, out envDTEProjectName) &&
                   _nuGetProjectCache.TryGetValue(envDTEProjectName, out nuGetProject);
        }

        public bool TryGetDTEProject(string name, out EnvDTEProject project)
        {
            project = null;
            // First try to find the project name in one of the dictionaries. Then locate the project for that name.
            EnvDTEProjectName envDTEProjectName;
            return TryGetNuGetProjectName(name, out envDTEProjectName) &&
                   _envDTEProjectCache.TryGetValue(envDTEProjectName, out project);
        }

        /// <summary>
        /// Finds a project name by short name, unique name or custom unique name.
        /// </summary>
        /// <param name="name">name of the project</param>
        /// <param name="EnvDTEProjectName">project name instance</param>
        /// <returns>true if the project name with the specified name is found.</returns>
        public bool TryGetNuGetProjectName(string name, out EnvDTEProjectName EnvDTEProjectName)
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

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            return _nuGetProjectCache.Values;
        }

        /// <summary>
        /// Returns all cached projects.
        /// </summary>
        public IEnumerable<EnvDTEProject> GetEnvDTEProjects()
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
        /// Adds a project to the project cache. If the project already exists in the cache, this
        /// this operation does nothing.
        /// </summary>
        /// <param name="projectName">The project name.</param>
        /// <param name="project">The VS project.</param>
        /// <param name="nuGetProject">The NuGet project.</param>
        /// <returns>
        /// Returns true if the project was newly added to the cache. Returns false if the project
        /// was already in the cache.
        /// </returns>
        public bool AddProject(EnvDTEProjectName projectName, EnvDTEProject project, NuGetProject nuGetProject)
        {
            // Do nothing if we already have an entry
            if (_envDTEProjectCache.ContainsKey(projectName))
            {
                return false;
            }

            // Add the project to the cache.
            AddShortName(projectName);

            _projectNamesCache[projectName.CustomUniqueName] = projectName;
            _projectNamesCache[projectName.UniqueName] = projectName;
            _projectNamesCache[projectName.FullName] = projectName;

            _nuGetProjectCache[projectName] = nuGetProject;

            // Add the entry mapping project name to the actual project
            _envDTEProjectCache[projectName] = project;

            return true;
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
        /// <param name="envDTEProjectName">The short name of the project.</param>
        private void RemoveShortName(EnvDTEProjectName envDTEProjectName)
        {
            HashSet<EnvDTEProjectName> projectNames;
            if (_shortNameCache.TryGetValue(envDTEProjectName.ShortName, out projectNames))
            {
                projectNames.Remove(envDTEProjectName);

                // Remove the item from the dictionary if we've removed the last project
                if (projectNames.Count == 0)
                {
                    _shortNameCache.Remove(envDTEProjectName.ShortName);
                }
            }
        }

        /// <summary>
        /// Removes a project from the project name dictionary.
        /// </summary>
        private void RemoveProjectName(EnvDTEProjectName envDTEProjectName)
        {
            _projectNamesCache.Remove(envDTEProjectName.CustomUniqueName);
            _projectNamesCache.Remove(envDTEProjectName.UniqueName);
            _projectNamesCache.Remove(envDTEProjectName.FullName);
            _envDTEProjectCache.Remove(envDTEProjectName);
            _nuGetProjectCache.Remove(envDTEProjectName);
        }

        public void Clear()
        {
            _nuGetProjectCache.Clear();
            _envDTEProjectCache.Clear();
            _projectNamesCache.Clear();
            _shortNameCache.Clear();
            IsInitialized = false;
        }
    }
}
