using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.Common
{
    internal class MSBuildSolutionParser : ISolutionParser
    {
        public IEnumerable<string> GetAllProjectFileNames(IFileSystem fileSystem, string solutionFile)
        {
            var solution = new Solution(fileSystem, solutionFile);
            var solutionDirectory = Path.GetDirectoryName(fileSystem.GetFullPath(solutionFile));

            return solution.Projects.Where(p => !p.IsSolutionFolder)
                .Select(p => Path.Combine(solutionDirectory, p.RelativePath));
        }
    }
}
