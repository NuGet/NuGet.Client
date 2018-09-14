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

        public bool IsStandardLicense { get; }

        public NuGetLicense(string identifier, bool plus, bool deprecated, bool isStandardLicense)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            Plus = plus;
            IsDeprecated = deprecated;
            IsStandardLicense = isStandardLicense;
            Type = LicenseExpressionType.License;
        }

        // TODO NK - the plus might need to be parsed. Consider making the parser more future proof.
        public static NuGetLicense Parse(string identifier, bool strict = true)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                if (NuGetLicenseData.LicenseList.TryGetValue(identifier, out var licenseData))
                {
                    return new NuGetLicense(identifier, plus: false, deprecated: licenseData.IsDeprecatedLicenseId, isStandardLicense: true);
                }
                else
                {
                    if (identifier[identifier.Length - 1] == '+')
                    {
                        var cleanIdentifier = identifier.Substring(0, identifier.Length - 1);
                        var plus = true;
                        if (NuGetLicenseData.LicenseList.TryGetValue(cleanIdentifier, out licenseData))
                        {
                            return new NuGetLicense(cleanIdentifier, plus: plus, deprecated: licenseData.IsDeprecatedLicenseId, isStandardLicense: true);
                        }

                        if (!strict)
                        {
                            return new NuGetLicense(cleanIdentifier, plus: plus, deprecated: false, isStandardLicense: false);
                        }
                    }
                    else
                    {
                        if (!strict)
                        {
                            return new NuGetLicense(identifier, plus: false, deprecated: false, isStandardLicense: false);
                        }
                    }
                }
            }
            throw new ArgumentException($"The NuGet License cannot be parsed.{identifier}");
        }

        public override string ToString()
        {
            var plus = Plus ? "+" : string.Empty;
            return $"{Identifier}{plus}";
        }
    }
}
