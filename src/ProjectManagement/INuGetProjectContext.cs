using NuGet.Packaging;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Comprises of the various client context such as logging, fileconflictaction
    /// </summary>
    public interface INuGetProjectContext
    {
        /// <summary>
        /// Logs a message for the given project context
        /// </summary>
        void Log(MessageLevel level, string message, params object[] args);

        void ReportError(string message);

        /// <summary>
        /// Resolves a file conflict for the given project context
        /// </summary>
        FileConflictAction ResolveFileConflict(string message);
        PackageExtractionContext PackageExtractionContext { get; set; }
        ISourceControlManagerProvider SourceControlManagerProvider { get; }
        ExecutionContext ExecutionContext { get; }
    }

    /// <summary>
    /// MessageLevel
    /// </summary>
    public enum MessageLevel
    {
        /// <summary>
        /// Information
        /// </summary>
        Info,
        /// <summary>
        /// Warning
        /// </summary>
        Warning,
        /// <summary>
        /// Debug only
        /// </summary>
        Debug,
        /// <summary>
        /// Error
        /// </summary>
        Error
    }
}
