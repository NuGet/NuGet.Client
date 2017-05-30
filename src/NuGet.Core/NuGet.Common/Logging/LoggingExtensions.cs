
using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Common
{
    public static class LoggingExtensions
    {
        public static string FormatWithCode(this ILogMessage message)
        {
            return $"{message.Code}: {message.Message}";
        }
    }
}
