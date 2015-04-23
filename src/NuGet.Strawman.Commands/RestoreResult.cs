using System.Collections.Generic;

namespace NuGet.Strawman.Commands
{
    public class RestoreResult
    {
        public bool Success { get; }
        
        /// <summary>
        /// Gets the resolved dependency graphs produced by the restore operation
        /// </summary>
        public IEnumerable<RestoreGraph> RestoreGraphs { get; }

        public RestoreResult(bool success, IEnumerable<RestoreGraph> restoreGraphs)
        {
            Success = success;
            RestoreGraphs = restoreGraphs;
        }
    }
}