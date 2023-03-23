// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// This represents three distinct registration hives available for various client versions.
    /// </summary>
    public static class RegistrationsBaseUrlTypes
    {
        //private const string TypeName = "RegistrationsBaseUrl";

        /// <summary>
        /// These registrations are not compressed (meaning they use an implied Content-Encoding: identity).
        /// SemVer 2.0.0 packages are excluded from this hive.
        /// </summary>
        public static readonly string[] RegistrationsBaseUrl;

        /// <summary>
        /// These registrations are compressed using Content-Encoding: gzip.
        /// SemVer 2.0.0 packages are excluded from this hive.
        /// </summary>
        public static readonly string[] RegistrationsBaseUrlVersion340;

        /// <summary>
        /// These registrations are compressed using Content-Encoding: gzip.
        /// SemVer 2.0.0 packages are included in this hive.
        /// </summary>
        public static readonly string[] RegistrationsBaseUrlVersion360;
    }
}
