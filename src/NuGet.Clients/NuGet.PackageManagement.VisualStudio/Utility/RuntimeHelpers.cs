// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using EnvDTEProject = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class RuntimeHelpers
    {
        public static async Task AddBindingRedirectsAsync(
            VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            IVsFrameworkMultiTargeting frameworkMultiTargeting,
            INuGetProjectContext nuGetProjectContext)
        {
            // Create a new app domain so we can load the assemblies without locking them in this app domain
            AppDomain domain = AppDomain.CreateDomain("assembliesDomain");

            try
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Keep track of visited projects
                if (EnvDTEProjectUtility.SupportsBindingRedirects(envDTEProject))
                {
                    // Get the dependentEnvDTEProjectsDictionary once here, so that, it is not called for every single project
                    var dependentEnvDTEProjectsDictionary = await vsSolutionManager.GetDependentEnvDTEProjectsDictionaryAsync();
                    await AddBindingRedirectsAsync(vsSolutionManager, envDTEProject, domain,
                        frameworkMultiTargeting, dependentEnvDTEProjectsDictionary, nuGetProjectContext);
                }
            }
            finally
            {
                QueueUnloadAndForget(domain);
            }
        }

        private static Task AddBindingRedirectsAsync(
            VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            AppDomain domain,
            IVsFrameworkMultiTargeting frameworkMultiTargeting,
            IDictionary<string, List<EnvDTEProject>> dependentEnvDTEProjectsDictionary,
            INuGetProjectContext nuGetProjectContext)
        {
            // Need to be on the UI thread

            var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projectAssembliesCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            return AddBindingRedirectsAsync(vsSolutionManager, envDTEProject, domain, visitedProjects, projectAssembliesCache,
                frameworkMultiTargeting, dependentEnvDTEProjectsDictionary, nuGetProjectContext);
        }

        private static async Task AddBindingRedirectsAsync(VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            AppDomain domain,
            HashSet<string> visitedProjects,
            Dictionary<string, HashSet<string>> projectAssembliesCache,
            IVsFrameworkMultiTargeting frameworkMultiTargeting,
            IDictionary<string, List<EnvDTEProject>> dependentEnvDTEProjectsDictionary,
            INuGetProjectContext nuGetProjectContext)
        {
            // Need to be on the UI thread

            string envDTEProjectUniqueName = EnvDTEProjectInfoUtility.GetUniqueName(envDTEProject);
            if (visitedProjects.Contains(envDTEProjectUniqueName))
            {
                return;
            }

            if (EnvDTEProjectUtility.SupportsBindingRedirects(envDTEProject))
            {
                await AddBindingRedirectsAsync(vsSolutionManager, envDTEProject, domain, projectAssembliesCache, frameworkMultiTargeting, nuGetProjectContext);
            }

            // Add binding redirects to all envdteprojects that are referencing this one
            foreach (EnvDTEProject dependentEnvDTEProject in VSSolutionManager.GetDependentEnvDTEProjects(dependentEnvDTEProjectsDictionary, envDTEProject))
            {
                await AddBindingRedirectsAsync(
                    vsSolutionManager,
                    dependentEnvDTEProject,
                    domain,
                    visitedProjects,
                    projectAssembliesCache,
                    frameworkMultiTargeting,
                    dependentEnvDTEProjectsDictionary,
                    nuGetProjectContext);
            }

            visitedProjects.Add(envDTEProjectUniqueName);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "nuGetProjectContext")]
        public static async Task<IEnumerable<AssemblyBinding>> AddBindingRedirectsAsync(
            ISolutionManager solutionManager,
            EnvDTEProject envDTEProject,
            AppDomain domain,
            IDictionary<string, HashSet<string>> projectAssembliesCache,
            IVsFrameworkMultiTargeting frameworkMultiTargeting,
            INuGetProjectContext nuGetProjectContext)
        {
            // Run this on the UI thread since it enumerates all references
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var redirects = Enumerable.Empty<AssemblyBinding>();
            var msBuildNuGetProjectSystem = GetMSBuildNuGetProjectSystem(solutionManager, envDTEProject);

            // If no msBuildNuGetProjectSystem, no binding redirects. Bail
            if (msBuildNuGetProjectSystem == null)
            {
                return redirects;
            }

            // Get the full path from envDTEProject
            var root = EnvDTEProjectInfoUtility.GetFullPath(envDTEProject);

            IEnumerable<string> assemblies = EnvDTEProjectUtility.GetAssemblyClosure(envDTEProject, projectAssembliesCache);
            redirects = BindingRedirectResolver.GetBindingRedirects(assemblies, domain);

            if (frameworkMultiTargeting != null)
            {
                // filter out assemblies that already exist in the target framework (CodePlex issue #3072)
                FrameworkName targetFrameworkName = EnvDTEProjectInfoUtility.GetDotNetFrameworkName(envDTEProject);
                redirects = redirects.Where(p => !FrameworkAssemblyResolver.IsHigherAssemblyVersionInFramework(p.Name, p.AssemblyNewVersion, targetFrameworkName));
            }

            // Create a binding redirect manager over the configuration
            var manager = new BindingRedirectManager(EnvDTEProjectInfoUtility.GetConfigurationFile(envDTEProject), msBuildNuGetProjectSystem);

            // Add the redirects
            manager.AddBindingRedirects(redirects);

            return redirects;
        }

        private static IMSBuildNuGetProjectSystem GetMSBuildNuGetProjectSystem(ISolutionManager solutionManager, EnvDTEProject envDTEProject)
        {
            var nuGetProject = solutionManager.GetNuGetProject(envDTEProject.Name);
            if (nuGetProject != null)
            {
                var msBuildNuGetProject = nuGetProject as MSBuildNuGetProject;
                if (msBuildNuGetProject != null)
                {
                    return msBuildNuGetProject.MSBuildNuGetProjectSystem;
                }
            }
            return null;
        }

        private static void QueueUnloadAndForget(AppDomain domain)
        {
            Task.Run(() =>
            {
                try
                {
                    AppDomain.Unload(domain);
                }
                catch (Exception)
                {
                }
            });
        }
    }
}