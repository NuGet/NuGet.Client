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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                AppDomain.Unload(domain);
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

            string envDTEProjectUniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var redirects = Enumerable.Empty<AssemblyBinding>();
            var msBuildNuGetProjectSystem = GetMSBuildNuGetProjectSystem(solutionManager, envDTEProject);

            // If no msBuildNuGetProjectSystem, no binding redirects. Bail
            if (msBuildNuGetProjectSystem == null)
            {
                return redirects;
            }

            // Get the full path from envDTEProject
            var root = EnvDTEProjectUtility.GetFullPath(envDTEProject);

            IEnumerable<string> assemblies = EnvDTEProjectUtility.GetAssemblyClosure(envDTEProject, projectAssembliesCache);
            redirects = BindingRedirectResolver.GetBindingRedirects(assemblies, domain);

            if (frameworkMultiTargeting != null)
            {
                // filter out assemblies that already exist in the target framework (CodePlex issue #3072)
                FrameworkName targetFrameworkName = EnvDTEProjectUtility.GetDotNetFrameworkName(envDTEProject);
                redirects = redirects.Where(p => !FrameworkAssemblyResolver.IsHigherAssemblyVersionInFramework(p.Name, p.AssemblyNewVersion, targetFrameworkName));
            }

            // Create a binding redirect manager over the configuration
            var manager = new BindingRedirectManager(EnvDTEProjectUtility.GetConfigurationFile(envDTEProject), msBuildNuGetProjectSystem);

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

        /// <summary>
        /// Load the specified assembly using the information from the executing assembly.
        /// If the executing assembly is strongly signed, use Assembly.Load(); Otherwise,
        /// use Assembly.LoadFrom()
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to be loaded.</param>
        /// <returns>The loaded Assembly instance.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2001")]
        internal static Assembly LoadAssemblySmart(string assemblyName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            AssemblyName executingAssemblyName = executingAssembly.GetName();
            if (HasStrongName(executingAssemblyName))
            {
                // construct the Full Name of the assembly using the same version/culture/public key token
                // of the executing assembly.
                string assemblyFullName = String.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, Version={1}, Culture=neutral, PublicKeyToken={2}",
                    assemblyName,
                    executingAssemblyName.Version,
                    ConvertToHexString(executingAssemblyName.GetPublicKeyToken()));

                return Assembly.Load(assemblyFullName);
            }
            var assemblyDirectory = Path.GetDirectoryName(executingAssembly.Location);
            return Assembly.LoadFrom(Path.Combine(assemblyDirectory, assemblyName + ".dll"));
        }

        private static bool HasStrongName(AssemblyName assembly)
        {
            byte[] publicKeyToken = assembly.GetPublicKeyToken();
            return publicKeyToken != null && publicKeyToken.Length > 0;
        }

        private static string ConvertToHexString(byte[] data)
        {
            return new SoapHexBinary(data).ToString();
        }
    }
}
