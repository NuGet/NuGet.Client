using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsGlobalPackagesInitScriptExecutor))]
    public class VsGlobalPackagesInitScriptExecutor : IVsGlobalPackagesInitScriptExecutor
    {
        private IScriptExecutor ScriptExecutor { get; }

        [ImportingConstructor]
        public VsGlobalPackagesInitScriptExecutor(IScriptExecutor scriptExecutor)
        {
            if (scriptExecutor == null)
            {
                throw new ArgumentNullException(nameof(scriptExecutor));
            }

            ScriptExecutor = scriptExecutor;
        }

        public Task<bool> ExecuteInitScriptAsync(string packageId, string packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageId));
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageVersion));
            }

            var version = new NuGetVersion(packageVersion);
            var packageIdentity = new PackageIdentity(packageId, version);
            return ScriptExecutor.ExecuteInitScriptAsync(packageIdentity);
        }
    }
}
