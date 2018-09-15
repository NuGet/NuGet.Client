// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicense : NuGetLicenseExpression
    {
        /// <summary>
        /// Identifier
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Signifies whether the plus operator has been specified on this license
        /// </summary>
        public bool Plus { get; }

        /// <summary>
        /// Signifies whether this is a standard license known by the NuGet APIs.
        /// Pack for example should warn for these.
        /// </summary>
        public bool IsStandardLicense { get; }

        public NuGetLicense(string identifier, bool plus, bool isStandardLicense)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            Plus = plus;
            IsStandardLicense = isStandardLicense;
            Type = LicenseExpressionType.License;
        }

        /// <summary>
        /// Parse a licenseIdentifier. If a licenseIdentifier is deprecated, this will throw. Non-standard licenses get parsed into a object model as well.
        /// </summary>
        /// <param name="licenseIdentifier"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If the identifier is deprecated</exception>
        /// <exception cref="ArgumentException">If it's null or empty.</exception>
        public static NuGetLicense Parse(string licenseIdentifier)
        {
            if (!string.IsNullOrWhiteSpace(licenseIdentifier))
            {
                if (NuGetLicenseData.LicenseList.TryGetValue(licenseIdentifier, out var licenseData))
                {
                    return !licenseData.IsDeprecatedLicenseId ?
                        new NuGetLicense(licenseIdentifier, plus: false, isStandardLicense: true) :
                        throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_DeprecatedIdentifier, licenseIdentifier));
                }
                else
                {
                    if (licenseIdentifier[licenseIdentifier.Length - 1] == '+')
                    {
                        var cleanIdentifier = licenseIdentifier.Substring(0, licenseIdentifier.Length - 1);
                        var plus = true;
                        if (NuGetLicenseData.LicenseList.TryGetValue(cleanIdentifier, out licenseData))
                        {
                            return !licenseData.IsDeprecatedLicenseId ?
                                new NuGetLicense(cleanIdentifier, plus: plus, isStandardLicense: true) :
                                throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_DeprecatedIdentifier, licenseIdentifier));
                        }
                        return ProcessNonStandardLicense(cleanIdentifier, plus: plus);
                    }

                    return ProcessNonStandardLicense(licenseIdentifier, plus: false);
                }
            }
            // This will not happen in production code as the tokenizer takes cares of that. 
            throw new ArgumentException(Strings.NuGetLicenseExpression_LicenseIdentifierIsException, nameof(licenseIdentifier));
        }

        private static NuGetLicense ProcessNonStandardLicense(string licenseIdentifier, bool plus)
        {
            if (!NuGetLicenseData.ExceptionList.TryGetValue(licenseIdentifier, out var exceptionData))
            {
                return new NuGetLicense(licenseIdentifier, plus: plus, isStandardLicense: false);
            }
            throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_ExceptionIdentifierIsLicense, licenseIdentifier));
        }

        public override string ToString()
        {
            var plus = Plus ? "+" : string.Empty;
            return $"{Identifier}{plus}";
        }
    }
}
