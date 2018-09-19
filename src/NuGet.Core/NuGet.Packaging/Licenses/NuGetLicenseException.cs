// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Packaging
{
    /// <summary>
    /// NuGet's internal representation of a license exception identifier. 
    /// </summary>
    public class NuGetLicenseException
    {
        /// <summary>
        /// The Exception's identifier
        /// </summary>
        public string Identifier { get; }

        private NuGetLicenseException(string identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        /// <summary>
        /// Parse an exceptionIdentifier. If the exceptionIdentifier is deprecated, this will throw. Non-standard exception do not get parsed into an object model.
        /// </summary>
        /// <param name="exceptionIdentifier"></param>
        /// <returns>NuGetLicenseException</returns>
        // <exception cref="ArgumentException">If the identifier is deprecated</exception>
        /// <exception cref="ArgumentException">If it's null or empty.</exception>
        public static NuGetLicenseException Parse(string exceptionIdentifier)
        {
            if (!string.IsNullOrWhiteSpace(exceptionIdentifier))
            {
                if (NuGetLicenseData.ExceptionList.TryGetValue(exceptionIdentifier, out var exceptionData))
                {
                    return !exceptionData.IsDeprecatedLicenseId ?
                        new NuGetLicenseException(exceptionIdentifier) :
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_DeprecatedIdentifier, exceptionIdentifier));
                }
                else
                {
                    if (NuGetLicenseData.LicenseList.TryGetValue(exceptionIdentifier, out var licenseData))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_ExceptionIdentifierIsLicense, exceptionIdentifier));
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_InvalidExceptionIdentifier, exceptionIdentifier));
                    }
                }
            }
            // This will not happen in production code as the tokenizer takes cares of that. 
            throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(exceptionIdentifier));
        }

        public override string ToString()
        {
            return Identifier;
        }
    }
}
