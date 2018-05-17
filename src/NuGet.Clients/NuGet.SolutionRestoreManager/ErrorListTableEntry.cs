// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.IO;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using NuGet.Common;

namespace NuGet.SolutionRestoreManager
{
    public class ErrorListTableEntry : ITableEntry
    {
        internal const string ErrorSouce = "NuGet";

        public ILogMessage Message { get; }

        public object Identity => Message.Message;

        public ErrorListTableEntry(ILogMessage message)
        {
            Message = message;
        }

        public ErrorListTableEntry(string message, LogLevel level)
            : this(new LogMessage(level, message))
        {
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
                    content = Message.Message;
                    return true;
                case StandardTableColumnDefinitions.ErrorSeverity:
                    content = GetErrorCategory(Message.Level);
                    return true;
                case StandardTableColumnDefinitions.Priority:
                    content = "high";
                    return true;
                case StandardTableColumnDefinitions.ErrorSource:
                    content = ErrorSouce;
                    return true;
                case StandardTableColumnDefinitions.ErrorCode:
                    var result = false;

                    if (Message.Code > NuGetLogCode.Undefined)
                    {
                        result = Message.Code.TryGetName(out var codeString);
                        content = codeString;
                    }

                    return result;
                case StandardTableColumnDefinitions.Line:

                    if (Message is RestoreLogMessage)
                    {
                        content = (Message as RestoreLogMessage).StartLineNumber;
                        return true;
                    }

                    return false;
                case StandardTableColumnDefinitions.Column:

                    if (Message is RestoreLogMessage)
                    {
                        content = (Message as RestoreLogMessage).StartColumnNumber;
                        return true;
                    }

                    return false;
                case StandardTableColumnDefinitions.DocumentName:

                    var documentName = GetProjectFile(Message);

                    if (!string.IsNullOrEmpty(documentName))
                    {
                        content = documentName;
                        return true;
                    }

                    return false;
                case StandardTableColumnDefinitions.ProjectName:

                    var projectName = GetProjectFile(Message);

                    if (!string.IsNullOrEmpty(projectName))
                    {
                        content = Path.GetFileNameWithoutExtension(projectName);
                        return true;
                    }

                    return false;
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

        private static string GetProjectFile(ILogMessage logMessage)
        {
            string file = null;
            if (!string.IsNullOrEmpty(logMessage.ProjectPath))
            {
                file = logMessage.ProjectPath;
            }
            else if (logMessage is RestoreLogMessage)
            {
                file = (logMessage as RestoreLogMessage).FilePath;
            }

            return file;
        }
    }
}
