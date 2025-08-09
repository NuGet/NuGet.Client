// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol
{
    internal class ValidatePackageId
    {
        internal IEnvironmentVariableReader _environmentVariableReader;

        public ValidatePackageId(IEnvironmentVariableReader environment = null)
        {
            _environmentVariableReader = environment ?? EnvironmentVariableWrapper.Instance;
        }
        internal void Validate(string packageId)
        {
            string envVar = _environmentVariableReader.GetEnvironmentVariable("NUGET_DISABLE_PACKAGEID_VALIDATION");

            if (!string.Equals(envVar, "true", StringComparison.OrdinalIgnoreCase))
            {
                Packaging.PackageIdValidator.ValidatePackageIdRegex(packageId);
            }
        }
    }
}
