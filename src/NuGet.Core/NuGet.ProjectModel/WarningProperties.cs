// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Class to hold warning properties given by project system.
    /// </summary>
    public class WarningProperties
    {
        /// <summary>
        /// List of Warning Codes that should be treated as Errors.
        /// </summary>
        public IList<NuGetLogCode> WarningsAsErrorsList { get; }

        /// <summary>
        /// List of Warning Codes that should be ignored.
        /// </summary>
        public IList<NuGetLogCode> NoWarnList { get; }

        /// <summary>
        /// Indicates if all warnings should be ignored.
        /// </summary>
        public bool AllWarningsAsErrors { get; }

        public WarningProperties(IList<NuGetLogCode> warningsAsErrorsList, IList<NuGetLogCode> noWarnList, bool allWarningsAsErrors)
        {
            WarningsAsErrorsList = warningsAsErrorsList;
            NoWarnList = noWarnList;
            AllWarningsAsErrors = allWarningsAsErrors;
        }
    }
}
