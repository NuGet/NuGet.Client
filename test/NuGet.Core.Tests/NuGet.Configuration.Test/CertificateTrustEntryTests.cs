// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using NuGet.Common;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class CertificateTrustEntryTests
    {
        [Fact]
        public void EqualsReturnsTrueForSameObject()
        {
            var fingerprint = "hash";
            var subjectName = "subjectname";
            var algorithm = HashAlgorithmName.SHA256;

            var entry = new CertificateTrustEntry(fingerprint, subjectName, algorithm);

            entry.Equals(entry).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsTrueForIndenticalObjects()
        {
            var fingerprint = "hash";
            var subjectName = "subjectname";
            var algorithm = HashAlgorithmName.SHA256;

            var entry1 = new CertificateTrustEntry(fingerprint, subjectName, algorithm);
            var entry2 = new CertificateTrustEntry(fingerprint, subjectName, algorithm);

            entry1.Equals(entry2).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsTrueForEquivalentObjects()
        {
            var fingerprint = "hash";
            var subjectName1 = "subjectname";
            var subjectName2 = "subjectName";
            var algorithm = HashAlgorithmName.SHA256;

            var entry1 = new CertificateTrustEntry(fingerprint, subjectName1, algorithm);
            var entry2 = new CertificateTrustEntry(fingerprint, subjectName2, algorithm);

            entry1.Equals(entry2).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsFalseForNullOtherObject()
        {
            var fingerprint = "hash";
            var subjectName = "subjectname";
            var algorithm = HashAlgorithmName.SHA256;

            var entry = new CertificateTrustEntry(fingerprint, subjectName, algorithm);

            entry.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void EqualsReturnsFalseForDifferentFingerprintCase()
        {
            var fingerprint1 = "hash";
            var fingerprint2 = "HASH";
            var subjectName = "subjectname";
            var algorithm = HashAlgorithmName.SHA256;

            var entry1 = new CertificateTrustEntry(fingerprint1, subjectName, algorithm);
            var entry2 = new CertificateTrustEntry(fingerprint2, subjectName, algorithm);

            entry1.Equals(entry2).Should().BeFalse();
        }

        [Fact]
        public void EqualsReturnsFalseForDifferentFingerprints()
        {
            var fingerprint1 = "hash";
            var fingerprint2 = "other hash";
            var subjectName = "subjectname";
            var algorithm = HashAlgorithmName.SHA256;

            var entry1 = new CertificateTrustEntry(fingerprint1, subjectName, algorithm);
            var entry2 = new CertificateTrustEntry(fingerprint2, subjectName, algorithm);

            entry1.Equals(entry2).Should().BeFalse();
        }
    }
}
