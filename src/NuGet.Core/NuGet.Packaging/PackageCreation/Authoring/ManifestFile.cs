using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public class ManifestFile
    {
        private static readonly char[] _invalidTargetChars = Path.GetInvalidFileNameChars().Except(new[] { '\\', '/', '.' }).ToArray();
        private static readonly char[] _invalidSourceCharacters = Path.GetInvalidPathChars();

        public string Source { get; set; }

        public string Target { get; set; }

        public string Exclude { get; set; }

        public IEnumerable<string> Validate()
        {
            if (String.IsNullOrEmpty(Source))
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Source");
            }
            else if (Source.IndexOfAny(_invalidSourceCharacters) != -1)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_SourceContainsInvalidCharacters, Source);
            }

            if (!String.IsNullOrEmpty(Target) && Target.IndexOfAny(_invalidTargetChars) != -1)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_TargetContainsInvalidCharacters, Target);
            }

            if (!String.IsNullOrEmpty(Exclude) && Exclude.IndexOfAny(_invalidSourceCharacters) != -1)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_ExcludeContainsInvalidCharacters, Exclude);
            }
        }
    }
}