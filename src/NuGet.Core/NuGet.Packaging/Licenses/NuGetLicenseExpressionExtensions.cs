// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Licenses
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
                        case LicenseOperatorType.LogicalOperator:
                            var logicalOperator = (LogicalOperator)licenseOperator;
                            return logicalOperator.Left.HasOnlyStandardIdentifiers() && logicalOperator.Right.HasOnlyStandardIdentifiers();
                        case LicenseOperatorType.WithOperator:
                            var withOperator = (WithOperator)licenseOperator;
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
                    var license = (NuGetLicense)expression;
                    licenseProcessor(license);
                    break;

                case LicenseExpressionType.Operator:
                    var licenseOperator = (LicenseOperator)expression;
                    switch (licenseOperator.OperatorType)
                    {
                        case LicenseOperatorType.LogicalOperator:
                            var logicalOperator = (LogicalOperator)licenseOperator;

                            logicalOperator.Left.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            logicalOperator.Right.OnEachLeafNode(licenseProcessor, exceptionProcessor);
                            break;

                        case LicenseOperatorType.WithOperator:
                            var withOperator = (WithOperator)licenseOperator;
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