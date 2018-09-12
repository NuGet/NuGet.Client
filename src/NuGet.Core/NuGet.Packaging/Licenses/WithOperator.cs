// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class WithOperator : LicenseOperator
    {
        public WithOperator(NuGetLicense license, NuGetLicenseException exception)
        {
            License = license ?? throw new ArgumentNullException(nameof(license));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Type = LicenseExpressionType.Operator;
        }

        public NuGetLicense License { get; private set; }
        public NuGetLicenseException Exception { get; private set; }

        public override string ToString()
        {
            return $"{License.Identifier} WITH {Exception.Identifier}";
        }
    }
}
