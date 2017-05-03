using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Common
{
    public interface IRestoreLogMessage : ILogMessage, ILogFileContext
    {
        /// <summary>
        /// Project or Package Id
        /// </summary>
        string LibraryId { get; set; }
    }
}
