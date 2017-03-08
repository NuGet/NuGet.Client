// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using NuGet.Common;

namespace NuGet.SolutionRestoreManager
{
    internal class ErrorListTableEntry : ITableEntry
    {
        internal const string ErrorSouce = "NuGet";

        public object Identity
        {
            get
            {
                return Message;
            }
        }

        public string Message { get; set; }

        public LogLevel LogLevel { get; set; }

        public ErrorListTableEntry(string message, LogLevel level)
        {
            Message = message;
            LogLevel = level;
        }

        public bool CanSetValue(string keyName)
        {
            return false;
        }

        public bool TryGetValue(string keyName, out object content)
        {
            content = null;

            switch (keyName)
            {
                case StandardTableColumnDefinitions.Text:
                    content = Message;
                    return true;
                case StandardTableColumnDefinitions.ErrorSeverity:
                    content = GetErrorCategory(LogLevel);
                    return true;
                case StandardTableColumnDefinitions.Priority:
                    content = "high";
                    return true;
                case StandardTableColumnDefinitions.ErrorSource:
                    content = ErrorSouce;
                    return true;
            }

            return false;
        }

        public bool TrySetValue(string keyName, object content)
        {
            content = null;
            return false;
        }

        private static __VSERRORCATEGORY GetErrorCategory(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return __VSERRORCATEGORY.EC_ERROR;
                case LogLevel.Warning:
                    return __VSERRORCATEGORY.EC_WARNING;
                default:
                    return __VSERRORCATEGORY.EC_MESSAGE;
            }
        }
    }
}
