using NuGet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// MEF export interface for SourceRepositoryProvider
    /// </summary>
    public interface ISourceRepositoryProvider
    {
        /// <summary>
        /// Retrieve repositories
        /// </summary>
        /// <returns></returns>
        IEnumerable<SourceRepository> GetRepositories();
    }
}
