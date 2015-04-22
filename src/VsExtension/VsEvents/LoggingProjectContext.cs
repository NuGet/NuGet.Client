using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging;
using System.Globalization;

namespace NuGetVSExtension
{
    /// <summary>
    /// INuGetProjectContext with logging support
    /// </summary>
    public class LoggingProjectContext : INuGetProjectContext
    {
        private readonly Action<string> _logMessage;

        public LoggingProjectContext(Action<string> logMessage)
        {
            _logMessage = logMessage;
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            _logMessage(String.Format(CultureInfo.CurrentCulture, message, args));
        }

        public ExecutionContext ExecutionContext
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public PackageExtractionContext PackageExtractionContext
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void ReportError(string message)
        {
            throw new NotImplementedException();
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            throw new NotImplementedException();
        }
    }
}
