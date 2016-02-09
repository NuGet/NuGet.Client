using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal class SourceDescriptorItem : IEquatable<SourceDescriptorItem>
    {
        public SourceRepository[] SourceRepositories { get; private set; }

        public IEnumerable<string> PackageSources => SourceRepositories.Select(s => s.PackageSource.Name);

        public string SourceName { get; private set; }

        public SourceDescriptorItem(SourceRepository sourceRepository)
        {
            if (sourceRepository == null)
            {
                throw new ArgumentNullException(nameof(sourceRepository));
            }
            SourceName = sourceRepository.PackageSource.Name;
            SourceRepositories = new[] { sourceRepository };
        }

        public SourceDescriptorItem(string sourceName, IEnumerable<SourceRepository> sourceRepositories)
        {
            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            if (!sourceRepositories.Any())
            {
                throw new ArgumentException("List of sources cannot be empty", nameof(sourceRepositories));
            }
            SourceName = sourceName;
            SourceRepositories = sourceRepositories.ToArray();
        }

        public override string ToString()=> $"{SourceName}: [{string.Join("; ", PackageSources)}]";

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

        public bool Equals(SourceDescriptorItem other) => StringComparer.OrdinalIgnoreCase.Equals(this.ToString(), other.ToString());

        public override bool Equals(object obj)
        {
            return obj is SourceDescriptorItem && this == (SourceDescriptorItem)obj;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public static bool operator ==(SourceDescriptorItem x, SourceDescriptorItem y)
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

        public static bool operator !=(SourceDescriptorItem x, SourceDescriptorItem y) => !(x == y);
    }
}
