using System.Collections.Generic;

namespace NuGet
{
    internal interface ISolutionParser
    {
        IEnumerable<string> GetAllProjectFileNames(IFileSystem fileSystem, string solutionFile);
    }
}
