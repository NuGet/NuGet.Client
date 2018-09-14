// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicense : NuGetLicenseExpression
    {
        public string Identifier { get; }
        public bool Plus { get; }

        public bool IsDeprecated { get; }

        public NuGetLicense(string identifier, bool plus, bool deprecated)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            Plus = plus;
            IsDeprecated = deprecated;
            Type = LicenseExpressionType.License;
        }

        public static NuGetLicense Parse(string identifier, bool strict = true)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                if (NuGetLicenseData.LicenseList.TryGetValue(identifier, out var licenseData))
                {
                    return new NuGetLicense(identifier, plus: false, deprecated: licenseData.IsDeprecatedLicenseId);
                }
                else
                {

                    // TODO NK - Do the actual parsing.
                    if (identifier[identifier.Length - 1] == '+')
                    {
                        return new NuGetLicense(identifier.Substring(0, identifier.Length - 1), true);
                    }
                }
            }
            return null;
        }

        public override string ToString()
        {
            var plus = Plus ? "+" : string.Empty;
            return $"{Identifier}{plus}";
        }
    }
}
