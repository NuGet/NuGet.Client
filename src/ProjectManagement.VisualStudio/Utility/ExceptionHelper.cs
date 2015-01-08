using System;
using Microsoft.VisualStudio.Shell;

namespace NuGet.ProjectManagement.VisualStudio
{
    public static class ExceptionHelper
    {
        private const string LogEntrySource = "NuGet Package Manager";

        public static void WriteToActivityLog(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            throw new InvalidOperationException("Not using Shell yet", exception);
            //exception = ExceptionUtility.Unwrap(exception);

            //ActivityLog.LogError(LogEntrySource, exception.Message + exception.StackTrace);
        }
    }
}