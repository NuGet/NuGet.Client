using System.Collections.Generic;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    public interface IDependencyGraphToolSpecProvider
    {
        /// <summary>
        /// Returns specs for DotnetCliToolReferences
        /// </summary>
        /// <returns></returns>
        IReadOnlyList<PackageSpec> GetDotnetCliToolSpecs();
    }
}
