using System.Collections.Generic;

namespace NuGet.ProjectManagement
{
    public abstract class SourceControlManager
    {
        public abstract void ProcessInstall(string root, IEnumerable<string> files);
    }
}
