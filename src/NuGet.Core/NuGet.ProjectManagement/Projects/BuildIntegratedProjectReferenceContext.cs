using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    public class ExternalProjectReferenceContext
    {
        /// <summary>
        /// Create a new build integrated project reference context and cache.
        /// </summary>
        public ExternalProjectReferenceContext()
            : this(NullLogger.Instance)
        {
        }

        /// <summary>
        /// Create a new build integrated project reference context and caches.
        /// </summary>
        public ExternalProjectReferenceContext(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Logger = logger;

            Cache = new Dictionary<string, IReadOnlyList<ExternalProjectReference>>(
                StringComparer.OrdinalIgnoreCase);

            SpecCache = new Dictionary<string, PackageSpec>(
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Create a new build integrated project reference context with the given caches.
        /// </summary>
        public ExternalProjectReferenceContext(
            ILogger logger,
            IDictionary<string, IReadOnlyList<ExternalProjectReference>> cache,
            IDictionary<string, PackageSpec> specCache)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (specCache == null)
            {
                throw new ArgumentNullException(nameof(specCache));
            }

            Logger = logger;
            Cache = cache;
            SpecCache = specCache;
        }

        /// <summary>
        /// Cached references
        /// </summary>
        /// <remarks>Projects should add themselves here after finding their references.</remarks>
        public IDictionary<string, IReadOnlyList<ExternalProjectReference>> Cache { get; }

        /// <summary>
        /// Cached project.json files
        /// </summary>
        public IDictionary<string, PackageSpec> SpecCache { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Retrieves a project.json file from the cache. It will be added if it does not exist already.
        /// </summary>
        public PackageSpec GetOrCreateSpec(string projectName, string projectJsonPath)
        {
            PackageSpec spec;
            if (!SpecCache.TryGetValue(projectJsonPath, out spec))
            {
                // Read the spec and add it to the cache
                spec = JsonPackageSpecReader.GetPackageSpec(
                    projectName,
                    projectJsonPath);

                SpecCache.Add(projectJsonPath, spec);
            }

            return spec;
        }
    }
}
