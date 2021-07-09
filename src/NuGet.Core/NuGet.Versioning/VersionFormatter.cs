// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text;

namespace NuGet.Versioning
{
    /// <summary>
    /// Custom formatter for NuGet versions.
    /// </summary>
    public class VersionFormatter : IFormatProvider, ICustomFormatter
    {
        /// <summary>
        /// A static instance of the VersionFormatter class.
        /// </summary>
        public static readonly VersionFormatter Instance = new();

        /// <summary>
        /// Format a version string.
        /// </summary>
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            if (string.IsNullOrEmpty(format) || arg is not SemanticVersion version)
            {
                return null;
            }

            StringBuilder builder = StringBuilderPool.Shared.Rent(256);

            foreach (char c in format)
            {
                Format(builder, c, version);
            }

            string formattedString = builder.ToString();

            StringBuilderPool.Shared.Return(builder);

            return formattedString;

        }

        /// <summary>
        /// Get version format type.
        /// </summary>
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)
                || formatType == typeof(NuGetVersion)
                || formatType == typeof(SemanticVersion))
            {
                return this;
            }

            return null;
        }

        private static void Format(StringBuilder builder, char c, SemanticVersion version)
        {
            switch (c)
            {
                case 'N':
                    AppendNormalized(builder, version);
                    return;
                case 'V':
                    AppendVersion(builder, version);
                    return;
                case 'F':
                    AppendFull(builder, version);
                    return;
                case 'R':
                    builder.Append(version.Release);
                    return;
                case 'M':
                    builder.Append(version.Metadata);
                    return;
                case 'x':
                    builder.Append(version.Major);
                    return;
                case 'y':
                    builder.Append(version.Minor);
                    return;
                case 'z':
                    builder.Append(version.Patch);
                    return;
                case 'r':
                    builder.Append(version is NuGetVersion nuGetVersion && nuGetVersion.IsLegacyVersion ? nuGetVersion.Version.Revision : 0);
                    return;

                default:
                    builder.Append(c);
                    return;
            }
        }

        /// <summary>
        /// Appends the full version string including metadata. This is primarily for display purposes.
        /// </summary>
        private static void AppendFull(StringBuilder builder, SemanticVersion version)
        {
            AppendNormalized(builder, version);

            if (version.HasMetadata)
            {
                builder.Append('+');
                builder.Append(version.Metadata);
            }
        }

        /// <summary>
        /// Appends a normalized version string. This string is unique for each version 'identity' 
        /// and does not include leading zeros or metadata.
        /// </summary>
        private static void AppendNormalized(StringBuilder builder, SemanticVersion version)
        {
            AppendVersion(builder, version);

            if (version.IsPrerelease)
            {
                builder.Append('-');
                builder.Append(version.Release);
            }
        }

        private static void AppendVersion(StringBuilder builder, SemanticVersion version)
        {
            builder.Append(version.Major);
            builder.Append('.');
            builder.Append(version.Minor);
            builder.Append('.');
            builder.Append(version.Patch);

            if (version is NuGetVersion nuGetVersion && nuGetVersion.IsLegacyVersion)
            {
                builder.Append('.');
                builder.Append(nuGetVersion.Version.Revision);
            }
        }
    }
}
