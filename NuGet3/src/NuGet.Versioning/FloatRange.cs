// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Versioning
{
    /// <summary>
    /// The floating subset of a version range.
    /// </summary>
    public class FloatRange : IEquatable<FloatRange>
    {
        private readonly NuGetVersion _minVersion;
        private readonly NuGetVersionFloatBehavior _floatBehavior;
        private readonly string _releasePrefix;

        public FloatRange(NuGetVersionFloatBehavior floatBehavior)
            : this(floatBehavior, null, null)
        {
        }

        public FloatRange(NuGetVersionFloatBehavior floatBehavior, NuGetVersion minVersion)
            : this(floatBehavior, minVersion, null)
        {
        }

        /// <summary>
        /// FloatRange
        /// </summary>
        /// <param name="floatBehavior">Section to float</param>
        /// <param name="minVersion">Min version of the range</param>
        /// <param name="originalReleasePrefix">The original release label. Invalid labels are allowed here.</param>
        public FloatRange(NuGetVersionFloatBehavior floatBehavior, NuGetVersion minVersion, string releasePrefix)
        {
            _floatBehavior = floatBehavior;
            _minVersion = minVersion;
            _releasePrefix = releasePrefix;

            if (_releasePrefix == null
                && minVersion != null
                && minVersion.IsPrerelease)
            {
                // use the actual label if one was not given
                _releasePrefix = minVersion.Release;
            }
        }

        /// <summary>
        /// True if a min range exists.
        /// </summary>
        public bool HasMinVersion
        {
            get { return _minVersion != null; }
        }

        /// <summary>
        /// The minimum version of the float range. This is null for cases such as *
        /// </summary>
        public NuGetVersion MinVersion
        {
            get { return _minVersion; }
        }

        /// <summary>
        /// Defined float behavior
        /// </summary>
        public NuGetVersionFloatBehavior FloatBehavior
        {
            get { return _floatBehavior; }
        }

        /// <summary>
        /// True if the given version falls into the floating range.
        /// </summary>
        public bool Satisfies(NuGetVersion version)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            if (_floatBehavior == NuGetVersionFloatBehavior.AbsoluteLatest)
            {
                return true;
            }

            if (_floatBehavior == NuGetVersionFloatBehavior.Major
                && !version.IsPrerelease)
            {
                return true;
            }

            if (_minVersion != null)
            {
                // everything beyond this point requires a version
                if (_floatBehavior == NuGetVersionFloatBehavior.Prerelease)
                {
                    // allow the stable version to match
                    return VersionComparer.Version.Equals(_minVersion, version)
                           && ((version.IsPrerelease && version.Release.StartsWith(_releasePrefix, StringComparison.OrdinalIgnoreCase))
                               || !version.IsPrerelease);
                }
                else if (_floatBehavior == NuGetVersionFloatBehavior.Revision)
                {
                    return _minVersion.Major == version.Major
                           && _minVersion.Minor == version.Minor
                           && _minVersion.Patch == version.Patch
                           && !version.IsPrerelease;
                }
                else if (_floatBehavior == NuGetVersionFloatBehavior.Patch)
                {
                    return _minVersion.Major == version.Major
                           && _minVersion.Minor == version.Minor
                           && !version.IsPrerelease;
                }
                else if (_floatBehavior == NuGetVersionFloatBehavior.Minor)
                {
                    return _minVersion.Major == version.Major
                           && !version.IsPrerelease;
                }
            }

            return false;
        }

        /// <summary>
        /// Parse a floating version into a FloatRange
        /// </summary>
        public static FloatRange Parse(string versionString)
        {
            FloatRange range = null;

            TryParse(versionString, out range);

            return range;
        }

        /// <summary>
        /// Parse a floating version into a FloatRange
        /// </summary>
        public static bool TryParse(string versionString, out FloatRange range)
        {
            range = null;

            if (versionString != null)
            {
                var starPos = versionString.IndexOf('*');

                var actualVersion = versionString;
                string releasePrefix = null;

                if (versionString.Length == 1
                    && starPos == 0)
                {
                    range = new FloatRange(NuGetVersionFloatBehavior.Major, new NuGetVersion(new Version(0, 0)));
                }
                else if (starPos == versionString.Length - 1)
                {
                    var behavior = NuGetVersionFloatBehavior.None;

                    actualVersion = versionString.Substring(0, versionString.Length - 1);

                    if (versionString.IndexOf('-') == -1)
                    {
                        // replace the * with a 0
                        actualVersion += "0";

                        var versionParts = actualVersion.Split('.').Length;

                        if (versionParts == 2)
                        {
                            behavior = NuGetVersionFloatBehavior.Minor;
                        }
                        else if (versionParts == 3)
                        {
                            behavior = NuGetVersionFloatBehavior.Patch;
                        }
                        else if (versionParts == 4)
                        {
                            behavior = NuGetVersionFloatBehavior.Revision;
                        }
                    }
                    else
                    {
                        behavior = NuGetVersionFloatBehavior.Prerelease;

                        // check for a prefix
                        if (versionString.IndexOf('-') == versionString.LastIndexOf('-'))
                        {
                            releasePrefix = actualVersion.Substring(versionString.LastIndexOf('-') + 1);

                            if (actualVersion.EndsWith("-"))
                            {
                                // remove the empty release label, the version will be release but
                                // the behavior will have to account for this
                                actualVersion += "-";
                            }
                            else if (actualVersion.EndsWith("."))
                            {
                                // ending with a . is not allowed
                                // TODO: solve this better
                                actualVersion += "0";
                            }
                        }
                    }

                    NuGetVersion version = null;
                    if (NuGetVersion.TryParse(actualVersion, out version))
                    {
                        // there is no float range for this version
                        range = new FloatRange(behavior, version, releasePrefix);
                    }
                }
                else
                {
                    // normal version parse
                    NuGetVersion version = null;
                    if (NuGetVersion.TryParse(versionString, out version))
                    {
                        // there is no float range for this version
                        range = new FloatRange(NuGetVersionFloatBehavior.None, version);
                    }
                }
            }

            return range != null;
        }

        /// <summary>
        /// Create a floating version string in the format: 1.0.0-alpha-*
        /// </summary>
        public override string ToString()
        {
            var result = string.Empty;
            switch (_floatBehavior)
            {
                case NuGetVersionFloatBehavior.None:
                    result = MinVersion.ToNormalizedString();
                    break;
                case NuGetVersionFloatBehavior.Prerelease:
                    result = String.Format(new VersionFormatter(), "{0:V}-{1}*", MinVersion, _releasePrefix);
                    break;
                case NuGetVersionFloatBehavior.Revision:
                    result = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.*", MinVersion.Major, MinVersion.Minor, MinVersion.Patch);
                    break;
                case NuGetVersionFloatBehavior.Patch:
                    result = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.*", MinVersion.Major, MinVersion.Minor);
                    break;
                case NuGetVersionFloatBehavior.Minor:
                    result = String.Format(CultureInfo.InvariantCulture, "{0}.*", MinVersion.Major);
                    break;
                case NuGetVersionFloatBehavior.Major:
                    result = "*";
                    break;
                case NuGetVersionFloatBehavior.AbsoluteLatest:
                    // TODO: how should this be denoted?
                    result = string.Empty;
                    break;
                default:
                    break;
            }

            return result;
        }

        public bool Equals(FloatRange other)
        {
            return FloatBehavior == other.FloatBehavior
                   && VersionComparer.Default.Equals(MinVersion, other.MinVersion);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(FloatBehavior);
            combiner.AddObject(MinVersion);

            return combiner.CombinedHash;
        }
    }
}
