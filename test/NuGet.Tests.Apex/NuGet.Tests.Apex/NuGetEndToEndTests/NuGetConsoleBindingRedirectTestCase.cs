// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.CSharp;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;
using NuGet.Versioning;

namespace NuGet.Tests.Apex
{
    public partial class NuGetConsoleTestCase
    {
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectToWebApplicationAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var webSite = AddProject(testContext, ProjectTemplate.WebSiteEmpty, "WebSite");
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirects");
            var console = GetConsole(testContext.Project);

            foreach (var project in new[] { testContext.Project, webSite })
            {
                Install(console, project, "B", "2.0", testContext.PackageSource);
                Install(console, project, "A", "1.0", testContext.PackageSource);

                AssertAssemblyReference(project, "A", "1.0.0.0");
                AssertAssemblyReference(project, "B", "2.0.0.0");
                AssertBindingRedirect(project, "web.config", "B", "0.0.0.0-2.0.0.0", "2.0.0.0");
            }
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectToStandaloneWebSiteAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebSiteEmpty);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsWebsite");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "E", "1.0", testContext.PackageSource);
            Update(console, testContext.Project, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectToPrimaryWebConfigAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsMultipleConfigs");
            var projectDirectory = Path.GetDirectoryName(testContext.Project.FullPath)!;
            var nestedDirectory = Path.Combine(projectDirectory, "test");
            Directory.CreateDirectory(nestedDirectory);
            var nestedWebConfigPath = Path.Combine(nestedDirectory, "web.config");
            File.WriteAllText(nestedWebConfigPath, "<configuration />");
            var dteProject = VisualStudio.Dte.Solution.Projects
                .Cast<EnvDTE.Project>()
                .Single(p => p.UniqueName == testContext.Project.UniqueName);
            dteProject.ProjectItems.AddFromFile(nestedWebConfigPath);
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "B", "2.0", testContext.PackageSource);
            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource);

            AssertBindingRedirect(testContext.Project, "web.config", "B", "0.0.0.0-2.0.0.0", "2.0.0.0");
            File.ReadAllText(nestedWebConfigPath).Should().NotContain("bindingRedirect");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCAddsBindingRedirectToClassLibraryAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ClassLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsClassLibraryUpdatePackage");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "E", "1.0", testContext.PackageSource);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "E", Logger);
            AssertAssemblyReference(testContext.Project, "E", "1.0.0.0");
            AssertAssemblyReference(testContext.Project, "F", "1.0.0.0");
            AssertProjectFileDoesNotExist(testContext.Project, "app.config");

            Update(console, testContext.Project, "F", testContext.PackageSource, "-Safe");

            AssertAssemblyReference(testContext.Project, "F", "1.0.5.0");
            AssertBindingRedirect(testContext.Project, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectThroughProjectReferenceAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var webSite = AddProject(testContext, ProjectTemplate.WebSiteEmpty, "WebSite");
            var webApplicationLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "WebApplicationLibrary", ProjectTargetFramework.V46);
            var webSiteLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "WebSiteLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(webApplicationLibrary);
            AddWebSiteProjectReference(webSite, webSiteLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsClassLibraryReference");
            var console = GetConsole(testContext.Project);

            foreach (var classLibrary in new[] { webApplicationLibrary, webSiteLibrary })
            {
                Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
                Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
                AssertAssemblyReference(classLibrary, "E", "1.0.0.0");
                AssertBindingRedirect(classLibrary, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
                AssertProjectFileDoesNotExist(classLibrary, "web.config");
            }
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(webSite, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectThroughIndirectProjectReferenceAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var middleLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "MiddleLibrary", ProjectTargetFramework.V46);
            var leafLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "LeafLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(middleLibrary);
            middleLibrary.References.Dte.AddProjectReference(leafLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsIndirectReference");
            var console = GetConsole(testContext.Project);

            Install(console, leafLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, leafLibrary, "F", testContext.PackageSource, "-Safe");

            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(middleLibrary, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(leafLibrary, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertProjectFileDoesNotExist(middleLibrary, "web.config");
            AssertProjectFileDoesNotExist(leafLibrary, "web.config");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectThroughComplexProjectGraphAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var consoleProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApplication");
            var classLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "ClassLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(consoleProject);
            consoleProject.References.Dte.AddProjectReference(classLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectComplex");
            var console = GetConsole(testContext.Project);

            Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(consoleProject, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectToConsoleApplicationAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ConsoleApplication);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "SimpleBindingRedirectsNonWeb");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "E", "1.0", testContext.PackageSource);
            Update(console, testContext.Project, "F", testContext.PackageSource, "-Safe");

            AssertBindingRedirect(testContext.Project, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectThroughLongProjectReferenceChainAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var previous = testContext.Project;
            ProjectTestExtension leaf = null!;
            for (var i = 0; i < 26; i++)
            {
                leaf = AddProject(testContext, ProjectTemplate.ClassLibrary, $"ClassLibrary{i}", ProjectTargetFramework.V46);
                previous.References.Dte.AddProjectReference(leaf);
                previous = leaf;
            }

            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectInstallLargeProject");
            var console = GetConsole(testContext.Project);

            Install(console, leaf, "E", "1.0", testContext.PackageSource);
            Update(console, leaf, "F", testContext.PackageSource, "-Safe");

            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectWithDuplicateReferencesAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var consoleProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApplication");
            var classLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "ClassLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(consoleProject);
            consoleProject.References.Dte.AddProjectReference(classLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectDuplicateReferences");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, consoleProject, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(consoleProject, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectForDifferentDependentProjectsAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var consoleProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApplication");
            var classLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "ClassLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(classLibrary);
            consoleProject.References.Dte.AddProjectReference(classLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectClassLibraryWithDifferentDependents");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, consoleProject, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(consoleProject, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectWithAssemblyReferenceFromDifferentLocationAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var consoleProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApplication");
            var classLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "ClassLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(consoleProject);
            consoleProject.References.Dte.AddProjectReference(classLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectProjectsThatReferenceSameAssemblyFromDifferentLocations");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            var packageAssembly = Directory.GetFiles(testContext.SolutionRoot, "A.dll", SearchOption.AllDirectories).First();
            var copiedAssembly = Path.Combine(testContext.SolutionRoot, "A.dll");
            File.Copy(packageAssembly, copiedAssembly, overwrite: true);
            ((dynamic)VisualStudio.Dte.Solution.Projects.Item(2).Object).References.Add(copiedAssembly);
            Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(consoleProject, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectWithDifferentAssemblyVersionsAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            var consoleProject = AddProject(testContext, ProjectTemplate.ConsoleApplication, "ConsoleApplication");
            var classLibrary = AddProject(testContext, ProjectTemplate.ClassLibrary, "ClassLibrary", ProjectTargetFramework.V46);
            testContext.Project.References.Dte.AddProjectReference(consoleProject);
            consoleProject.References.Dte.AddProjectReference(classLibrary);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "BindingRedirectProjectsThatReferenceDifferentVersionsOfSameAssembly");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "A", "2.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, consoleProject, "A", "1.0", testContext.PackageSource, "-IgnoreDependencies");
            Install(console, classLibrary, "E", "1.0", testContext.PackageSource);
            Update(console, classLibrary, "F", testContext.PackageSource, "-Safe");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, classLibrary, "E", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertBindingRedirect(consoleProject, "app.config", "F", "0.0.0.0-1.0.5.0", "1.0.5.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectForMixedStrongAndNonStrongNamedAssembliesAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ConsoleApplication);
            CopyEndToEndPackagesToSource(
                testContext.PackageSource,
                "PackageWithNonStrongNamedLibA.1.0.nupkg",
                "PackageWithNonStrongNamedLibB.1.0.nupkg",
                "PackageWithStrongNamedLib.1.0.nupkg");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "PackageWithNonStrongNamedLibA", "1.0", testContext.PackageSource);
            Install(console, testContext.Project, "PackageWithNonStrongNamedLibB", "1.0", testContext.PackageSource);

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "PackageWithNonStrongNamedLibA", Logger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "PackageWithNonStrongNamedLibB", Logger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "PackageWithStrongNamedLib", "1.1", Logger);
            AssertAssemblyReference(testContext.Project, "A", "1.0.0.0");
            AssertAssemblyReference(testContext.Project, "B", "1.0.0.0");
            AssertAssemblyReference(testContext.Project, "Core", "1.1.0.0");
            AssertBindingRedirect(testContext.Project, "app.config", "Core", "0.0.0.0-1.1.0.0", "1.1.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCWithFrameworkAssemblyReference_DoesNotAddBindingRedirectAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ConsoleApplication);
            var packages = CreateFrameworkAssemblyBindingRedirectPackages();
            await CreatePackagesAsync(testContext.PackageSource, packages.ToArray());
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "System.Net.Http", "4.2.0", testContext.PackageSource);
            Install(console, testContext.Project, "System.Runtime.CompilerServices.Unsafe", "5.0.0", testContext.PackageSource);
            Install(console, testContext.Project, "FrameworkAssemblyConsumer", "1.0.0", testContext.PackageSource, "-IgnoreDependencies");

            AssertBindingRedirect(testContext.Project, "app.config", "System.Runtime.CompilerServices.Unsafe", "0.0.0.0-5.0.0.0", "5.0.0.0");
            AssertNoBindingRedirect(testContext.Project, "app.config", "System.Net.Http", "0.0.0.0-4.2.0.0", "4.2.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCWithNonFrameworkAssemblyReference_AddsBindingRedirectAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ConsoleApplication);
            var packages = CreateNonFrameworkAssemblyBindingRedirectPackages();
            await CreatePackagesAsync(testContext.PackageSource, packages.ToArray());
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "NuGet.Protocol", "5.10.0", testContext.PackageSource);
            Update(console, testContext.Project, "Newtonsoft.Json", testContext.PackageSource, "-Version 13.0.1");

            AssertBindingRedirect(testContext.Project, "app.config", "Newtonsoft.Json", "0.0.0.0-13.0.0.0", "13.0.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMCAddsBindingRedirectAfterSecondInstallAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "AddingBindingRedirectAfterUpdate");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "A", "1.0", Logger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "B", "1.0", Logger);
            Install(console, testContext.Project, "C", "1.0", testContext.PackageSource);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "C", "1.0", Logger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "B", "2.0", Logger);
            AssertSolutionPackage(testContext, "B", "1.0", exists: false);

            testContext.SolutionService.Build();
            console.Execute("Add-BindingRedirect");

            AssertBindingRedirect(testContext.Project, "web.config", "B", "0.0.0.0-2.0.0.0", "2.0.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackageFromPMCUpdatesExistingBindingRedirectAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.WebApplicationEmpty);
            await CreateBindingRedirectPackagesFromDgmlAsync(testContext.PackageSource, "UpdatingBindingRedirectAfterUpdate");
            var console = GetConsole(testContext.Project);

            Install(console, testContext.Project, "B", "2.0", testContext.PackageSource);
            Install(console, testContext.Project, "A", "1.0", testContext.PackageSource);
            AssertBindingRedirect(testContext.Project, "web.config", "B", "0.0.0.0-2.0.0.0", "2.0.0.0");

            Update(console, testContext.Project, "B", testContext.PackageSource, "-Version 3.0");

            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, "B", "3.0", Logger);
            AssertBindingRedirect(testContext.Project, "web.config", "B", "0.0.0.0-3.0.0.0", "3.0.0.0");
            AssertNoErrors(console);
        }

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageApiAddsBindingRedirectAsync()
        {
            using var testContext = CreatePackagesConfigContext(ProjectTemplate.ClassLibrary);
            await CreatePackagesAsync(testContext.PackageSource, CreateApiBindingRedirectPackages().ToArray());

            var projectUniqueName = VisualStudio.Dte.Solution.Projects.Item(1).UniqueName;
            testContext.NuGetApexTestService.InstallPackage(
                testContext.PackageSource,
                projectUniqueName,
                "TestBindingRedirectA",
                "1.0.0");
            var console = GetConsole(testContext.Project);

            AssertBindingRedirect(testContext.Project, "app.config", "B", "0.0.0.0-2.0.0.0", "2.0.0.0");
            AssertNoErrors(console);
        }

        private static async Task CreateBindingRedirectPackagesFromDgmlAsync(string packageSource, string scenarioName)
        {
            var scenarioPath = Path.Combine(GetEndToEndPackagesPath(), scenarioName);
            var scenarioGraphPath = Path.Combine(scenarioPath, $"{scenarioName}.dgml");
            var graphPath = File.Exists(scenarioGraphPath) ? scenarioGraphPath : Path.Combine(scenarioPath, "BindingRedirectsGraph.dgml");
            var packages = CreateGeneratedPackagesFromDgml(graphPath);
            await CreatePackagesAsync(packageSource, packages.ToArray());
        }

        private static List<SimpleTestPackageContext> CreateGeneratedPackagesFromDgml(string graphPath)
        {
            var document = XDocument.Load(graphPath);
            XNamespace ns = "http://schemas.microsoft.com/vs/2009/dgml";
            var packages = new Dictionary<string, GeneratedPackage>(StringComparer.OrdinalIgnoreCase);

            GeneratedPackage GetPackage(string fullName)
            {
                if (!packages.TryGetValue(fullName, out var package))
                {
                    var parts = fullName.Split(':');
                    package = new GeneratedPackage(parts[0], parts[1]);
                    packages.Add(fullName, package);
                }

                return package;
            }

            foreach (var link in document.Descendants(ns + "Link"))
            {
                var source = GetPackage(link.Attribute("Source")!.Value);
                var target = GetPackage(link.Attribute("Target")!.Value);
                source.Dependencies.Add((target, link.Attribute("Label")?.Value));
            }

            foreach (var node in document.Descendants(ns + "Node"))
            {
                GetPackage(node.Attribute("Id")!.Value);
            }

            return CreateGeneratedPackageContexts(packages.Values);
        }

        private static IReadOnlyList<SimpleTestPackageContext> CreateFrameworkAssemblyBindingRedirectPackages()
        {
            var systemNetHttp40 = new GeneratedPackage("System.Net.Http", "4.0.0", "4.0.0");
            var systemNetHttp42 = new GeneratedPackage("System.Net.Http", "4.2.0", "4.2.0");
            var unsafe40 = new GeneratedPackage("System.Runtime.CompilerServices.Unsafe", "4.0.0", "4.0.0");
            var unsafe50 = new GeneratedPackage("System.Runtime.CompilerServices.Unsafe", "5.0.0", "5.0.0");
            var consumer = new GeneratedPackage("FrameworkAssemblyConsumer", "1.0.0");
            consumer.Dependencies.Add((systemNetHttp40, "[4.0.0,)"));
            consumer.Dependencies.Add((unsafe40, "[4.0.0,)"));

            return CreateGeneratedPackageContexts(systemNetHttp40, systemNetHttp42, unsafe40, unsafe50, consumer);
        }

        private static IReadOnlyList<SimpleTestPackageContext> CreateNonFrameworkAssemblyBindingRedirectPackages()
        {
            var newtonsoftJson12 = new GeneratedPackage("Newtonsoft.Json", "12.0.3", "12.0.0");
            var newtonsoftJson13 = new GeneratedPackage("Newtonsoft.Json", "13.0.1", "13.0.0");
            var nugetProtocol = new GeneratedPackage("NuGet.Protocol", "5.10.0");
            nugetProtocol.Dependencies.Add((newtonsoftJson12, "[12.0.3,)"));

            return CreateGeneratedPackageContexts(newtonsoftJson12, newtonsoftJson13, nugetProtocol);
        }

        private static IReadOnlyList<SimpleTestPackageContext> CreateApiBindingRedirectPackages()
        {
            var b1 = new GeneratedPackage("B", "1.0", "1.0");
            var b2 = new GeneratedPackage("B", "2.0", "2.0");
            var root = new GeneratedPackage("TestBindingRedirectA", "1.0.0");
            root.Dependencies.Add((b1, null));

            CompileGeneratedAssemblies(root, b2);

            return new[]
            {
                CreateGeneratedPackageContext(b2),
                CreateGeneratedPackageContext(
                    root,
                    new (string Id, string? Range)[] { ("B", "[2.0]") }),
            };
        }

        private static List<SimpleTestPackageContext> CreateGeneratedPackageContexts(
            params GeneratedPackage[] packages)
        {
            return CreateGeneratedPackageContexts((IEnumerable<GeneratedPackage>)packages);
        }

        private static List<SimpleTestPackageContext> CreateGeneratedPackageContexts(
            IEnumerable<GeneratedPackage> packages)
        {
            var packageList = packages.ToList();
            CompileGeneratedAssemblies(packageList.ToArray());
            return packageList.Select(package => CreateGeneratedPackageContext(package)).ToList();
        }

        private static SimpleTestPackageContext CreateGeneratedPackageContext(
            GeneratedPackage package,
            IEnumerable<(string Id, string? Range)>? dependencies = null)
        {
            var context = new SimpleTestPackageContext(package.Id, package.Version)
            {
                UseDefaultRuntimeAssemblies = false,
            };
            context.AddFile($"lib/net45/{package.Id}.dll", File.ReadAllBytes(package.AssemblyPath));
            foreach (var dependency in dependencies ?? package.Dependencies.Select(
                dependency => (dependency.Package.Id, (string?)(dependency.Range ?? dependency.Package.Version))))
            {
                context.Dependencies.Add(new SimpleTestPackageContext
                {
                    Id = dependency.Id,
                    Version = dependency.Range,
                });
            }

            return context;
        }

        private static void CompileGeneratedAssemblies(params GeneratedPackage[] packages)
        {
            var assemblyRoot = Path.Combine(Path.GetTempPath(), "NuGetApexGeneratedAssemblies", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(assemblyRoot);
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in packages)
            {
                CompileGeneratedAssembly(package, assemblyRoot, processed);
            }
        }

        private static void CompileGeneratedAssembly(GeneratedPackage package, string assemblyRoot, HashSet<string> processed)
        {
            var key = $"{package.Id}:{package.Version}";
            if (!processed.Add(key))
            {
                return;
            }

            foreach (var dependency in package.Dependencies)
            {
                CompileGeneratedAssembly(dependency.Package, assemblyRoot, processed);
            }

            var outputDirectory = Path.Combine(assemblyRoot, package.Id, package.Version);
            Directory.CreateDirectory(outputDirectory);
            package.AssemblyPath = Path.Combine(outputDirectory, $"{package.Id}.dll");
            var keyFile = Path.Combine(GetRepositoryRoot(), "test", "TestExtensions", "GenerateTestPackages", "TestPackageKey.snk");
            using var codeProvider = new CSharpCodeProvider();
            var compilerParameters = new CompilerParameters
            {
                OutputAssembly = package.AssemblyPath,
                CompilerOptions = $"/target:library /keyfile:\"{keyFile}\"",
                GenerateExecutable = false,
            };
            compilerParameters.ReferencedAssemblies.Add("System.dll");
            foreach (var dependency in package.Dependencies)
            {
                compilerParameters.ReferencedAssemblies.Add(dependency.Package.AssemblyPath);
            }

            var dependencyExpressions = package.Dependencies
                .Select(dependency => $"{GetTypeName(dependency.Package.Id)}.Value")
                .ToArray();
            var source = $@"
using System.Reflection;
[assembly: AssemblyVersion(""{ToAssemblyVersion(package.AssemblyVersion)}"")]
public static class {GetTypeName(package.Id)}
{{
    public static string Value {{ get {{ return ""{package.Id}:{package.Version}""{(dependencyExpressions.Length == 0 ? string.Empty : " + " + string.Join(" + ", dependencyExpressions))}; }} }}
}}";
            var results = codeProvider.CompileAssemblyFromSource(compilerParameters, source);
            if (results.Errors.HasErrors)
            {
                throw new InvalidOperationException(results.Errors[0].ToString());
            }
        }

        private static string ToAssemblyVersion(string version)
        {
            var parsed = NuGetVersion.Parse(version).Version;
            return $"{parsed.Major}.{parsed.Minor}.{parsed.Build}.{parsed.Revision}";
        }

        private static string GetTypeName(string id)
        {
            var chars = id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            var name = new string(chars);
            return char.IsDigit(name[0]) ? "_" + name : name;
        }

        private void AssertAssemblyReference(ProjectTestExtension project, string referenceName, string expectedVersion)
        {
            var dteProject = GetDteProject(project);
            dynamic references = ((dynamic)dteProject.Object).References;
            object? matchingReference = null;
            foreach (dynamic reference in references)
            {
                if (string.Equals((string)reference.Name, referenceName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingReference = reference;
                    break;
                }
            }

            matchingReference.Should().NotBeNull($"reference {referenceName} should exist in {project.Name}");
            dynamic assemblyReference = matchingReference!;
            string? referencePath;
            try
            {
                referencePath = assemblyReference.Path as string;
            }
            catch (RuntimeBinderException)
            {
                referencePath = assemblyReference.FullPath as string;
            }
            referencePath.Should().NotBeNullOrEmpty($"reference {referenceName} should not be broken");
            File.Exists(referencePath).Should().BeTrue($"reference {referenceName} should point to an existing assembly");

            string? version;
            try
            {
                version = assemblyReference.Version?.ToString();
            }
            catch (RuntimeBinderException)
            {
                version = null;
            }
            var actualVersion = string.IsNullOrEmpty(version)
                ? System.Reflection.AssemblyName.GetAssemblyName(referencePath!).Version
                : Version.Parse(version);
            actualVersion.Should().Be(Version.Parse(expectedVersion));
        }

        private static void AssertProjectFileDoesNotExist(ProjectTestExtension project, string fileName)
        {
            Directory.GetFiles(GetProjectDirectory(project), fileName, SearchOption.TopDirectoryOnly)
                .Should().BeEmpty($"{fileName} should not be added to {project.Name}");
        }

        private static void AssertBindingRedirect(ProjectTestExtension project, string configFileName, string assemblyName, string oldVersion, string newVersion)
        {
            var configPath = GetProjectFilePath(project, configFileName);
            var document = XDocument.Load(configPath);
            HasBindingRedirect(document, assemblyName, oldVersion, newVersion)
                .Should().BeTrue($"expected {assemblyName} binding redirect in {configPath}. Actual: {document}");
        }

        private static void AssertNoBindingRedirect(ProjectTestExtension project, string configFileName, string assemblyName, string oldVersion, string newVersion)
        {
            var configPath = GetProjectFilePath(project, configFileName);
            var document = XDocument.Load(configPath);
            HasBindingRedirect(document, assemblyName, oldVersion, newVersion)
                .Should().BeFalse($"did not expect {assemblyName} binding redirect in {configPath}. Actual: {document}");
        }

        private static bool HasBindingRedirect(XDocument document, string assemblyName, string oldVersion, string newVersion)
        {
            XNamespace ns = "urn:schemas-microsoft-com:asm.v1";
            return document.Descendants(ns + "dependentAssembly")
                .Any(assembly =>
                {
                    var identity = assembly.Element(ns + "assemblyIdentity");
                    var redirect = assembly.Element(ns + "bindingRedirect");
                    return (string?)identity?.Attribute("name") == assemblyName &&
                        (string?)redirect?.Attribute("oldVersion") == oldVersion &&
                        (string?)redirect?.Attribute("newVersion") == newVersion;
                });
        }

        private void AddWebSiteProjectReference(ProjectTestExtension webSite, ProjectTestExtension referencedProject)
        {
            // WebSite projects expose a VSWebSite automation object, not VSProject, so the Apex
            // References.Dte.AddProjectReference helper (which requires VSProject) does not work here.
            ((dynamic)GetDteProject(webSite).Object).References.AddFromProject(GetDteProject(referencedProject));
        }

        private EnvDTE.Project GetDteProject(ProjectTestExtension project)
        {
            return VisualStudio.Dte.Solution.Projects
                .Cast<EnvDTE.Project>()
                .Single(candidate => candidate.UniqueName == project.UniqueName);
        }

        private static string GetProjectDirectory(ProjectTestExtension project)
        {
            return Directory.Exists(project.FullPath)
                ? project.FullPath
                : Path.GetDirectoryName(project.FullPath)!;
        }

        private static string GetProjectFilePath(ProjectTestExtension project, string fileName)
        {
            var projectDirectory = GetProjectDirectory(project);
            var file = Directory.GetFiles(projectDirectory, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (file == null)
            {
                throw new FileNotFoundException($"Unable to find {fileName} in {projectDirectory}.");
            }

            return file;
        }

        private static void CopyEndToEndPackagesToSource(string packageSource, params string[] packageNames)
        {
            var root = GetEndToEndPackagesPath();
            foreach (var packageName in packageNames)
            {
                File.Copy(Path.Combine(root, packageName), Path.Combine(packageSource, packageName), overwrite: true);
            }
        }

        private static string GetRepositoryRoot()
        {
            foreach (var startPath in new[] { Directory.GetCurrentDirectory(), typeof(NuGetConsoleTestCase).Assembly.Location })
            {
                var directory = new DirectoryInfo(File.Exists(startPath) ? Path.GetDirectoryName(startPath)! : startPath);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "NuGet.sln")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException("Unable to locate repository root.");
        }

        private sealed class GeneratedPackage
        {
            public GeneratedPackage(string id, string version)
                : this(id, version, version)
            {
            }

            public GeneratedPackage(string id, string version, string assemblyVersion)
            {
                Id = id;
                Version = version;
                AssemblyVersion = assemblyVersion;
            }

            public string Id { get; }
            public string Version { get; }
            public string AssemblyVersion { get; }
            public List<(GeneratedPackage Package, string? Range)> Dependencies { get; } = new();
            public string AssemblyPath { get; set; } = string.Empty;
        }
    }
}
