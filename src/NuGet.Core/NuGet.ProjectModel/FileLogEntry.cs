// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Stores a console output message to a file.
    /// </summary>
    public class FileLogEntry
    {
        public FileLogEntryType Type { get; }

        public string Message { get; }

        public FileLogEntry(FileLogEntryType type, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Type = type;
            Message = message;
        }
    }
}