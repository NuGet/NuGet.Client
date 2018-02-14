// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class CertificateTrustEntry : ITrustEntry, IEquatable<CertificateTrustEntry>
    {
        /// <summary>
        /// Certificate fingerprint.
        /// </summary>
        public string Fingerprint { get; }

        /// <summary>
        /// Certificate subject name.
        /// </summary>
        public string SubjectName { get; }

        /// <summary>
        /// Hash algorithm used to generate the certificate fingerprint.
        /// </summary>
        public HashAlgorithmName FingerprintAlgorithm { get; }

        /// <summary>
        /// The priority of this certificate entry in the nuget.config hierarchy. Same as SettingValue.Priority.
        /// Null if this entry is not read from a config file.
        /// </summary>
        public int? Priority { get; }

        public CertificateTrustEntry(string fingerprint, string subjectName, HashAlgorithmName algorithm)
            : this(fingerprint, subjectName, algorithm, priority: null)
        {
        }

        public CertificateTrustEntry(string fingerprint, string subjectName, HashAlgorithmName algorithm, int? priority)
        {
            if (string.IsNullOrEmpty(fingerprint))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(fingerprint));
            }

            if (string.IsNullOrEmpty(subjectName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(subjectName));
            }

            Fingerprint = fingerprint;
            SubjectName = subjectName;
            FingerprintAlgorithm = algorithm;
            Priority = priority;
        }

        public bool Equals(CertificateTrustEntry other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // If two fingerperints are same, then the entries represent the same certificate
            return string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            // certificate fingerprint is a good check for equality
            hashCode.AddObject(Fingerprint);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object other)
        {
            return Equals(other as CertificateTrustEntry);
        }

        internal CertificateTrustEntry Clone()
        {
            return new CertificateTrustEntry(Fingerprint, SubjectName, FingerprintAlgorithm, Priority);
        }
    }
}
