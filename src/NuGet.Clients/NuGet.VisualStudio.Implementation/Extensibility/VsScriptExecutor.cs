using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsScriptExecutor))]
    public class VsScriptExecutor : IVsScriptExecutor
    {
        public Task<bool> ExecuteInitScriptAsync(string packageId, string packageVersion)
        {
            return Task.FromResult<bool>(true);
        }
    }
}
