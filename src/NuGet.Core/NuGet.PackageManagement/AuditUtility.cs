// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Model;
using NuGet.Versioning;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    internal partial class AuditUtility
    {
        private readonly bool _isExplicitOptIn;
        private readonly IEnumerable<PackageRestoreData> _packages;
        private readonly List<SourceRepository> _sourceRepositories;
        private readonly ILogger _logger;
        private readonly SourceCacheContext _sourceCacheContext;
        private readonly PackageVulnerabilitySeverity _minSeverity;

        internal List<string>? PackagesWithAdvisory { get; private set; }
        internal int Sev0Matches { get; private set; }
        internal int Sev1Matches { get; private set; }
        internal int Sev2Matches { get; private set; }
        internal int Sev3Matches { get; private set; }
        internal int InvalidSevDirectMatches { get; private set; }
        internal int SourcesWithVulnerabilityData { get; private set; }

        public AuditUtility(
            bool isExplicitOptIn,
            PackageVulnerabilitySeverity minSeverity,
            IEnumerable<PackageRestoreData> packages,
            List<SourceRepository> sourceRepositories,
            SourceCacheContext sourceCacheContext,
            ILogger logger)
        {
            _isExplicitOptIn = isExplicitOptIn;
            _minSeverity = minSeverity;
            _packages = packages;
            _sourceRepositories = sourceRepositories;
            _sourceCacheContext = sourceCacheContext;
            _logger = logger;
        }

        public static async Task<GetVulnerabilityInfoResult?> GetAllVulnerabilityDataAsync(List<SourceRepository> sourceRepositories, SourceCacheContext sourceCacheContext, ILogger logger, CancellationToken cancellationToken)
        {
            List<Task<GetVulnerabilityInfoResult?>>? results = new(sourceRepositories.Count);

            foreach (SourceRepository source in sourceRepositories)
            {
                Task<GetVulnerabilityInfoResult?> getVulnerabilityInfoResult = GetVulnerabilityInfoAsync(source, sourceCacheContext, logger);
                if (getVulnerabilityInfoResult != null)
                {
                    results.Add(getVulnerabilityInfoResult);
                }
            }

            await Task.WhenAll(results);
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            List<Exception>? errors = null;
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>? knownVulnerabilities = null;
            foreach (var resultTask in results)
            {
                GetVulnerabilityInfoResult? result = await resultTask;
                if (result is null) continue;

                if (result.KnownVulnerabilities != null)
                {
                    if (knownVulnerabilities == null)
                    {
                        knownVulnerabilities = new();
                    }

                    knownVulnerabilities.AddRange(result.KnownVulnerabilities);
                }

                if (result.Exceptions != null)
                {
                    if (errors == null)
                    {
                        errors = new();
                    }

                    errors.AddRange(result.Exceptions.InnerExceptions);
                }
            }

            GetVulnerabilityInfoResult? final =
                knownVulnerabilities != null || errors != null
                ? new(knownVulnerabilities, errors != null ? new AggregateException(errors) : null)
                : null;
            return final;

            static async Task<GetVulnerabilityInfoResult?> GetVulnerabilityInfoAsync(SourceRepository source, SourceCacheContext cacheContext, ILogger logger)
            {
                IVulnerabilityInfoResource vulnerabilityInfoResource =
                    await source.GetResourceAsync<IVulnerabilityInfoResource>(CancellationToken.None);
                if (vulnerabilityInfoResource is null)
                {
                    return null;
                }
                return await vulnerabilityInfoResource.GetVulnerabilityInfoAsync(cacheContext, logger, CancellationToken.None);
            }
        }

        public async Task CheckPackageVulnerabilitiesAsync(CancellationToken cancellationToken)
        {
            double? DownloadDurationSeconds;
            double? CheckPackagesDurationSeconds;
            double? GenerateOutputDurationSeconds;

            var stopwatch = Stopwatch.StartNew();
            GetVulnerabilityInfoResult? allVulnerabilityData = await GetAllVulnerabilityDataAsync(_sourceRepositories, _sourceCacheContext, _logger, cancellationToken);
            stopwatch.Stop();
            DownloadDurationSeconds = stopwatch.Elapsed.TotalSeconds;

            if (allVulnerabilityData?.Exceptions is not null)
            {
                ReplayErrors(allVulnerabilityData.Exceptions);
            }

            if (allVulnerabilityData is null || !IsAnyVulnerabilityDataFound(allVulnerabilityData.KnownVulnerabilities))
            {
                if (_isExplicitOptIn)
                {
                    var restoreLogMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1905, "No vulnerability data");
                    _logger.Log(restoreLogMessage);
                }
                return;
            }

            if (allVulnerabilityData.KnownVulnerabilities is not null)
            {
                stopwatch.Restart();
                Dictionary<PackageIdentity, PackageAuditInfo>? packagesWithKnownVulnerabilities =
                    FindPackagesWithKnownVulnerabilities(allVulnerabilityData.KnownVulnerabilities,
                                                        _packages,
                                                        _minSeverity);
                stopwatch.Stop();
                CheckPackagesDurationSeconds = stopwatch.Elapsed.TotalSeconds;
                if (packagesWithKnownVulnerabilities is not null)
                {
                    stopwatch.Restart();
                    CreateWarningsForPackagesWithVulnerabilities(packagesWithKnownVulnerabilities);
                    stopwatch.Stop();
                    GenerateOutputDurationSeconds = stopwatch.Elapsed.TotalSeconds;
                }
            }

            static bool IsAnyVulnerabilityDataFound(IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>? knownVulnerabilities)
            {
                if (knownVulnerabilities is null || knownVulnerabilities.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < knownVulnerabilities.Count; i++)
                {
                    if (knownVulnerabilities[i].Count > 0) { return true; }
                }

                return false;
            }
        }

        private void ReplayErrors(AggregateException exceptions)
        {
            foreach (Exception exception in exceptions.InnerExceptions)
            {
                var messageText = string.Format("Error fetching vulnerabilities", exception.Message);
                var logMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1900, messageText);
                _logger.Log(logMessage);
            }
        }

        private void CreateWarningsForPackagesWithVulnerabilities(Dictionary<PackageIdentity, PackageAuditInfo> packagesWithKnownVulnerabilities)
        {
            PackagesWithAdvisory = new(packagesWithKnownVulnerabilities.Count);
            foreach ((PackageIdentity package, PackageAuditInfo auditInfo) in packagesWithKnownVulnerabilities.OrderBy(p => p.Key.Id))
            {
                PackagesWithAdvisory.Add(package.Id);
                foreach (PackageVulnerabilityInfo vulnerability in auditInfo.Vulnerabilities)
                {
                    (var severityLabel, NuGetLogCode logCode) = PackageVulnerabilitySeverityHelper.GetSeverityLabelAndCode(vulnerability.Severity);
                    var message = string.Format("Package with known vulnerability",
                        package.Id,
                        package.Version.ToNormalizedString(),
                        severityLabel,
                        vulnerability.Url);
                    var restoreLogMessage =
                        RestoreLogMessage.CreateWarning(logCode,
                        message,
                        package.Id);
                    _logger.Log(restoreLogMessage);
                }
                foreach (var advisory in auditInfo.Vulnerabilities)
                {
                    PackageVulnerabilitySeverity severity = advisory.Severity;
                    if (severity == PackageVulnerabilitySeverity.Low) { Sev0Matches++; }
                    else if (severity == PackageVulnerabilitySeverity.Moderate) { Sev1Matches++; }
                    else if (severity == PackageVulnerabilitySeverity.High) { Sev2Matches++; }
                    else if (severity == PackageVulnerabilitySeverity.Critical) { Sev3Matches++; }
                    else { InvalidSevDirectMatches++; }
                }
            }
        }

        private static List<PackageVulnerabilityInfo>? GetKnownVulnerabilities(
            string name,
            NuGetVersion version,
            IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>? knownVulnerabilities)
        {
            HashSet<PackageVulnerabilityInfo>? vulnerabilities = null;

            if (knownVulnerabilities == null) return null;

            foreach (var file in knownVulnerabilities)
            {
                if (file.TryGetValue(name, out var packageVulnerabilities))
                {
                    foreach (var vulnInfo in packageVulnerabilities)
                    {
                        if (vulnInfo.Versions.Satisfies(version))
                        {
                            vulnerabilities ??= new();
                            vulnerabilities.Add(vulnInfo);
                        }
                    }
                }
            }

            return vulnerabilities != null ? vulnerabilities.ToList() : null;
        }

        private static Dictionary<PackageIdentity, PackageAuditInfo>? FindPackagesWithKnownVulnerabilities(
            IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities,
            IEnumerable<PackageRestoreData> packages, PackageVulnerabilitySeverity minSeverity)
        {
            Dictionary<PackageIdentity, PackageAuditInfo>? result = null;

            foreach (PackageRestoreData packageRestoreData in packages)
            {
                PackageIdentity packageIdentity = packageRestoreData.PackageReference.PackageIdentity;
                List<PackageVulnerabilityInfo>? knownVulnerabilitiesForPackage = GetKnownVulnerabilities(packageIdentity.Id, packageIdentity.Version, knownVulnerabilities);

                if (knownVulnerabilitiesForPackage?.Count > 0)
                {
                    foreach (PackageVulnerabilityInfo knownVulnerability in knownVulnerabilitiesForPackage)
                    {
                        if ((int)knownVulnerability.Severity < (int)minSeverity && knownVulnerability.Severity != PackageVulnerabilitySeverity.Unknown)
                        {
                            continue;
                        }

                        result ??= new();

                        if (!result.TryGetValue(packageIdentity, out PackageAuditInfo? auditInfo))
                        {
                            auditInfo = new(packageIdentity);
                            result.Add(packageIdentity, auditInfo);
                        }

                        auditInfo.Vulnerabilities.Add(knownVulnerability);
                    }
                }
            }
            return result;
        }

        private class PackageAuditInfo
        {
            public PackageIdentity Identity { get; }
            public List<PackageVulnerabilityInfo> Vulnerabilities { get; }

            public PackageAuditInfo(PackageIdentity identity)
            {
                Identity = identity;
                Vulnerabilities = new();
            }
        }
    }
}
