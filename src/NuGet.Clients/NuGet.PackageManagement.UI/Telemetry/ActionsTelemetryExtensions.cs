// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Telemetry;
using NuGet.PackageManagement.Telemetry;
using NuGet.PackageManagement.UI;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using TelemetryEvent = NuGet.Common.TelemetryEvent;

namespace NuGet.VisualStudio
{
    internal static class ActionsTelemetryExtensions
    {
        internal static void AddUiActionEngineTelemetryProperties(
            this VSActionsTelemetryEvent actionTelemetryEvent,
            bool continueAfterPreview,
            bool acceptedLicense,
            UserAction userAction,
            IEnumerable<PackageItemViewModel> selectedPackages,
            DetailControlModel activePackageDetail,
            int? selectedIndex,
            int? recommendedCount,
            bool? recommendPackages,
            (string modelVersion, string vsixVersion)? recommenderVersion,
            HashSet<Tuple<string, string>> existingPackages,
            List<Tuple<string, string>> addedPackages,
            List<string> removedPackages,
            List<Tuple<string, string>> updatedPackagesOld,
            List<Tuple<string, string>> updatedPackagesNew,
            IReadOnlyCollection<string> targetFrameworks)
        {
            // log possible cancel reasons
            if (!continueAfterPreview)
            {
                actionTelemetryEvent["CancelAfterPreview"] = "True";
            }

            if (!acceptedLicense)
            {
                actionTelemetryEvent["AcceptedLicense"] = "False";
            }

            // log the single top level package the user is installing or removing
            if (userAction != null)
            {
                // userAction.Version can be null for deleted packages.
                actionTelemetryEvent.ComplexData["SelectedPackage"] = ToTelemetryPackage(userAction.PackageId, userAction.Version);
                actionTelemetryEvent["SelectedIndex"] = selectedIndex;
                actionTelemetryEvent["RecommendedCount"] = recommendedCount;
                actionTelemetryEvent["RecommendPackages"] = recommendPackages;
                actionTelemetryEvent["Recommender.ModelVersion"] = recommenderVersion?.modelVersion;
                actionTelemetryEvent["Recommender.VsixVersion"] = recommenderVersion?.vsixVersion;
            }

            IEnumerable<PackageItemViewModel> vulnerableSelectedPkgs = Enumerable.Empty<PackageItemViewModel>();
            IEnumerable<PackageItemViewModel> deprecatedSelectedPkgs = Enumerable.Empty<PackageItemViewModel>();
            int vulnerablePkgsCount = 0;
            List<int> vulnerablePkgsMaxSeverities = new List<int>();

            // Selected packages in packages list
            if (selectedPackages != null)
            {
                vulnerableSelectedPkgs = selectedPackages?
                 .Where(x => x.IsPackageVulnerable || (x.Vulnerabilities?.Any() ?? false))
                 ?? Enumerable.Empty<PackageItemViewModel>();
                vulnerablePkgsCount = vulnerableSelectedPkgs.Count();
                vulnerablePkgsMaxSeverities = vulnerableSelectedPkgs
                    .Select(pkg => pkg?.Vulnerabilities?.Max(v => v.Severity) ?? -1)
                    .ToList();

                deprecatedSelectedPkgs = selectedPackages?
                    .Where(x => x.IsPackageDeprecated || x.DeprecationMetadata != null)
                    ?? Enumerable.Empty<PackageItemViewModel>();
            }

            if (vulnerableSelectedPkgs.Any())
            {
                actionTelemetryEvent.ComplexData["TopLevelVulnerablePackages"] = ToTelemetryPackageList(vulnerableSelectedPkgs, ToTelemetryVulnerablePackage);
            }
            actionTelemetryEvent["TopLevelVulnerablePackagesCount"] = vulnerablePkgsCount;
            actionTelemetryEvent.ComplexData["TopLevelVulnerablePackagesMaxSeverities"] = vulnerablePkgsMaxSeverities;

            if (deprecatedSelectedPkgs.Any())
            {
                actionTelemetryEvent.ComplexData["TopLevelDeprecatedPackages"] = ToTelemetryPackageList(deprecatedSelectedPkgs, ToTelemetryDeprecatedPackage);
            }

            // package in detail pane
            if (activePackageDetail != null)
            {
                if (activePackageDetail.IsPackageDeprecated || activePackageDetail.PackageMetadata?.DeprecationMetadata != null)
                {
                    actionTelemetryEvent.ComplexData["DetailDeprecatedPackage"] = ToTelemetryDeprecatedPackage(
                        activePackageDetail.Id,
                        activePackageDetail.SelectedVersion.Version,
                        activePackageDetail.PackageMetadata.DeprecationMetadata);
                }

                if (activePackageDetail.IsPackageVulnerable || activePackageDetail.PackageMetadata?.Vulnerabilities != null)
                {
                    actionTelemetryEvent.ComplexData["DetailVulnerablePackage"] = ToTelemetryVulnerablePackage(
                        activePackageDetail.Id,
                        activePackageDetail.SelectedVersion.Version,
                        activePackageDetail.PackageMetadata.Vulnerabilities);
                }
            }

            // log the installed package state
            if (existingPackages?.Count > 0)
            {
                actionTelemetryEvent.ComplexData["ExistingPackages"] = ToTelemetryPackageList(existingPackages);
            }

            // other packages can be added, removed, or upgraded as part of bulk upgrade or as part of satisfying package dependencies, so log that also
            if (addedPackages?.Count > 0)
            {
                actionTelemetryEvent.ComplexData["AddedPackages"] = ToTelemetryPackageList(addedPackages);
            }

            if (removedPackages?.Count > 0)
            {
                actionTelemetryEvent.ComplexData["RemovedPackages"] = ToTelemetryPackageList(removedPackages, ToTelemetryDeletedPackage);
            }

            // two collections for updated packages: pre and post upgrade
            if (updatedPackagesNew?.Count > 0)
            {
                actionTelemetryEvent.ComplexData["UpdatedPackagesNew"] = ToTelemetryPackageList(updatedPackagesNew);
            }

            if (updatedPackagesOld?.Count > 0)
            {
                actionTelemetryEvent.ComplexData["UpdatedPackagesOld"] = ToTelemetryPackageList(updatedPackagesOld);
            }

            // target framworks
            if (targetFrameworks?.Count > 0)
            {
                actionTelemetryEvent["TargetFrameworks"] = string.Join(";", targetFrameworks);
            }
        }

        internal static TelemetryEvent ToTelemetryPackage(string id, string version)
        {
            var subEvent = new TelemetryEvent(eventName: null);
            subEvent.AddPiiData("id", VSTelemetryServiceUtility.NormalizePackageId(id));
            subEvent["version"] = version;
            return subEvent;
        }

        internal static TelemetryEvent ToTelemetryPackage(string id, NuGetVersion version) => ToTelemetryPackage(id, VSTelemetryServiceUtility.NormalizeVersion(version));

        internal static TelemetryEvent ToTelemetryPackage(Tuple<string, string> package) => ToTelemetryPackage(package.Item1, package.Item2);

        internal static TelemetryPiiProperty ToTelemetryDeletedPackage(string pkg)
        {
            return new TelemetryPiiProperty(VSTelemetryServiceUtility.NormalizePackageId(pkg));
        }

        internal static TelemetryEvent ToTelemetryVulnerablePackage(string id, NuGetVersion version, IEnumerable<PackageVulnerabilityMetadataContextInfo> vulnerabilities)
        {
            var evt = ToTelemetryPackage(id, version);

            if (vulnerabilities?.Count() > 0)
            {
                evt.ComplexData["Severities"] = vulnerabilities.Select(v => v.Severity).ToList();
            }

            return evt;
        }

        internal static TelemetryEvent ToTelemetryVulnerablePackage(PackageItemViewModel package)
        {
            var evt = ToTelemetryVulnerablePackage(package.Id, package.Version, package.Vulnerabilities);

            evt["IsLatestVersionVulnerable"] = package.IsLatestVersionVulnerable;

            return evt;
        }

        internal static TelemetryEvent ToTelemetryDeprecatedPackage(PackageItemViewModel package)
        {
            var evt = ToTelemetryDeprecatedPackage(package.Id, package.Version, package.DeprecationMetadata);

            evt["IsLatestVersionDeprecated"] = package.IsLatestVersionDeprecated;

            return evt;
        }

        internal static TelemetryEvent ToTelemetryDeprecatedPackage(string id, NuGetVersion version, PackageDeprecationMetadataContextInfo deprecation)
        {
            var evt = ToTelemetryPackage(id, version);

            if (deprecation?.AlternatePackage != null)
            {
                evt.ComplexData["AlternativePackage"] = ToTelemetryPackage(
                    VSTelemetryServiceUtility.NormalizePackageId(deprecation.AlternatePackage.PackageId),
                    VSTelemetryServiceUtility.NormalizeVersion(deprecation.AlternatePackage.VersionRange));
            }
            if (deprecation?.Reasons?.Count() > 0)
            {
                evt.ComplexData["Reasons"] = deprecation.Reasons.ToList();
            }

            return evt;
        }

        internal static List<TelemetryEvent> ToTelemetryPackageList(IEnumerable<Tuple<string, string>> packages) => ToTelemetryPackageList(packages, ToTelemetryPackage);

        internal static List<V> ToTelemetryPackageList<V, T>(IEnumerable<T> packages, Func<T, V> transformer) => packages.Select(transformer).ToList();
    }
}
