using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLogger : ILogger
    {
        private readonly INuGetProjectContext _projectContext;

        public ProjectContextLogger(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return new Scope();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            string message = state as string;

            if (!String.IsNullOrEmpty(message))
            {
                _projectContext.Log(MessageLevel.Info, message);
            }
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {

            }
        }
    }
}
