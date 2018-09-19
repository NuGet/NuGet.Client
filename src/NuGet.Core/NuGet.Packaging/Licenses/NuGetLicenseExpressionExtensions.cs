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
                case LicenseExpressionType.License:

                    return (expression as NuGetLicense).IsStandardLicense;

                case LicenseExpressionType.Operator:

                    var licenseOperator = expression as LicenseOperator;
                    switch (licenseOperator.OperatorType)
                    {
                        case OperatorType.LogicalOperator:
                            var logicalOperator = expression as LogicalOperator;
                            return logicalOperator.Left.HasOnlyStandardIdentifiers() && logicalOperator.Right.HasOnlyStandardIdentifiers();
                        case OperatorType.WithOperator:
                            var withOperator = expression as WithOperator;
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
                case LicenseExpressionType.License:
                    var license = expression as NuGetLicense;
                    licenseProcessor(license);
                    break;

                case LicenseExpressionType.Operator:
                    var licenseOperator = expression as LicenseOperator;
                    switch (licenseOperator.OperatorType)
                    {
                        case OperatorType.LogicalOperator:
                            var logicalOperator = expression as LogicalOperator;

                            logicalOperator.Left.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            logicalOperator.Right.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            break;

                        case OperatorType.WithOperator:
                            var withOperator = expression as WithOperator;
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