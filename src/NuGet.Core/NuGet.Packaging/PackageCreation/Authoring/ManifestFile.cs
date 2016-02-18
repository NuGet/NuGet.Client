#if !DNXCORE50
using NuGet.Packaging.PackageCreation.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
#endif

namespace NuGet.Packaging
{
    public class ManifestFile
#if !DNXCORE50
        : IValidatableObject
#endif
    {
#if !DNXCORE50
        private static readonly char[] _invalidTargetChars = Path.GetInvalidFileNameChars().Except(new[] { '\\', '/', '.' }).ToArray();
        private static readonly char[] _invalidSourceCharacters = Path.GetInvalidPathChars();
#endif

        public string Source { get; set; }

        public string Target { get; set; }

        public string Exclude { get; set; }

#if !DNXCORE50
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!String.IsNullOrEmpty(Source) && Source.IndexOfAny(_invalidSourceCharacters) != -1)
            {
                yield return new ValidationResult(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_SourceContainsInvalidCharacters, Source));
            }

            if (!String.IsNullOrEmpty(Target) && Target.IndexOfAny(_invalidTargetChars) != -1)
            {
                yield return new ValidationResult(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_TargetContainsInvalidCharacters, Target));
            }

            if (!String.IsNullOrEmpty(Exclude) && Exclude.IndexOfAny(_invalidSourceCharacters) != -1)
            {
                yield return new ValidationResult(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_ExcludeContainsInvalidCharacters, Exclude));
            }
        }
#endif
    }
}