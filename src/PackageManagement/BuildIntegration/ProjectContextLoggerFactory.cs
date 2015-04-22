using System;
using Microsoft.Framework.Logging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLoggerFactory : ILoggerFactory
    {
        private readonly INuGetProjectContext _projectContext;
        private LogLevel _level;

        public ProjectContextLoggerFactory(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public LogLevel MinimumLevel
        {
            get
            {
                return _level;
            }

            set
            {
                _level = value;
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ProjectContextLogger(_projectContext);
        }

        public void AddProvider(ILoggerProvider provider)
        {

        }
    }
}
