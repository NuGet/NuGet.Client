using System;
using System.Reflection;

namespace NuGet.Common
{
    /// <summary>
    /// Represents a project in a sln file.
    /// </summary>
    internal class ProjectInSolution
    {
        private static readonly Type _projectInSolutionType = GetProjectInSolutionType();
        private static readonly PropertyInfo _relativePathProperty = GetRelativePathProperty();
        private static readonly PropertyInfo _projectTypeProperty = GetProjectTypeProperty();

        /// <summary>
        /// The path of the project relative to the solution.
        /// </summary>
        public string RelativePath { get; private set; }

        /// <summary>
        /// Indicates if the project is a solution folder.
        /// </summary>
        public bool IsSolutionFolder { get; private set; }        

        public ProjectInSolution(object solutionProject)
        {
            string projectType = _projectTypeProperty.GetValue(solutionProject, index: null).ToString();
            IsSolutionFolder = projectType.Equals("SolutionFolder", StringComparison.OrdinalIgnoreCase);
            RelativePath = (string)_relativePathProperty.GetValue(solutionProject, index: null);
        }

        private static Type GetProjectInSolutionType()
        {
            var assembly = typeof(Microsoft.Build.Construction.ProjectElement).Assembly;
            var projectInSolutionType = assembly.GetType("Microsoft.Build.Construction.ProjectInSolution");
            if (projectInSolutionType == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("Error_CannotLoadTypeProjectInSolution"));
            }

            return projectInSolutionType;
        }

        private static PropertyInfo GetRelativePathProperty()
        {
            return _projectInSolutionType.GetProperty("RelativePath", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static PropertyInfo GetProjectTypeProperty()
        {
            return _projectInSolutionType.GetProperty("ProjectType", BindingFlags.NonPublic | BindingFlags.Instance);
        }        
    }
}
