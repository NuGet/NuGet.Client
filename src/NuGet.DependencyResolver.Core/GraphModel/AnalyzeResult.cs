using System;
using System.Collections.Generic;

namespace NuGet.DependencyResolver
{
    public class AnalyzeResult<TItem>
    {
        public List<Tuple<GraphNode<TItem>, GraphNode<TItem>>> Downgrades { get; private set; }
        public List<Tuple<GraphNode<TItem>, GraphNode<TItem>>> VersionConflicts { get; private set; }
        public List<GraphNode<TItem>> Cycles { get; private set; }

        public AnalyzeResult()
        {
            Downgrades = new List<Tuple<GraphNode<TItem>, GraphNode<TItem>>>();
            VersionConflicts = new List<Tuple<GraphNode<TItem>, GraphNode<TItem>>>();
            Cycles = new List<GraphNode<TItem>>();
        }
    }
}
