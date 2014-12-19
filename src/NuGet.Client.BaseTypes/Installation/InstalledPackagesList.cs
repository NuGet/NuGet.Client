using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;


namespace NuGet.Client
{
    public abstract class InstalledPackagesList
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification="This method make require computation.")]
        public abstract Task<IEnumerable<JObject>> GetAllInstalledPackagesAndMetadata();

        /// <summary>
        /// Searches the list of installed packages
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="cancelToken"></param>
        /// <returns>Returns a list of JSON objects suitable for rendering by the Package Manager Dialog</returns>
        public abstract Task<IEnumerable<JObject>> Search(SourceRepository source, string searchTerm, int skip, int take, CancellationToken cancelToken);

        /// <summary>
        /// Retrieves a list of installed packages
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public abstract IEnumerable<InstalledPackageReference> GetInstalledPackages();

        /// <summary>
        /// Retrieves a list of all versions of the specified package installed into this target.
        /// </summary>
        /// <param name="packageId">The Id of the package to look for</param>
        /// <returns></returns>
        public abstract InstalledPackageReference GetInstalledPackage(string packageId);

        /// <summary>
        /// Returns a boolean indicating if ANY package with the specified ID/Version pair is installed.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="packageVersion"></param>
        /// <returns></returns>
        public abstract bool IsInstalled(string packageId, NuGetVersion packageVersion);

        /// <summary>
        /// Returns a boolean indicating if ANY package with the specified ID is installed.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        public abstract bool IsInstalled(string packageId);
    }
}
