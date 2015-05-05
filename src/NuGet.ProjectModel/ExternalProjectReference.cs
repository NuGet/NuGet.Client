using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents a reference to a project produced by an external build system, such as msbuild.
    /// </summary>
    public class ExternalProjectReference
    {
        private readonly string _name;
        private readonly string _packageSpecPath;
        private readonly IReadOnlyList<string> _projectReferences;

        /// <summary>
        /// Represents a reference to a project produced by an external build system, such as msbuild.
        /// </summary>
        /// <param name="name">unique project name or full path</param>
        /// <param name="packageSpecPath">project.json path</param>
        /// <param name="projectReferences">unique names of the referenced projects</param>
        public ExternalProjectReference(string name, string packageSpecPath, IEnumerable<string> projectReferences)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (packageSpecPath == null)
            {
                throw new ArgumentNullException(nameof(packageSpecPath));
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException(nameof(projectReferences));
            }

            _name = name;
            _packageSpecPath = packageSpecPath;
            _projectReferences = projectReferences.ToList().AsReadOnly();
        }

        /// <summary>
        /// The name of the external project
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// The path to the nuget.json file representing the NuGet dependencies of the project
        /// </summary>
        public string PackageSpecPath
        {
            get
            {
                return _packageSpecPath;
            }
        }

        /// <summary>
        /// A list of other external projects this project references
        /// </summary>
        public IReadOnlyList<string> ExternalProjectReferences
        {
            get
            {
                return _projectReferences;
            }
        }
    }
}