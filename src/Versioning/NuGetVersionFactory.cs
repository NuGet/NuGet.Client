using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NuGet.Versioning
{
    public partial class NuGetVersion
    {
        /// <summary>
        /// Creates a NuGetVersion from a string representing the semantic version.
        /// </summary>
        public static new NuGetVersion Parse(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, value), "value");
            }

            NuGetVersion ver = null;
            if (!TryParse(value, out ver))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Invalidvalue, value), "value");
            }

            return ver;
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static bool TryParse(string value, out NuGetVersion version)
        {
            if (!String.IsNullOrEmpty(value))
            {
                var match = Constants.SemanticVersionRegex.Match(value.Trim());

                Version versionValue;
                if (match.Success && Version.TryParse(match.Groups["Version"].Value, out versionValue))
                {
                    Version ver = NormalizeVersionValue(versionValue);

                    version = new NuGetVersion(version: ver,
                                                releaseLabels: ParseReleaseLabels(match.Groups["Release"].Value.TrimStart('-')),
                                                metadata: match.Groups["Metadata"].Value.TrimStart('+'),
                                                originalVersion: value.Replace(" ", ""));
                    return true;
                }
            }

            version = null;
            return false;
        }

        /// <summary>
        /// Parses a version string using strict SemVer rules.
        /// </summary>
        public static bool TryParseStrict(string value, out NuGetVersion version)
        {
            version = null;

            if (String.IsNullOrEmpty(value))
            {
                return false;
            }

            var match = Constants.SemanticVersionStrictRegex.Match(value.Trim());

            Version versionValue;
            if (!match.Success || !Version.TryParse(match.Groups["Version"].Value, out versionValue))
            {
                return false;
            }

            Version ver = NormalizeVersionValue(versionValue);

            version = new NuGetVersion(ver, ParseReleaseLabels(match.Groups["Release"].Value.TrimStart('-')), match.Groups["Metadata"].Value.TrimStart('+'), null);

            return true;
        }

        private static Version NormalizeVersionValue(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }

        /// <summary>
        /// Creates a legacy version string using System.Version
        /// </summary>
        private static string GetLegacyString(Version version, IEnumerable<string> releaseLabels, string metadata)
        {
            StringBuilder sb = new StringBuilder(version.ToString());

            if (releaseLabels != null)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "-{0}", String.Join(".", releaseLabels));
            }

            if (!String.IsNullOrEmpty(metadata))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "+{0}", metadata);
            }

            return sb.ToString();
        }

        private static IEnumerable<string> ParseReleaseLabels(string releaseLabels)
        {
            if (!String.IsNullOrEmpty(releaseLabels))
            {
                return releaseLabels.Split('.');
            }

            return null;
        }
    }
}
