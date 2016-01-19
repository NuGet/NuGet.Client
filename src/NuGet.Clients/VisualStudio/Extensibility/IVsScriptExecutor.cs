using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains methods to execute powershell scripts from package(s) in a solution
    /// </summary>
    [ComImport]
    [Guid("8c6750b0-214f-4052-93b2-284f12ebb896")]
    public interface IVsScriptExecutor
    {
        /// <summary>
        /// Executes the init script of the given package if available.
        /// 1) If the init.ps1 script has already been executed by the powershell host, it will not be executed again.
        /// True is returned.
        /// 2) If the package is found on the packages folder, it will be used. Otherwise, the one on global packages
        /// folder will be used. If that is not found either, it will return false and do nothing.
        /// 3) Also, note if other scripts are executing while this call was made, it will wait for them to complete.
        /// </summary>
        /// <param name="packageId">Id of the package whose init.ps1 will be executed.</param>
        /// <param name="packageVersion">Version of the package whose init.ps1 will be executed.</param>
        /// <returns>Returns true if the script was executed or has been executed already.</returns>
        Task<bool> ExecuteInitScriptAsync(string packageId, string packageVersion);
    }
}
