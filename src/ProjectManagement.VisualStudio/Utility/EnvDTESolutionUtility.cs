using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTESolution = EnvDTE.Solution;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItem = EnvDTE.ProjectItem;
using EnvDTEProjectItems = EnvDTE.ProjectItems;

namespace NuGet.ProjectManagement.VisualStudio
{
    public static class EnvDTESolutionUtility
    {
        /// <summary>
        /// Get the list of all supported projects in the current solution. This method
        /// recursively iterates through all projects.
        /// </summary>
        public static IEnumerable<EnvDTEProject> GetAllEnvDTEProjects(EnvDTESolution envDTESolution)
        {
            if (envDTESolution == null || !envDTESolution.IsOpen)
            {
                yield break;
            }

            var envDTEProjects = new Stack<EnvDTEProject>();
            foreach (EnvDTEProject envDTEProject in envDTESolution.Projects)
            {
                if (!EnvDTEProjectUtility.IsExplicitlyUnsupported(envDTEProject))
                {
                    envDTEProjects.Push(envDTEProject);
                }
            }

            while (envDTEProjects.Any())
            {
                EnvDTEProject envDTEProject = envDTEProjects.Pop();

                if (EnvDTEProjectUtility.IsSupported(envDTEProject))
                {
                    yield return envDTEProject;
                }
                else if (EnvDTEProjectUtility.IsExplicitlyUnsupported(envDTEProject))
                {
                    // do not drill down further if this project is explicitly unsupported, e.g. LightSwitch projects
                    continue;
                }

                EnvDTEProjectItems envDTEProjectItems = null;
                try
                {
                    // bug 1138: Oracle Database Project doesn't implement the ProjectItems property
                    envDTEProjectItems = envDTEProject.ProjectItems;
                }
                catch (NotImplementedException)
                {
                    continue;
                }

                // ProjectItems property can be null if the project is unloaded
                if (envDTEProjectItems != null)
                {
                    foreach (EnvDTEProjectItem envDTEProjectItem in envDTEProjectItems)
                    {
                        try
                        {
                            if (envDTEProjectItem.SubProject != null)
                            {
                                envDTEProjects.Push(envDTEProjectItem.SubProject);
                            }
                        }
                        catch (NotImplementedException)
                        {
                            // Some project system don't implement the SubProject property,
                            // just ignore those
                            continue;
                        }
                    }
                }
            }
        }
    }
}
