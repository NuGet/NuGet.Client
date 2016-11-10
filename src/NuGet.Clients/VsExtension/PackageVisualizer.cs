using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.Commands;
using System.Threading;
using Microsoft.TeamFoundation.Common;
using NuGet.PackageManagement.UI;
using NuGet.Packaging.Core;

namespace NuGetVSExtension
{
    public class PackageVisualizer
    {
        private const string DgmlNs = "http://schemas.microsoft.com/vs/2009/dgml";

        private readonly ISolutionManager _solutionManager;
        private readonly INuGetUIContextFactory _contextFactory;
        private readonly NuGetPackage _package;
        public PackageVisualizer(ISolutionManager solutionManager, INuGetUIContextFactory contextFactory, NuGetPackage package)
        {
            _solutionManager = solutionManager;
            _contextFactory = contextFactory;
            _package = package;
        }

        public string CreateGraph()
        {
            var links = new List<DGMLLink>();
            var nodes = new Dictionary<string, DGMLNode>();
            var nugetProjects = _solutionManager.GetNuGetProjects();
            foreach (NuGetProject nugetProject in nugetProjects)
            {
                
                INuGetUIContext context = _contextFactory.Create(_package, new[] { nugetProject });
                var dependencyInfosTask = context.PackageManager.GetInstalledPackagesDependencyInfo(nugetProject, CancellationToken.None);
                dependencyInfosTask.Wait();
                var dependencyInfos = dependencyInfosTask.Result;

                if (dependencyInfos.IsNullOrEmpty()) // no dependency skip
                {
                    continue;
                }

                DGMLNode project = new DGMLNode()
                {
                    Name = nugetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName),
                    Label = nugetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name),
                    Category = Resources.Visualizer_Project
                };
                nodes.Add(nugetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName), project);

                var directDependencies = new HashSet<string>();

                foreach (PackageDependencyInfo packageDependencyInfo in dependencyInfos)
                {
                    if (!nodes.ContainsKey(packageDependencyInfo.Id)) // first time we encounter this package in the solution
                    {
                        DGMLNode package = new DGMLNode()
                        {
                            Name = packageDependencyInfo.Id,
                            Label =
                                packageDependencyInfo.Id + " " +
                                (packageDependencyInfo.HasVersion ? packageDependencyInfo.Version.ToString() : "")
                        };
                        nodes.Add(package.Name, package);
                    }
                    directDependencies.Add(packageDependencyInfo.Id);
                }

                foreach (PackageDependencyInfo packageDependencyInfo in dependencyInfos)
                {
                    DGMLNode parent = nodes.GetValueOrDefault(packageDependencyInfo.Id);
                    foreach (PackageDependency dependency in packageDependencyInfo.Dependencies)
                    {
                        DGMLNode child = nodes.GetValueOrDefault(dependency.Id);

                        if (true) //TODO NK - check that the versions are satisfied
                        {
                            child.Category = Resources.Visualizer_Package;
                            directDependencies.Remove(dependency.Id);
                            links.Add(new DGMLLink
                            {
                                SourceName = parent.Name,
                                DestName = child.Name,
                                Category = Resources.Visualizer_PackageDependency
                            });
                        }
                    }
                }

                foreach (string directPackageDependency in directDependencies)
                {
                    DGMLNode directPackage = nodes.GetValueOrDefault(directPackageDependency);
                    directPackage.Category = Resources.Visualizer_InstalledPackage;

                    links.Add(new DGMLLink() { SourceName = project.Name, DestName = directPackage.Name, Category = Resources.Visualizer_PackageDependency });
                }
            }
            return GenerateDgml(nodes.Values.ToList(), links);
        }

        private string GenerateDgml(List<DGMLNode> nodes, List<DGMLLink> links)
        {
            bool hasDependencies = links.Any(l => l.Category == Resources.Visualizer_PackageDependency);
            var document = new XDocument(
                new XElement(XName.Get("DirectedGraph", DgmlNs),
                    new XAttribute("GraphDirection", "LeftToRight"),
                    new XElement(XName.Get("Nodes", DgmlNs),
                        from item in nodes select new XElement(XName.Get("Node", DgmlNs), new XAttribute("Id", item.Name), new XAttribute("Label", item.Label), new XAttribute("Category", item.Category))),
                    new XElement(XName.Get("Links", DgmlNs),
                        from item in links
                        select new XElement(XName.Get("Link", DgmlNs), new XAttribute("Source", item.SourceName), new XAttribute("Target", item.DestName),
                            new XAttribute("Category", item.Category))),
                    new XElement(XName.Get("Categories", DgmlNs),
                        new XElement(XName.Get("Category", DgmlNs), new XAttribute("Id", Resources.Visualizer_Project)),
                        new XElement(XName.Get("Category", DgmlNs), new XAttribute("Id", Resources.Visualizer_Package))),
                    new XElement(XName.Get("Styles", DgmlNs),
                        StyleElement(Resources.Visualizer_Project, "Node", "Background", "Blue"),
                        hasDependencies ? StyleElement(Resources.Visualizer_PackageDependency, "Link", "Background", "Yellow") : null))
            );
            var saveFilePath = Path.Combine(_solutionManager.SolutionDirectory, "Packages.dgml");
            document.Save(saveFilePath);
            return saveFilePath;
        }

        private static XElement StyleElement(string category, string targetType, string propertyName, string propertyValue)
        {
            return new XElement(XName.Get("Style", DgmlNs), new XAttribute("TargetType", targetType), new XAttribute("GroupLabel", category), new XAttribute("ValueLabel", "True"),
                    new XElement(XName.Get("Condition", DgmlNs), new XAttribute("Expression", String.Format(CultureInfo.InvariantCulture, "HasCategory('{0}')", category))),
                    new XElement(XName.Get("Setter", DgmlNs), new XAttribute("Property", propertyName), new XAttribute("Value", propertyValue)));
        }

        private class DGMLNode : IEquatable<DGMLNode>
        {
            public string Name { get; set; }

            public string Label { get; set; }

            public string Category { get; set; }

            public bool Equals(DGMLNode other)
            {
                return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        private class DGMLLink
        {
            public string SourceName { get; set; }

            public string DestName { get; set; }

            public string Category { get; set; }
        }
    }
}