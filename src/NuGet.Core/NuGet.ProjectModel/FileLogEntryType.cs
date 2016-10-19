// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Log level for file entries
    /// </summary>
    public enum FileLogEntryType : ushort
    {
        None = 0,
        Error = 1,
        Warning = 2
    }
}