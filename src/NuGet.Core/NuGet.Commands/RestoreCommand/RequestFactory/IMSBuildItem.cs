using System.Collections.Generic;

namespace NuGet.Commands
{
    /// <summary>
    /// ITaskItem abstraction
    /// </summary>
    public interface IMSBuildItem
    {
        string Identity { get; }

        string GetProperty(string property);

        IReadOnlyList<string> Properties { get; }
    }
}
