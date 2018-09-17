// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicenseException
    {
        public string Identifier { get; }

        public bool IsStandardException { get; }

        public NuGetLicenseException(string identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            IsStandardException = true;
        }

        public static NuGetLicenseException Parse(string exceptionIdentifier)
        {
            if (!string.IsNullOrWhiteSpace(exceptionIdentifier))
            {
                if (NuGetLicenseData.ExceptionList.TryGetValue(exceptionIdentifier, out var exceptionData))
                {
                    return !exceptionData.IsDeprecatedLicenseId ?
                        new NuGetLicenseException(exceptionIdentifier) :
                        throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_DeprecatedIdentifier, exceptionIdentifier));
                }
                else
                {
                    if (NuGetLicenseData.LicenseList.TryGetValue(exceptionIdentifier, out var licenseData))
                    {
                        throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_ExceptionIdentifierIsLicense, exceptionIdentifier));
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(Strings.NuGetLicenseExpression_InvalidExceptionIdentifier, exceptionIdentifier));
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
