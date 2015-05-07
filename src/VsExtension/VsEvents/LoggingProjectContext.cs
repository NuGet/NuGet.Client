// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Packaging;
using NuGet.ProjectManagement;

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
            get { throw new NotImplementedException(); }
        }

        public PackageExtractionContext PackageExtractionContext
        {
            get { throw new NotImplementedException(); }

            set { throw new NotImplementedException(); }
        }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { throw new NotImplementedException(); }
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
