// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System.Diagnostics;
using System.Xml.Linq;

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
                // no op
                Debug.Assert(false, "Not Implemented");
                return null;
            }
        }

        public PackageExtractionContext PackageExtractionContext
        {
            get
            { 
                // no op
                Debug.Assert(false, "Not Implemented");
                return null;
            }

            set { Debug.Assert(false, "Not Implemented"); }
        }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get
            {
                // no op
                Debug.Assert(false, "Not Implemented");
                return null;
            }
        }

        public void ReportError(string message)
        {
            // no op
            Debug.Assert(false, "Not Implemented");
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            // no op
            Debug.Assert(false, "Not Implemented");
            return 0;
        }

        public XDocument OriginalPackagesConfig { get; set; }
    }
}
