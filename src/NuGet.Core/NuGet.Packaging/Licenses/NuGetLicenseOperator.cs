// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Packaging
{
    /// <summary>
    /// A NuGetLicenseOperator. The operator options are: WITH or Logical operator, AND and OR.
    /// This is an abstract class so based on the NuGetLicenseOperatorType, it can be either a <see cref="WithOperator"/> or a <see cref="NuGetLicenseLogicalOperator"/>.
    /// </summary>
    public abstract class NuGetLicenseOperator : NuGetLicenseExpression
    {
        /// <summary>
        /// The operator type.
        /// LogicalOperator means it's AND or OR and <see cref="NuGetLicenseLogicalOperator"/>
        /// NuGetLicenseWithOperator means it's the WITH operator and <see cref="WithOperator"/>
        /// </summary>
        public NuGetLicenseOperatorType OperatorType { get; protected set; }

        protected NuGetLicenseOperator()
        {
            Type = NuGetLicenseExpressionType.Operator;
        }
    }
}
