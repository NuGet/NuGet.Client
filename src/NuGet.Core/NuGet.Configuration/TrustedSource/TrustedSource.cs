// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class TrustedSource : IEquatable<TrustedSource>
    {
        /// <summary>
        /// Name of the associated package source.
        /// </summary>
        public string SourceName { get; }

        /// <summary>
        /// Service index of the source.
        /// </summary>
        public ServiceIndexTrustEntry ServiceIndex { get; set; }

        /// <summary>
        /// List of trusted certificates.
        /// </summary>
        public ISet<CertificateTrustEntry> Certificates { get; }

        public TrustedSource(string source)
        {
            SourceName = source ?? throw new ArgumentNullException(nameof(source));
            Certificates = new HashSet<CertificateTrustEntry>();
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(SourceName);

            return combiner.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return Equals(other as TrustedSource);
        }

        public bool Equals(TrustedSource other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(SourceName, other.SourceName, StringComparison.CurrentCultureIgnoreCase) &&
                EqualityUtility.EqualsWithNullCheck(ServiceIndex, other.ServiceIndex) &&
                EqualityUtility.SetEqualsWithNullCheck(Certificates, other.Certificates);
        }

        internal TrustedSource Clone()
        {
            var cloned = new TrustedSource(SourceName)
            {
                ServiceIndex = ServiceIndex
            };

            foreach (var entry in Certificates)
            {
                cloned.Certificates.Add(entry.Clone());
            }

            return cloned;
        }
    }
}
