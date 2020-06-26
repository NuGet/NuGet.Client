// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary> UI Logger factory. </summary>
    public interface INuGetUILoggerFactory
    {
        /// <summary> Create logger and start logging. </summary>
        /// <returns> An instance of <see cref="INuGetUILogger"/> implementation. </returns>
        INuGetUILogger Create();
    }
}
