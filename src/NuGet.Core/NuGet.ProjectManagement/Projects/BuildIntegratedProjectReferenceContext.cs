using System;
using System.Collections.Generic;
using NuGet.Logging;

namespace NuGet.ProjectManagement
{
    public class BuildIntegratedProjectReferenceContext
    {
        /// <summary>
        /// Create a new build integrated project reference context and cache.
        /// </summary>
        public BuildIntegratedProjectReferenceContext()
            : this(NullLogger.Instance)
        {
        }

        /// <summary>
        /// Create a new build integrated project reference context and cache.
        /// </summary>
        public BuildIntegratedProjectReferenceContext(ILogger logger)
            : this(logger, new Dictionary<string, IReadOnlyList<BuildIntegratedProjectReference>>(
                StringComparer.OrdinalIgnoreCase))
        {
        }

        /// <summary>
        /// Create a new build integrated project reference context with the given cache.
        /// </summary>
        public BuildIntegratedProjectReferenceContext(
            ILogger logger,
            IDictionary<string, IReadOnlyList<BuildIntegratedProjectReference>> cache)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            Logger = logger;
            Cache = cache;
        }

        /// <summary>
        /// Cached references
        /// </summary>
        /// <remarks>Projects should add themselves here after finding their references.</remarks>
        public IDictionary<string, IReadOnlyList<BuildIntegratedProjectReference>> Cache { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }
    }
}
