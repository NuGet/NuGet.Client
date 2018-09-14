// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class NuGetLicenseException
    {
        public string Identifier { get; }

        public bool IsStandardException { get; }

        public NuGetLicenseException(string identifier, bool isStandardException)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            IsStandardException = isStandardException;
        }

        public static NuGetLicenseException Parse(string exceptionIdentifier)
        {
            if (!string.IsNullOrWhiteSpace(exceptionIdentifier))
            {
                if (NuGetLicenseData.ExceptionList.TryGetValue(exceptionIdentifier, out var exceptionData))
                {
                    return !exceptionData.IsDeprecatedLicenseId ?
                        new NuGetLicenseException(exceptionIdentifier, isStandardException: true) :
                        throw new ArgumentException(string.Format(Strings.LicenseExpression_DeprecatedIdentifier, exceptionIdentifier));

                }
                return new NuGetLicenseException(exceptionIdentifier, false);
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
