// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class ServiceIndexTrustEntry : ITrustEntry, IEquatable<ServiceIndexTrustEntry>
    {
        /// <summary>
        /// Service index uri.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The priority of this entry in the nuget.config hierarchy. Same as SettingValue.Priority.
        /// Should be used only if the ServiceIndexTrustEntry is read from a config file.
        /// </summary>
        public int Priority { get; }

        public ServiceIndexTrustEntry(string serviceIndex)
            : this(serviceIndex, priority: 0)
        {
        }

        public ServiceIndexTrustEntry(string serviceIndex, int priority)
        {
            Value = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));
            Priority = priority;
        }

        public bool Equals(ServiceIndexTrustEntry other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(Value);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object other)
        {
            return Equals(other as ServiceIndexTrustEntry);
        }

        internal ServiceIndexTrustEntry Clone()
        {
            return new ServiceIndexTrustEntry(Value, Priority);
        }
    }
}
