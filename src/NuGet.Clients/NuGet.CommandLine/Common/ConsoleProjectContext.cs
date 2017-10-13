using System.Globalization;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.CommandLine
{
    public class ConsoleProjectContext : INuGetProjectContext
    {
        private readonly Common.ILogger _logger;

        public ConsoleProjectContext(Common.ILogger logger)
        {
            _logger = logger;
        }

        public ExecutionContext ExecutionContext => null;

        public PackageExtractionV2Context PackageExtractionContext { get; set; }

        public XDocument OriginalPackagesConfig { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider => null;

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            if (args.Length > 0)
            {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }
            
            switch (level)
            {
                case ProjectManagement.MessageLevel.Debug:
                    _logger.LogDebug(message);
                    break;

                case ProjectManagement.MessageLevel.Info:
                    _logger.LogMinimal(message);
                    break;

                case ProjectManagement.MessageLevel.Warning:
                    _logger.LogWarning(message);
                    break;

                case ProjectManagement.MessageLevel.Error:
                    _logger.LogError(message);
                    break;
            }
        }

        public void ReportError(string message)
        {
            _logger.LogError(message);
        }

        public virtual ProjectManagement.FileConflictAction ResolveFileConflict(string message)
        {
            return ProjectManagement.FileConflictAction.IgnoreAll;
        }

        public NuGetActionType ActionType { get; set; }

        public TelemetryServiceHelper TelemetryService { get; set; }
    }
}
