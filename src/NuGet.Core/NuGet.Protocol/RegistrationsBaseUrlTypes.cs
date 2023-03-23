// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// This represents three distinct registration hives available for various client versions.
    /// </summary>
    public static class RegistrationsBaseUrlTypes
    {
        private const string TypeName = "RegistrationsBaseUrl";

        /// <summary>
        /// These registrations are not compressed (meaning they use an implied Content-Encoding: identity).
        /// SemVer 2.0.0 packages are excluded from this hive.
        /// </summary>
        public static readonly string RegistrationsBaseUrl = TypeName;

        /// <summary>
        /// An alias for RegistrationsBaseUrl
        /// </summary>
        public static readonly string RegistrationsBaseUrlVersion300beta = $"{TypeName}{ClientVersions.Version300beta}";

        /// <summary>
        /// An alias for RegistrationsBaseUrl
        /// </summary>
        public static readonly string RegistrationsBaseUrlVersion300rc= $"{TypeName}{ClientVersions.Version300rc}";

        /// <summary>
        /// These registrations are compressed using Content-Encoding: gzip.
        /// SemVer 2.0.0 packages are excluded from this hive.
        /// </summary>
        public static readonly string RegistrationsBaseUrlVersion340 = $"{TypeName}{ClientVersions.Version340}";

        /// <summary>
        /// These registrations are compressed using Content-Encoding: gzip.
        /// SemVer 2.0.0 packages are included in this hive.
        /// </summary>
        public static readonly string RegistrationsBaseUrlVersion360 = $"{TypeName}{ClientVersions.Version360}";

        /// <summary>
        /// An alias for RegistrationsBaseUrlVersion360
        /// </summary>
        public static readonly string RegistrationsBaseUrlVersioned = $"{TypeName}{ClientVersions.Versioned}";

        /// <summary>
        /// All of the possible RegistrationsBaseUrl values including aliases
        /// </summary>
        public static readonly string[] RegistrationsBaseUrls =
        {
            RegistrationsBaseUrl,
            RegistrationsBaseUrlVersion300beta,
            RegistrationsBaseUrlVersion300rc,
            RegistrationsBaseUrlVersion340,
            RegistrationsBaseUrlVersion360,
            RegistrationsBaseUrlVersioned
        };

    }
}
