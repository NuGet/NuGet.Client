using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol.ApiApps
{
    /// <summary>
    /// ApiApp package with search metadata fields
    /// </summary>
    public class ApiAppPackage : ApiAppIdentity
    {
        public ApiAppPackage(string packageNamespace, string id, NuGetVersion version)
            : base(packageNamespace, id, version)
        {
            Authors = Enumerable.Empty<string>();
            Tags = Enumerable.Empty<string>();
        }

        /// <summary>
        /// Download count for this version
        /// </summary>
        public int DownloadCount { get; internal set; }

        /// <summary>
        /// Package authors
        /// </summary>
        public IEnumerable<string> Authors { get; internal set; }

        /// <summary>
        /// Package tags
        /// </summary>
        public IEnumerable<string> Tags { get; internal set; }

        /// <summary>
        /// Package title
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// Package summary
        /// </summary>
        public string Summary { get; internal set; }

        /// <summary>
        /// Package description
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Visibility ex: public
        /// </summary>
        public string Visibility { get; internal set; }

        /// <summary>
        /// Tenant Id
        /// </summary>
        public Guid TenantId { get; internal set; }

        /// <summary>
        /// Catalog page url
        /// </summary>
        public Uri CatalogEntry { get; internal set; }

        /// <summary>
        /// Registration information
        /// </summary>
        public Uri Registration { get; internal set; }

        /// <summary>
        /// Package download url
        /// </summary>
        public Uri PackageContent { get; internal set; }

        /// <summary>
        /// Package types
        /// </summary>
        public IEnumerable<string> PackageTypes { get; internal set; }
    }
}