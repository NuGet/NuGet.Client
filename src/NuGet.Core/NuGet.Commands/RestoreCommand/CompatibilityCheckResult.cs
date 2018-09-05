using System.Collections.Generic;
using System.Linq;

namespace NuGet.Commands
{
    public class CompatibilityCheckResult
    {
        public RestoreTargetGraph Graph { get; }

        public IReadOnlyList<CompatibilityIssue> Issues { get; }

        public bool Success => !Issues.Any();

        public CompatibilityCheckResult(RestoreTargetGraph graph, IEnumerable<CompatibilityIssue> issues)
        {
            Graph = graph;
            Issues = issues.ToList().AsReadOnly();
        } 
    }
}