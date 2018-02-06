using System.Diagnostics;
using Apex.NuGetClient.Host;

namespace Apex.NuGetClient.PackageManageUI
{
    /// <summary>
    /// Interface for NuGet Package Manage UI Service
    /// </summary>
    public interface IPackageManageUIService
    {
        /// <summary>
        /// Get the package manage UI host for the given project
        /// </summary>
        /// <param name="projectName">The project name</param>
        /// <param name="packageManageUIHostProcess">the process hosting the package manage UI(do we need this one?)</param>
        /// <returns></returns>
        PackageManageUIHost GetPackageManageUIHost(string projectName, Process packageManageUIHostProcess);
    }
}
