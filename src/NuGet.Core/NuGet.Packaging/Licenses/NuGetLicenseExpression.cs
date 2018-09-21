// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Licenses
{
    /// <summary>
    /// Represents a parsed NuGetLicenseExpression.
    /// This is an abstract class so based on the Type, it can be either a <see cref="NuGetLicense"/> or a <see cref="LicenseOperator"/>.
    /// <seealso cref="LicenseExpressionType"/>
    /// </summary>
    public abstract class NuGetLicenseExpression
    {
        /// <summary>
        /// The type of the NuGetLicenseExpression.
        /// License type means that it's a <see cref="NuGetLicense"/>.
        /// Operator means that it's a <see cref="LicenseOperator"/>
        /// </summary>
        public LicenseExpressionType Type { get; protected set; }
    }
}