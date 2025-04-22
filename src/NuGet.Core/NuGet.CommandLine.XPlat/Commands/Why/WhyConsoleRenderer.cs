// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Shared;

namespace NuGet.CommandLine.XPlat.Commands.Why
{
    /// <summary>
    /// Console output renderer for 'why' command
    /// </summary>
    internal class WhyConsoleRenderer : IReportRenderer
    {
        private readonly ILoggerWithColor _logger;

        private const ConsoleColor TargetPackageColor = ConsoleColor.Cyan;

        // Dependency graph console output symbols
        private const string ChildNodeSymbol = "├─ ";
        private const string LastChildNodeSymbol = "└─ ";

        private const string ChildPrefixSymbol = "│  ";
        private const string LastChildPrefixSymbol = "   ";

        public WhyConsoleRenderer(ILoggerWithColor logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Render(WhyReportModel reportModel)
        {
            if (reportModel.DependencyGraphPerFramework.Count == 0)
            {
                _logger.LogMinimal(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Message_NoDependencyGraphsFoundInProject,
                        reportModel.ProjectName,
                        reportModel.TargetPackage));
                return;
            }

            _logger.LogMinimal(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Message_DependencyGraphsFoundInProject,
                    reportModel.ProjectName,
                    reportModel.TargetPackage));

            // print empty line
            _logger.LogMinimal("");

            // deduplicate the dependency graphs
            List<List<string>> deduplicatedFrameworks = GetDeduplicatedFrameworks(reportModel.DependencyGraphPerFramework);

            foreach (var frameworks in deduplicatedFrameworks)
            {
                if (frameworks.Count > 0)
                {
                    PrintDependencyGraphPerFramework(frameworks, reportModel.DependencyGraphPerFramework[frameworks.First()], reportModel.TargetPackage);
                }
            }
        }

        /// <summary>
        /// Prints the dependency graph for a given framework/list of frameworks.
        /// </summary>
        /// <param name="frameworks">The list of frameworks that share this dependency graph.</param>
        /// <param name="topLevelNodes">The top-level package nodes of the dependency graph.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        private void PrintDependencyGraphPerFramework(List<string> frameworks, List<DependencyNode>? topLevelNodes, string targetPackage)
        {
            // print framework header
            foreach (var framework in frameworks)
            {
                _logger.LogMinimal($"  [{framework}]");
            }

            _logger.LogMinimal($"   {ChildPrefixSymbol}");

            if (topLevelNodes == null || topLevelNodes.Count == 0)
            {
                _logger.LogMinimal($"   {LastChildNodeSymbol}{Strings.WhyCommand_Message_NoDependencyGraphsFoundForFramework}\n\n");
                return;
            }

            var stack = new Stack<StackOutputData>();

            // initialize the stack with all top-level nodes
            int counter = 0;
            foreach (var node in topLevelNodes.OrderByDescending(c => c.Id, StringComparer.OrdinalIgnoreCase))
            {
                stack.Push(new StackOutputData(node, prefix: "   ", isLastChild: counter++ == 0));
            }

            // print the dependency graph
            while (stack.Count > 0)
            {
                var current = stack.Pop();

                string currentPrefix, childPrefix;
                if (current.IsLastChild)
                {
                    currentPrefix = current.Prefix + LastChildNodeSymbol;
                    childPrefix = current.Prefix + LastChildPrefixSymbol;
                }
                else
                {
                    currentPrefix = current.Prefix + ChildNodeSymbol;
                    childPrefix = current.Prefix + ChildPrefixSymbol;
                }

                // print current node
                if (current.Node.Id.Equals(targetPackage, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogMinimal($"{currentPrefix}", Console.ForegroundColor);
                    _logger.LogMinimal($"{current.Node.Id} (v{current.Node.Version})\n", TargetPackageColor);
                }
                else
                {
                    _logger.LogMinimal($"{currentPrefix}{current.Node.Id} (v{current.Node.Version})");
                }

                if (current.Node.Children?.Count > 0)
                {
                    // push all the node's children onto the stack
                    counter = 0;
                    foreach (var child in current.Node.Children.OrderByDescending(c => c.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        stack.Push(new StackOutputData(child, childPrefix, isLastChild: counter++ == 0));
                    }
                }
            }

            _logger.LogMinimal("");
        }

        /// <summary>
        /// Deduplicates dependency graphs, and returns groups of frameworks that share the same graph.
        /// </summary>
        /// <param name="dependencyGraphPerFramework">A dictionary mapping target frameworks to their dependency graphs.</param>
        /// <returns>
        /// eg. { { "net6.0", "netcoreapp3.1" }, { "net472" } }
        /// </returns>
        private static List<List<string>> GetDeduplicatedFrameworks(Dictionary<string, List<DependencyNode>?> dependencyGraphPerFramework)
        {
            List<string>? frameworksWithoutGraphs = null;
            var dependencyGraphHashes = new Dictionary<int, List<string>>(dependencyGraphPerFramework.Count);

            foreach (var framework in dependencyGraphPerFramework.Keys)
            {
                if (dependencyGraphPerFramework[framework] == null)
                {
                    frameworksWithoutGraphs ??= [];
                    frameworksWithoutGraphs.Add(framework);
                    continue;
                }

                int hash = GetDependencyGraphHashCode(dependencyGraphPerFramework[framework]);
                if (dependencyGraphHashes.ContainsKey(hash))
                {
                    dependencyGraphHashes[hash].Add(framework);
                }
                else
                {
                    dependencyGraphHashes.Add(hash, [framework]);
                }
            }

            var deduplicatedFrameworks = dependencyGraphHashes.Values.ToList();

            if (frameworksWithoutGraphs != null)
            {
                deduplicatedFrameworks.Add(frameworksWithoutGraphs);
            }

            return deduplicatedFrameworks;
        }

        /// <summary>
        /// Returns a hash for a given dependency graph. Used to deduplicate dependency graphs for different frameworks.
        /// </summary>
        /// <param name="graph">The dependency graph for a given framework.</param>
        private static int GetDependencyGraphHashCode(List<DependencyNode>? graph)
        {
            var hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.AddUnorderedSequence(graph);
            return hashCodeCombiner.CombinedHash;
        }

        private class StackOutputData
        {
            public DependencyNode Node { get; set; }
            public string Prefix { get; set; }
            public bool IsLastChild { get; set; }

            public StackOutputData(DependencyNode node, string prefix, bool isLastChild)
            {
                Node = node;
                Prefix = prefix;
                IsLastChild = isLastChild;
            }
        }
    }
}
