// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class CertificateTrustEntry : IEquatable<CertificateTrustEntry>
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

        public CertificateTrustEntry(string fingerprint, string subjectName, HashAlgorithmName algorithm)
        {
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            SubjectName = subjectName ?? throw new ArgumentNullException(nameof(subjectName));
            FingerprintAlgorithm = algorithm;
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

            return FingerprintAlgorithm == other.FingerprintAlgorithm &&
                string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal) &&
                string.Equals(SubjectName, other.SubjectName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            // certificate fingerprint is a good quick check for inequality
            hashCode.AddObject(Fingerprint);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object other)
        {
            return Equals(other as CertificateTrustEntry);
        }
    }
}
