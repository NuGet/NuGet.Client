// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicenseException
    {
        public NuGetLicenseException(string identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        public string Identifier { get; }

        public static NuGetLicenseException Parse(string identifier)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                return new NuGetLicenseException(identifier);
            }
            return null;
        }
    }
}
