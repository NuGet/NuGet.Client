// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicenseException
    {

        public bool IsDeprecated { get; }
        public string Identifier { get; }

        public NuGetLicenseException(string identifier, bool isDeprecated)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            IsDeprecated = isDeprecated;
        }

        // TODO NK - maybe we have different handling for the deprecated IDs.
        public static NuGetLicenseException Parse(string identifier, bool strict = true)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                if(NuGetLicenseData.ExceptionList.TryGetValue(identifier, out var exceptionData))
                {
                    return new NuGetLicenseException(identifier, exceptionData.IsDeprecatedLicenseId);
                }
                if (!strict)
                {
                    return new NuGetLicenseException(identifier, false);
                }
            }
            throw new ArgumentException($"Invalid License Exception {identifier}");
        }

        public override string ToString()
        {
            return Identifier;
        }
    }
}
