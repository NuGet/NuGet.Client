// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public static class NuGetLicenseExpressionExtensions
    {
        /// <summary>
        /// Determines whether all the licenses and exceptions are not deprecated.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static bool HasOnlyStandardIdentifiers(this NuGetLicenseExpression expression)
        {
            switch (expression.Type)
            {
                case NuGetLicenseExpressionType.License:

                    return (expression as NuGetLicense).IsStandardLicense;

                case NuGetLicenseExpressionType.Operator:

                    var licenseOperator = expression as NuGetLicenseOperator;
                    switch (licenseOperator.OperatorType)
                    {
                        case NuGetLicenseOperatorType.LogicalOperator:
                            var logicalOperator = expression as NuGetLicenseLogicalOperator;
                            return logicalOperator.Left.HasOnlyStandardIdentifiers() && logicalOperator.Right.HasOnlyStandardIdentifiers();
                        case NuGetLicenseOperatorType.WithOperator:
                            var withOperator = expression as NuGetLicenseWithOperator;
                            return withOperator.License.IsStandardLicense;
                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }

        /// <summary>
        /// A leaf node in an expression can only be a License or an Exception. Run a func on each one.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="licenseProcessor"></param>
        /// <param name="exceptionProcessor"></param>
        public static void OnEachLeafNode(this NuGetLicenseExpression expression, Action<NuGetLicense> licenseProcessor, Action<NuGetLicenseException> exceptionProcessor)
        {
            switch (expression.Type)
            {
                case NuGetLicenseExpressionType.License:
                    var license = expression as NuGetLicense;
                    licenseProcessor(license);
                    break;

                case NuGetLicenseExpressionType.Operator:
                    var licenseOperator = expression as NuGetLicenseOperator;
                    switch (licenseOperator.OperatorType)
                    {
                        case NuGetLicenseOperatorType.LogicalOperator:
                            var logicalOperator = expression as NuGetLicenseLogicalOperator;

                            logicalOperator.Left.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            logicalOperator.Right.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            break;

                        case NuGetLicenseOperatorType.WithOperator:
                            var withOperator = expression as NuGetLicenseWithOperator;
                            licenseProcessor(withOperator.License);
                            exceptionProcessor(withOperator.Exception);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }
    }
}