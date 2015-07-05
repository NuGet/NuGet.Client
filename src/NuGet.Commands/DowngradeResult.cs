using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.DependencyResolver;

namespace NuGet.Commands
{
    public class DowngradeResult
    {
        public GraphNode<RemoteResolveResult> DowngradedFrom { get; set; }
        public GraphNode<RemoteResolveResult> DowngradedTo { get; set; }
    }
}
