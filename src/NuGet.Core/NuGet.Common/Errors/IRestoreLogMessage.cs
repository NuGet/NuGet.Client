﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{
    public interface IRestoreLogMessage : ILogMessage, ILogFileContext
    {
        /// <summary>
        /// Project or Package Id
        /// </summary>
        string LibraryId { get; set; }

        /// <summary>
        /// List of TargetGraphs
        /// </summary>
        IReadOnlyList<string> TargetGraphs { get; set; }
    }
}
