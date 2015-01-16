using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class RuntimeHelpers
    {
        public static void AddBindingRedirects(
            VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            IVsFrameworkMultiTargeting frameworkMultiTargeting)
        {
            // Create a new app domain so we can load the assemblies without locking them in this app domain
            AppDomain domain = AppDomain.CreateDomain("assembliesDomain");

            try
            {
                // Keep track of visited projects
                if (EnvDTEProjectUtility.SupportsBindingRedirects(envDTEProject))
                {
                    AddBindingRedirects(vsSolutionManager, envDTEProject, domain, frameworkMultiTargeting);
                }
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }

        private static void AddBindingRedirects(
            VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            AppDomain domain,
            IVsFrameworkMultiTargeting frameworkMultiTargeting)
        {
            var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projectAssembliesCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            AddBindingRedirects(vsSolutionManager, envDTEProject, domain, visitedProjects, projectAssembliesCache, frameworkMultiTargeting);
        }

        private static void AddBindingRedirects(VSSolutionManager vsSolutionManager,
            EnvDTEProject envDTEProject,
            AppDomain domain,
            HashSet<string> visitedProjects,
            Dictionary<string, HashSet<string>> projectAssembliesCache,
            IVsFrameworkMultiTargeting frameworkMultiTargeting)
        {
            string envDTEProjectUniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);
            if (visitedProjects.Contains(envDTEProjectUniqueName))
            {
                return;
            }

            if (EnvDTEProjectUtility.SupportsBindingRedirects(envDTEProject))
            {
                AddBindingRedirects(envDTEProject, domain, projectAssembliesCache, frameworkMultiTargeting);
            }

            // Add binding redirects to all envdteprojects that are referencing this one
            foreach (EnvDTEProject dependentEnvDTEProject in vsSolutionManager.GetDependentEnvDTEProjects(envDTEProject))
            {
                AddBindingRedirects(
                    vsSolutionManager,
                    dependentEnvDTEProject,
                    domain,
                    visitedProjects,
                    projectAssembliesCache,
                    frameworkMultiTargeting);
            }

            visitedProjects.Add(envDTEProjectUniqueName);
        }

        private static IEnumerable<AssemblyBinding> AddBindingRedirects(
            EnvDTEProject envDTEProject,
            AppDomain domain,
            IDictionary<string, HashSet<string>> projectAssembliesCache,
            IVsFrameworkMultiTargeting frameworkMultiTargeting)
        {
            var redirects = Enumerable.Empty<AssemblyBinding>();

            // Get the full path from envDTEProject
            var root = EnvDTEProjectUtility.GetFullPath(envDTEProject);

            // Run this on the UI thread since it enumerates all references
            IEnumerable<string> assemblies = ThreadHelper.Generic.Invoke(() => EnvDTEProjectUtility.GetAssemblyClosure(envDTEProject, projectAssembliesCache));

            redirects = BindingRedirectResolver.GetBindingRedirects(assemblies, domain);

            if (frameworkMultiTargeting != null)
            {
                // filter out assemblies that already exist in the target framework (CodePlex issue #3072)
                FrameworkName targetFrameworkName = EnvDTEProjectUtility.GetDotNetFrameworkName(envDTEProject);
                redirects = redirects.Where(p => !FrameworkAssemblyResolver.IsHigherAssemblyVersionInFramework(p.Name, p.AssemblyNewVersion, targetFrameworkName));
            }

            // Create a binding redirect manager over the configuration
            var manager = new BindingRedirectManager(root, EnvDTEProjectUtility.GetConfigurationFile(envDTEProject));

            // Add the redirects
            manager.AddBindingRedirects(redirects);

            return redirects;
        }
    }
}
