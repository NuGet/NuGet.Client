using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Represents the download details for a given package. This class would eventually hold the Url for the signatue file too.
    /// </summary>
    public class PackageDownloadMetadata
    {
        public Uri NupkgDownloadUrl { get; private set; }

    }
}
