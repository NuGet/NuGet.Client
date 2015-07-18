using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.DependencyResolver;

namespace NuGet.Commands
{
    public class VersionConflictResult
    {
        public GraphNode<RemoteResolveResult> Selected { get; set; }
        public GraphNode<RemoteResolveResult> Conflicting { get; set; }
    }
}
