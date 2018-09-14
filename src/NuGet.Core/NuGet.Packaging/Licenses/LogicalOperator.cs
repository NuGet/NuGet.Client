// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Packaging
{
    public class LogicalOperator : LicenseOperator
    {
        public LogicalOperator(LogicalOperatorType logicalOperatorType, NuGetLicenseExpression left, NuGetLicenseExpression right) : base()
        {
            LogicalOperatorType = logicalOperatorType;
            Left = left;
            Right = right;
        }

        public LogicalOperatorType LogicalOperatorType { get; }
        public NuGetLicenseExpression Left { get; }
        public NuGetLicenseExpression Right { get; }

        public override string ToString()
        {
            return $"{Left.ToString()} {LogicalOperatorType} {Right.ToString()}";
        }
    }
}
