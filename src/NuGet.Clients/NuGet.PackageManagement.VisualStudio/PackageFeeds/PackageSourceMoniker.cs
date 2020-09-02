// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using NuGet.Configuration;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class PackageSourceMoniker : IEquatable<PackageSourceMoniker>
    {
        public PackageSourceMoniker(string sourceName, IEnumerable<PackageSource> packageSources)
        {
            SourceName = sourceName;

            if (packageSources == null)
            {
                throw new ArgumentNullException(nameof(packageSources));
            }
            if (!packageSources.Any())
            {
                throw new ArgumentException("List of sources cannot be empty", nameof(packageSources));
            }
            PackageSources = packageSources.ToArray();
        }

        public IReadOnlyCollection<PackageSource> PackageSources { get; private set; }

        public IEnumerable<string> PackageSourceNames => PackageSources.Select(s => s.Name);

        public string SourceName { get; private set; }

        public bool IsAggregateSource => PackageSources.Count > 1;

        public override string ToString() => $"{SourceName}: [{string.Join("; ", PackageSourceNames)}]";

        public string GetTooltip()
        {
            return PackageSources.Count() == 1
                ? GetTooltip(PackageSources.First())
                : string.Join("; ", PackageSourceNames);
        }

        private static string GetTooltip(PackageSource packageSource)
        {
            return string.IsNullOrEmpty(packageSource.Description)
                ? $"{packageSource.Name} - {packageSource.Source}"
                : $"{packageSource.Name} - {packageSource.Description} - {packageSource.Source}";
        }

        public bool Equals(PackageSourceMoniker other) => StringComparer.OrdinalIgnoreCase.Equals(ToString(), other.ToString());

        public override bool Equals(object obj)
        {
            return obj is PackageSourceMoniker && this == (PackageSourceMoniker)obj;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public static bool operator ==(PackageSourceMoniker x, PackageSourceMoniker y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Equals(y);
        }

        public static bool operator !=(PackageSourceMoniker x, PackageSourceMoniker y) => !(x == y);

        public static async ValueTask<IReadOnlyCollection<PackageSourceMoniker>> PopulateListAsync(CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetSourcesService = await remoteBroker.GetProxyAsync<INuGetSourcesService>(NuGetServices.SourceProviderService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetSourcesService);
                IReadOnlyList<PackageSource> packageSources = await nugetSourcesService.GetPackageSourcesAsync(cancellationToken);

                var packageSourceMonikers = new List<PackageSourceMoniker>();
                if (packageSources.Count > 1) // If more than 1, add 'All'
                {
                    packageSourceMonikers.Add(new PackageSourceMoniker(Strings.AggregateSourceName, packageSources));
                }

                packageSourceMonikers.AddRange(packageSources.Select(s => new PackageSourceMoniker(s.Name, new[] { s })));

                return packageSourceMonikers;
            }
        }
    }
}
