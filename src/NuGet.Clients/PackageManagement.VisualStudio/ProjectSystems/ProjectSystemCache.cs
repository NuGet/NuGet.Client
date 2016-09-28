// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.VisualStudio
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IProjectSystemCache))]
    internal class ProjectSystemCache : IProjectSystemCache
    {
        private readonly Dictionary<ProjectNames, CacheEntry> _primaryCache = new Dictionary<ProjectNames, CacheEntry>();
        private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        // Secondary index. Mapping from all names to a project name structure
        private readonly Dictionary<string, ProjectNames> _projectNamesCache = new Dictionary<string, ProjectNames>(StringComparer.OrdinalIgnoreCase);

        // Non-unique names index. We need another dictionary for short names since there may be more than project name per short name
        private readonly Dictionary<string, HashSet<ProjectNames>> _shortNameCache = new Dictionary<string, HashSet<ProjectNames>>(StringComparer.OrdinalIgnoreCase);

        public bool TryGetNuGetProject(string name, out NuGetProject project)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            project = null;

            CacheEntry cacheEntry;
            if (TryGetCacheEntry(name, out cacheEntry))
            {
                project = cacheEntry.NuGetProject;
            }

            return project != null;
        }

        public bool TryGetDTEProject(string name, out EnvDTE.Project project)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            project = null;

            CacheEntry cacheEntry;
            if (TryGetCacheEntry(name, out cacheEntry))
            {
                project = cacheEntry.EnvDTEProject;
            }

            return project != null;
        }

        public bool TryGetProjectRestoreInfo(String name, out PackageSpec packageSpec)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            packageSpec = null;

            CacheEntry cacheEntry;
            if (TryGetCacheEntry(name, out cacheEntry))
            {
                packageSpec = cacheEntry.ProjectRestoreInfo;
            }

            return packageSpec != null;
        }

        public bool TryGetProjectNames(string name, out ProjectNames projectNames)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _readerWriterLock.EnterReadLock();

            try
            {
                return _projectNamesCache.TryGetValue(name, out projectNames) ||
                       TryGetProjectNameByShortName(name, out projectNames);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        public void RemoveProject(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _readerWriterLock.EnterUpgradeableReadLock();

            try
            {
                ProjectNames projectNames;
                if (_projectNamesCache.TryGetValue(name, out projectNames))
                {
                    _readerWriterLock.EnterWriteLock();

                    try
                    {
                        RemoveProjectName(projectNames);
                        RemoveShortName(projectNames);
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _readerWriterLock.ExitUpgradeableReadLock();
            }
        }

        public bool ContainsKey(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _readerWriterLock.EnterReadLock();

            try
            {
                return _projectNamesCache.ContainsKey(name) ||
                       _shortNameCache.ContainsKey(name);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        public IReadOnlyList<NuGetProject> GetNuGetProjects()
        {
            _readerWriterLock.EnterReadLock();

            try
            {
                return _primaryCache
                    .Select(kv => kv.Value.NuGetProject)
                    .ToList();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        public IReadOnlyList<EnvDTE.Project> GetEnvDTEProjects()
        {
            _readerWriterLock.EnterReadLock();

            try
            {
                return _primaryCache
                    .Select(kv => kv.Value.EnvDTEProject)
                    .ToList();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        public bool IsAmbiguous(string shortName)
        {
            if (shortName == null)
            {
                throw new ArgumentNullException(nameof(shortName));
            }

            _readerWriterLock.EnterReadLock();

            try
            {
                HashSet<ProjectNames> values;
                if (_shortNameCache.TryGetValue(shortName, out values))
                {
                    return values.Count > 1;
                }
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            return false;
        }

        public bool AddProject(ProjectNames projectNames, EnvDTE.Project dteProject, NuGetProject nuGetProject)
        {
            if (projectNames == null)
            {
                throw new ArgumentNullException(nameof(projectNames));
            }

            _readerWriterLock.EnterWriteLock();

            try
            {
                UpdateProjectNamesCache(projectNames);

                AddOrUpdateCacheEntry(
                    projectNames,
                    addEntryFactory: k => new CacheEntry
                    {
                        NuGetProject = nuGetProject,
                        EnvDTEProject = dteProject
                    },
                    updateEntryFactory: (k, e) =>
                    {
                        e.NuGetProject = nuGetProject;
                        e.EnvDTEProject = dteProject;
                        return e;
                    });
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

            return true;
        }

        public bool AddProjectRestoreInfo(ProjectNames projectNames, PackageSpec packageSpec)
        {
            if (projectNames == null)
            {
                throw new ArgumentNullException(nameof(projectNames));
            }

            _readerWriterLock.EnterWriteLock();

            try
            {
                UpdateProjectNamesCache(projectNames);

                AddOrUpdateCacheEntry(
                    projectNames,
                    addEntryFactory: k => new CacheEntry
                    {
                        ProjectRestoreInfo = packageSpec
                    },
                    updateEntryFactory: (k, e) =>
                    {
                        e.ProjectRestoreInfo = packageSpec;
                        return e;
                    });
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

            return true;
        }

        private CacheEntry AddOrUpdateCacheEntry(
            ProjectNames primaryKey, 
            Func<ProjectNames, CacheEntry> addEntryFactory,
            Func<ProjectNames, CacheEntry, CacheEntry> updateEntryFactory)
        {
            Debug.Assert(_readerWriterLock.IsWriteLockHeld);

            CacheEntry newCacheEntry, oldCacheEntry;
            if(_primaryCache.TryGetValue(primaryKey, out oldCacheEntry))
            {
                newCacheEntry = updateEntryFactory(primaryKey, oldCacheEntry);
                _primaryCache[primaryKey] = newCacheEntry;
            }
            else
            {
                newCacheEntry = addEntryFactory(primaryKey);
                _primaryCache.Add(primaryKey, newCacheEntry);
            }


            return newCacheEntry;
        }

        private void UpdateProjectNamesCache(ProjectNames projectNames)
        {
            Debug.Assert(_readerWriterLock.IsWriteLockHeld);

            AddShortName(projectNames);

            _projectNamesCache[projectNames.CustomUniqueName] = projectNames;
            _projectNamesCache[projectNames.UniqueName] = projectNames;
            _projectNamesCache[projectNames.FullName] = projectNames;
        }

        public bool TryGetProjectNameByShortName(string name, out ProjectNames projectNames)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            projectNames = null;

            _readerWriterLock.EnterReadLock();

            try
            {
                HashSet<ProjectNames> values;
                if (_shortNameCache.TryGetValue(name, out values))
                {
                    // Get the item at the front of the queue
                    projectNames = values.Count == 1 ? values.Single() : null;

                    // Only return true if the short name is unambiguous
                    return projectNames != null;
                }
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            return false;
        }

        /// <summary>
        /// Adds an entry to the short name cach.
        /// </summary>
        private void AddShortName(ProjectNames projectNames)
        {
            Debug.Assert(_readerWriterLock.IsWriteLockHeld);

            HashSet<ProjectNames> values;
            if (!_shortNameCache.TryGetValue(projectNames.ShortName, out values))
            {
                values = new HashSet<ProjectNames>();
                _shortNameCache.Add(projectNames.ShortName, values);
            }

            values.Add(projectNames);
        }

        /// <summary>
        /// Removes a project from the short name cache.
        /// </summary>
        /// <param name="projectNames">The short name of the project.</param>
        private void RemoveShortName(ProjectNames projectNames)
        {
            Debug.Assert(_readerWriterLock.IsWriteLockHeld);

            HashSet<ProjectNames> values;
            if (_shortNameCache.TryGetValue(projectNames.ShortName, out values))
            {
                values.Remove(projectNames);

                // Remove the item from the dictionary if we've removed the last project
                if (values.Count == 0)
                {
                    _shortNameCache.Remove(projectNames.ShortName);
                }
            }
        }

        /// <summary>
        /// Removes a project from the project name dictionary.
        /// </summary>
        private void RemoveProjectName(ProjectNames projectNames)
        {
            Debug.Assert(_readerWriterLock.IsWriteLockHeld);

            _projectNamesCache.Remove(projectNames.CustomUniqueName);
            _projectNamesCache.Remove(projectNames.UniqueName);
            _projectNamesCache.Remove(projectNames.FullName);
            _primaryCache.Remove(projectNames);
        }

        public void Clear()
        {
            _readerWriterLock.EnterWriteLock();

            try
            {
                _primaryCache.Clear();
                _projectNamesCache.Clear();
                _shortNameCache.Clear();
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }
        }

        private bool TryGetCacheEntry(ProjectNames primaryKey, out CacheEntry cacheEntry)
        {
            _readerWriterLock.EnterReadLock();

            try
            {
                return _primaryCache.TryGetValue(primaryKey, out cacheEntry);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        private bool TryGetCacheEntry(string secondaryKey, out CacheEntry cacheEntry)
        {
            cacheEntry = null;

            _readerWriterLock.EnterReadLock();

            try
            {
                ProjectNames primaryKey;
                if (_projectNamesCache.TryGetValue(secondaryKey, out primaryKey))
                {
                    return _primaryCache.TryGetValue(primaryKey, out cacheEntry);
                }
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            return false;
        }

        private class CacheEntry
        {
            public NuGetProject NuGetProject { get; set; }
            public EnvDTE.Project EnvDTEProject { get; set; }
            public PackageSpec ProjectRestoreInfo { get; set; }
        }
    }
}
