// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace NuGet.Packaging
{
    /// <summary>
    /// A Logical Operator NuGetLicenseExpression.
    /// It is either an OR or an AND operator, represented by <see cref="LogicalOperatorType"/>.
    /// This operator will always have a left and a right side, both of which are <see cref="NuGetLicenseExpression"/> and never null. 
    /// </summary>
    public class NuGetLicenseLogicalOperator : NuGetLicenseOperator
    {
        public NuGetLicenseLogicalOperator(NuGetLicenseLogicalOperatorType logicalOperatorType, NuGetLicenseExpression left, NuGetLicenseExpression right) : base()
        {
            LogicalOperatorType = logicalOperatorType;
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            OperatorType = NuGetLicenseOperatorType.LogicalOperator;
        }
        /// <summary>
        /// Represents the logical operator type of NuGetLicenseExpression.
        /// </summary>
        public NuGetLicenseLogicalOperatorType LogicalOperatorType { get; }
        public NuGetLicenseExpression Left { get; }
        public NuGetLicenseExpression Right { get; }

        public override string ToString()
        {
            return $"{Left.ToString()} {LogicalOperatorType} {Right.ToString()}";
        }
    }
}
