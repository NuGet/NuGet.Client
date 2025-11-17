// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using NuGet.Commands.Restore;
using NuGet.Commands.Restore.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat.Utility;

internal class DGSpecFactory
{
    /// <summary>Create a <see cref="DependencyGraphSpec"/> for the target at the given path.</summary>
    /// <param name="projectPath">A path to a solution or project file, or a directory where a solution or project exists.</param>
    /// <returns>The full path to a solution or project file. Throws is none or more than one are found</returns>
    /// <exception cref="ArgumentException">If no solution or project file is found, or if more than one is found.</exception>
    /// <exception cref="ArgumentNullException">If projectPath is null or whitespace.</exception>
    public static DependencyGraphSpec Create(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) { throw new ArgumentNullException(nameof(projectPath)); }

        IEnumerable<string> projects = MSBuildAPIUtility.GetListOfProjectsFromPathArgument(projectPath);

        var globalProperties = new Dictionary<string, string>();

        bool IsInnerBuild(ProjectGraphNode node)
        {
            return node.ProjectInstance.GlobalProperties.ContainsKey("TargetFramework");
        }

        var projectGraph = new ProjectGraph(projects, globalProperties, ProjectCollection.GlobalProjectCollection);
        Dictionary<string, (ProjectGraphNode? outerBuild, List<ProjectGraphNode> InnerBuilds)> nodesByProject = new(PathUtility.GetStringComparerBasedOnOS());
        foreach (var node in projectGraph.ProjectNodes)
        {
            if (IsInnerBuild(node))
            {
                if (nodesByProject.TryGetValue(node.ProjectInstance.FullPath, out var entry))
                {
                    entry.InnerBuilds.Add(node);
                }
                else
                {
                    nodesByProject[node.ProjectInstance.FullPath] = (null, new List<ProjectGraphNode> { node });
                }
            }
            else
            {
                if (nodesByProject.TryGetValue(node.ProjectInstance.FullPath, out var entry))
                {
                    entry.outerBuild = node;
                    nodesByProject[node.ProjectInstance.FullPath] = entry;
                }
                else
                {
                    nodesByProject[node.ProjectInstance.FullPath] = (node, new List<ProjectGraphNode>());
                }
            }
        }

        var dgSpec = new DependencyGraphSpec();
        var settings = Settings.LoadDefaultSettings(Path.GetDirectoryName(projectPath));

        var projectsByFullPath = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectNodes in nodesByProject.Values)
        {
            var outerBuild = projectNodes.outerBuild!.ProjectInstance;
            var innerBuilds = new Dictionary<string, ITargetFramework>(Math.Max(1, projectNodes.InnerBuilds.Count));
            if (projectNodes.InnerBuilds.Count == 0)
            {
                // Single TFM project
                var targetFramework = new MSBuildTargetFramework(outerBuild);
                var tfm = outerBuild.GetPropertyValue("TargetFramework") ?? string.Empty;
                innerBuilds.Add(tfm, targetFramework);
            }
            else
            {
                foreach (var innerBuildNode in projectNodes.InnerBuilds)
                {
                    var targetFramework = new MSBuildTargetFramework(innerBuildNode.ProjectInstance);
                    var tfm = innerBuildNode.ProjectInstance.GetPropertyValue("TargetFramework");
                    innerBuilds.Add(tfm, targetFramework);
                }
            }

            var msbuildProject = new MSBuildProject(outerBuild, innerBuilds);
            var packageSpec = PackageSpecFactory.GetPackageSpec(msbuildProject, settings);

            if (packageSpec != null)
            {
                dgSpec.AddProject(packageSpec);
            }
        }

        foreach (var project in projectGraph.EntryPointNodes)
        {
            dgSpec.AddRestore(project.ProjectInstance.FullPath);
        }

        return dgSpec;
    }

    private class MSBuildProject : IProject
    {
        private readonly ProjectInstance _project;
        private readonly IReadOnlyDictionary<string, ITargetFramework> _targetFrameworks;

        public MSBuildProject(ProjectInstance project, IReadOnlyDictionary<string, ITargetFramework> targetFrameworks)
        {
            _project = project;
            _targetFrameworks = targetFrameworks;
            FullPath = project.FullPath;
            Directory = project.Directory;
            OuterBuild = new MSBuildTargetFramework(project);
        }

        public string FullPath { get; }
        public string Directory { get; }
        public ITargetFramework OuterBuild { get; }
        public IReadOnlyDictionary<string, ITargetFramework> TargetFrameworks => _targetFrameworks;
    }

    private class MSBuildTargetFramework : ITargetFramework
    {
        private readonly ProjectInstance _project;

        public MSBuildTargetFramework(ProjectInstance project)
        {
            _project = project;
        }

        public string? GetProperty(string propertyName)
        {
            var value = _project.GetPropertyValue(propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return value;
        }

        public IReadOnlyList<IItem> GetItems(string itemType)
        {
            var items = _project.GetItems(itemType);
            if (items == null || !items.Any())
            {
                return Array.Empty<IItem>();
            }

            return items.Select(i => (IItem)new MSBuildItem(i)).ToList();
        }
    }

    private class MSBuildItem : IItem
    {
        private readonly ProjectItemInstance _item;

        public MSBuildItem(ProjectItemInstance item)
        {
            _item = item;
            Identity = item.EvaluatedInclude;
        }

        public string Identity { get; }

        public string? GetMetadata(string name)
        {
            var value = _item.GetMetadataValue(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return value;
        }
    }
}
