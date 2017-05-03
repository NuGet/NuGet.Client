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
        /// Indicates the line for which the error was thrown.
        /// </summary>
        int LineNumber { get; set; }

        /// <summary>
        /// Indicates the column for which the error was thrown.
        /// </summary>
        int ColumnNumber { get; set; }
    }
}
