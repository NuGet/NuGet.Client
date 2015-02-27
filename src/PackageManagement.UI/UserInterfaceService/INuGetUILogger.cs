using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUILogger
    {
        void Log(MessageLevel level, string message, params object[] args);

        void ReportError(string message);

        void Start();

        void End();
    }
}