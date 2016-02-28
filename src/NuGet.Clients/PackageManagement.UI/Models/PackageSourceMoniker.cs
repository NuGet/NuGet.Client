using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal class PackageSourceMoniker : IEquatable<PackageSourceMoniker>
    {
        public SourceRepository[] SourceRepositories { get; private set; }

        public IEnumerable<string> PackageSources => SourceRepositories.Select(s => s.PackageSource.Name);

        public string SourceName { get; private set; }

        public PackageSourceMoniker(string sourceName, IEnumerable<SourceRepository> sourceRepositories)
        {
            SourceName = sourceName;

            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            if (!sourceRepositories.Any())
            {
                throw new ArgumentException("List of sources cannot be empty", nameof(sourceRepositories));
            }
            SourceRepositories = sourceRepositories.ToArray();
        }

        public override string ToString() => $"{SourceName}: [{string.Join("; ", PackageSources)}]";

        public string GetTooltip()
        {
            return SourceRepositories.Count() == 1
                ? GetTooltip(SourceRepositories.First().PackageSource)
                : string.Join("; ", PackageSources);
        }

        private static string GetTooltip(Configuration.PackageSource packageSource)
        {
            return string.IsNullOrEmpty(packageSource.Description)
                ? $"{packageSource.Name} - {packageSource.Source}"
                : $"{packageSource.Name} - {packageSource.Description} - {packageSource.Source}";
        }

        public bool Equals(PackageSourceMoniker other) => StringComparer.OrdinalIgnoreCase.Equals(this.ToString(), other.ToString());

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
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
            {
                return false;
            }

            return x.Equals(y);
        }

        public static bool operator !=(PackageSourceMoniker x, PackageSourceMoniker y) => !(x == y);

        public static PackageSourceMoniker FromSourceRepository(SourceRepository sourceRepository)
        {
            return new PackageSourceMoniker(sourceRepository.PackageSource.Name, new[] { sourceRepository });
        }

        public static PackageSourceMoniker Aggregated(IEnumerable<SourceRepository> sourceRepositories)
        {
            return new PackageSourceMoniker(Resources.AggregateSourceName, sourceRepositories);
        }

        public static List<PackageSourceMoniker> PopulateList(ISourceRepositoryProvider sourceRepositoryProvider)
        {
            var enabledSources = sourceRepositoryProvider
                .GetRepositories()
                .Where(repo => repo.PackageSource.IsEnabled)
                .ToArray();

            var descriptors = new List<PackageSourceMoniker>();

            if (enabledSources.Length > 1)
            {
                descriptors.Add(Aggregated(enabledSources));
            }

            descriptors.AddRange(
                enabledSources.Select(FromSourceRepository));

            return descriptors;
        }
    }
}
