using System;
using Microsoft.Framework.Logging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLoggerProvider : ILoggerProvider
    {
        private readonly INuGetProjectContext _projectContext;

        public ProjectContextLoggerProvider(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public ILogger CreateLogger(string name)
        {
            return new ProjectContextLogger(_projectContext);
        }
    }
}
