// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    /// <summary>
    /// Represents a <see cref="NuGetLicenseExpression"/> that's a WITH operator.
    /// It has a License and Exception.
    /// </summary>
    public class NuGetLicenseWithOperator : NuGetLicenseOperator
    {
        public NuGetLicenseWithOperator(NuGetLicense license, NuGetLicenseException exception) : base()
        {
            License = license ?? throw new ArgumentNullException(nameof(license));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            OperatorType = NuGetLicenseOperatorType.WithOperator;
        }

        /// <summary>
        /// The license.
        /// </summary>
        public NuGetLicense License { get; private set; }

        /// <summary>
        /// The exception.
        /// </summary>
        public NuGetLicenseException Exception { get; private set; }

        public override string ToString()
        {
            return $"{License.ToString()} WITH {Exception.ToString()}";
        }
    }
}
