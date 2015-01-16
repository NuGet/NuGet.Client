using System;
using System.Collections.Generic;

namespace NuGet.Client
{
    /// <summary>
    /// MEF attribute data
    /// </summary>
    public interface INuGetResourceProviderMetadata
    {
        /// <summary>
        /// Resource type provided
        /// </summary>
        Type ResourceType { get; }

        /// <summary>
        /// Name of the provider. This is used for ordering.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Names of providers this should be ordered BEFORE
        /// Gives this instance a higher priority.
        /// </summary>
        /// <remarks>If provider: default is named here, this provider will be called BEFORE default</remarks>
        IEnumerable<string> Before { get; }

        /// <summary>
        /// Names of providers this should be ordered AFTER.
        /// Gives this instance a lower priority.
        /// </summary>
        /// <remarks>If provider: default is named here, this provider will be called AFTER default</remarks>
        IEnumerable<string> After { get; }
    }
}
