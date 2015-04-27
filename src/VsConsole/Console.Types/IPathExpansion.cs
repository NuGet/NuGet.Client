using System.Threading;
using System.Threading.Tasks;

namespace NuGetConsole
{
    /// <summary>
    /// Simple path expansion interface. CommandExpansion tries path expansion
    /// if tab expansion returns no result.
    /// </summary>
    public interface IPathExpansion : ITabExpansion
    {
        Task<SimpleExpansion> GetPathExpansionsAsync(string line, CancellationToken token);
    }
}
