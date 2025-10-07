// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Protocol
{
    internal static class PackageIdValidator
    {
        /// <summary>
        /// Validates the package ID content.
        /// </summary>
        /// <param name="packageId">The package ID to validate.</param>
        internal static void Validate(string packageId, IEnvironmentVariableReader env = null)
        {
            // Do nothing
        }
    }
}
