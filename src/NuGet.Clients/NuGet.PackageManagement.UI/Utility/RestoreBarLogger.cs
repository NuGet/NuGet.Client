// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI.Utility
{
    internal class RestoreBarLogger : LoggerBase
    {
        private readonly INuGetProjectContext _nuGetProjectContext;

        public RestoreBarLogger(INuGetProjectContext nuGetProjectContext)
            : base(verbosityLevel: LogLevel.Warning)
        {
            _nuGetProjectContext = nuGetProjectContext ?? throw new ArgumentNullException(nameof(nuGetProjectContext));
        }

        public override void Log(ILogMessage message)
        {
            _nuGetProjectContext.Log(message);
        }

        public override Task LogAsync(ILogMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
