// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLicenseUtilities
    {
        private static string LicenseFormat = "https://licenses.nuget.org/{0}";

        internal static IReadOnlyList<IText> GenerateLicenseLinks(DetailedPackageMetadata metadata)
        {
            return GenerateLicenseLinks(metadata.LicenseMetadata, metadata.LicenseUrl, string.Format(CultureInfo.CurrentCulture, Resources.WindowTitle_LicenseFileWindow, metadata.Id), metadata.LoadFileAsText);
        }

        internal static IReadOnlyList<IText> GenerateLicenseLinks(IPackageSearchMetadata metadata)
        {
            if (metadata is LocalPackageSearchMetadata localMetadata)
            {
                return GenerateLicenseLinks(metadata.LicenseMetadata, metadata.LicenseUrl, string.Format(CultureInfo.CurrentCulture, Resources.WindowTitle_LicenseFileWindow, metadata.Identity.Id), localMetadata.LoadFileAsText);
            }
            return GenerateLicenseLinks(metadata.LicenseMetadata, metadata.LicenseUrl, metadata.Identity.Id, null);
        }

        internal static IReadOnlyList<IText> GenerateLicenseLinks(LicenseMetadata licenseMetadata, Uri licenseUrl, string licenseFileHeader, Func<string, string> loadFile)
        {
            if (licenseMetadata != null)
            {
                return GenerateLicenseLinks(licenseMetadata, licenseFileHeader, loadFile);
            }
            else if (licenseUrl != null)
            {
                return new List<IText>() { new LicenseText(Resources.Text_ViewLicense, licenseUrl) };
            }
            return new List<IText>();
        }

        // Internal for testing purposes.
        internal static IReadOnlyList<IText> GenerateLicenseLinks(LicenseMetadata metadata, string licenseFileHeader, Func<string, string> loadFile)
        {
            var list = new List<IText>();

            if (metadata.WarningsAndErrors != null)
            {
                list.Add(new WarningText(string.Join(Environment.NewLine, metadata.WarningsAndErrors)));
            }

            switch (metadata.Type)
            {
                case LicenseType.Expression:

                    if (metadata.LicenseExpression != null && !metadata.LicenseExpression.IsUnlicensed())
                    { 
                        var identifiers = new List<string>();
                        PopulateLicenseIdentifiers(metadata.LicenseExpression, identifiers);

                        var licenseToBeProcessed = metadata.License;

                        foreach (var identifier in identifiers)
                        {
                            var licenseStart = licenseToBeProcessed.IndexOf(identifier);
                            if (licenseStart != 0)
                            {
                                list.Add(new FreeText(licenseToBeProcessed.Substring(0, licenseStart)));
                            }
                            var license = licenseToBeProcessed.Substring(licenseStart, identifier.Length);
                            list.Add(new LicenseText(license, new Uri(string.Format(LicenseFormat, license))));
                            licenseToBeProcessed = licenseToBeProcessed.Substring(licenseStart + identifier.Length);
                        }

                        if (licenseToBeProcessed.Length != 0)
                        {
                            list.Add(new FreeText(licenseToBeProcessed));
                        }
                    }
                    else
                    {
                        list.Add(new FreeText(metadata.License));
                    }

                    break;

                case LicenseType.File:
                    list.Add(new LicenseFileText(Resources.Text_ViewLicense, licenseFileHeader, loadFile, metadata.License));
                    break;

                default:
                    break;
            }

            return list;
        }

        private static void PopulateLicenseIdentifiers(NuGetLicenseExpression expression, IList<string> identifiers)
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
                            PopulateLicenseIdentifiers(logicalOperator.Left, identifiers);
                            PopulateLicenseIdentifiers(logicalOperator.Right, identifiers);
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
