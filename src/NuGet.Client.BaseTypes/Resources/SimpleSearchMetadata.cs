using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// A basic search result needed for the command line
    /// </summary>
    public class SimpleSearchMetadata
    {
        /// <summary>
        /// Package id and version
        /// </summary>
        public PackageIdentity Identity { get; private set; }

        /// <summary>
        /// Package description
        /// </summary>
        public string Description { get; private set; }


        public SimpleSearchMetadata(PackageIdentity identity, string description)
        {
            Identity = identity;
            Description = description;
        }
    }
}
