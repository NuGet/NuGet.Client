using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public static class SolutionExtensions
    {
        /// <summary>
        /// Get the list of all supported projects in the current solution. This method
        /// recursively iterates through all projects.
        /// </summary>
        public static IEnumerable<Project> GetAllProjects(this Solution solution)
        {
            if (solution == null || !solution.IsOpen)
            {
                yield break;
            }

            var projects = new Stack<Project>();
            foreach (Project project in solution.Projects)
            {
                if (!project.IsExplicitlyUnsupported())
                {
                    projects.Push(project);
                }
            }

            while (projects.Any())
            {
                Project project = projects.Pop();

                if (project.IsSupported())
                {
                    yield return project;
                }
                else if (project.IsExplicitlyUnsupported())
                {
                    // do not drill down further if this project is explicitly unsupported, e.g. LightSwitch projects
                    continue;
                }

                ProjectItems projectItems = null;
                try
                {
                    // bug 1138: Oracle Database Project doesn't implement the ProjectItems property
                    projectItems = project.ProjectItems;
                }
                catch (NotImplementedException)
                {
                    continue;
                }

                // ProjectItems property can be null if the project is unloaded
                if (projectItems != null)
                {
                    foreach (ProjectItem projectItem in projectItems)
                    {
                        try
                        {
                            if (projectItem.SubProject != null)
                            {
                                projects.Push(projectItem.SubProject);
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

        public static string GetName(this Solution solution)
        {
            return (string)solution.Properties.Item("Name").Value;
        }
    }
}
