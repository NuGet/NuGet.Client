using System.Collections.Generic;

namespace NuGet.Commands
{
    /// <summary>
    /// ITaskItem abstraction
    /// </summary>
    public interface IMSBuildItem
    {
        /// <summary>
        /// Include attribute value.
        /// </summary>
        string Identity { get; }

        /// <summary>
        /// Retrieve property value and trim.
        /// </summary>
        string GetProperty(string property);

        /// <summary>
        /// Retrieve property value with optional trimming.
        /// </summary>
        string GetProperty(string property, bool trim);

        /// <summary>
        /// Raw untrimmed properties.
        /// </summary>
        IReadOnlyList<string> Properties { get; }
    }
}
