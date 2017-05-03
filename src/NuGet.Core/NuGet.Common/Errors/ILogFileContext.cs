using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Common
{
    public interface ILogFileContext
    {
        /// <summary>
        /// Indicates the file for which the error was thrown.
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// Indicates the starting line for which the error was thrown.
        /// </summary>
        int StartLineNumber { get; set; }

        /// <summary>
        /// Indicates the starting column for which the error was thrown.
        /// </summary>
        int StartColumnNumber { get; set; }

        /// <summary>
        /// Indicates the ending line for which the error was thrown.
        /// </summary>
        int EndLineNumber { get; set; }

        /// <summary>
        /// Indicates the ending column for which the error was thrown.
        /// </summary>
        int EndColumnNumber { get; set; }
    }
}
