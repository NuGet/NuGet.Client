// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLicenseUtilities
    {

        internal static IList<IText> GenerateLicenseLinks(DetailedPackageMetadata metadata)
        {
            return GenerateLicenseLinks(metadata.LicenseMetadata, metadata.LicenseUrl);
        }

        internal static IList<IText> GenerateLicenseLinks(IPackageSearchMetadata metadata)
        {
            return GenerateLicenseLinks(metadata.LicenseMetadata, metadata.LicenseUrl);
        }

        private static IList<IText> GenerateLicenseLinks(LicenseMetadata licenseMetadata, Uri licenseUrl)
        {
            IList<IText> list = new List<IText>();
            if (licenseMetadata != null && licenseMetadata.Type == LicenseType.Expression)
            {
                list = GenerateLicenseLinks(licenseMetadata);
            }
            else
            {
                list.Add(new LicenseText(Resources.Text_LicenseAcceptance, licenseUrl));
            }
            return list;
        }

        // Internal for testing purposes.
        internal static IList<IText> GenerateLicenseLinks(LicenseMetadata metadata)
        {
            var list = new List<IText>();

            var identifiers = new List<string>();
            GetLicenseIdentifiers(metadata.LicenseExpression, identifiers);

            var licenseToBeProcessed = metadata.License;

            foreach (var identifier in identifiers)
            {
                var licenseStart = licenseToBeProcessed.IndexOf(identifier);
                if (licenseStart != 0)
                {
                    list.Add(new FreeText(licenseToBeProcessed.Substring(0, licenseStart)));
                }
                var license = licenseToBeProcessed.Substring(licenseStart, identifier.Length);
                list.Add(new LicenseText(license, new Uri($"https://spdx.org/licenses/{license}.html")));

                licenseToBeProcessed = licenseToBeProcessed.Substring(licenseStart + identifier.Length);
            }

            if (licenseToBeProcessed.Length != 0)
            {
                list.Add(new FreeText(licenseToBeProcessed));
            }
            return list;
        }

        private static void GetLicenseIdentifiers(NuGetLicenseExpression expression, IList<string> identifiers)
        {
            switch (expression.Type)
            {
                case LicenseExpressionType.License:
                    var license = (NuGetLicense)expression;
                    identifiers.Add(license.Identifier);
                    break;

                case LicenseExpressionType.Operator:
                    var licenseOperator = (LicenseOperator)expression;
                    switch (licenseOperator.OperatorType)
                    {
                        case LicenseOperatorType.LogicalOperator:
                            var logicalOperator = (LogicalOperator)licenseOperator;
                            GetLicenseIdentifiers(logicalOperator.Left, identifiers);
                            GetLicenseIdentifiers(logicalOperator.Right, identifiers);
                            break;

                        case LicenseOperatorType.WithOperator:
                            var withOperator = (WithOperator)licenseOperator;
                            identifiers.Add(withOperator.License.Identifier);
                            identifiers.Add(withOperator.Exception.Identifier);
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
