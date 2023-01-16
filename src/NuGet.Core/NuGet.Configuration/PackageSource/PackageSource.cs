// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Configuration
{
    public class PackageSource : IEquatable<PackageSource>
    {
        /// <summary>
        /// The feed version for NuGet prior to v3.
        /// </summary>
        public const int DefaultProtocolVersion = 2;

        private int _hashCode;
        private string _source;
        private bool _isHttp;
        private bool _isHttps;
        private bool _isLocal;

        public string Name { get; }

        public string Source
        {
            get => _source;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(paramName: nameof(value));
                }

                _source = value;
                _hashCode = Name.ToUpperInvariant().GetHashCode() * 3137 + _source.ToUpperInvariant().GetHashCode();

                if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _isHttps = true;
                    _isHttp = true;
                    _isLocal = false;
                }
                else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    _isHttps = false;
                    _isHttp = true;
                    _isLocal = false;
                }
                else
                {
                    _isHttps = false;
                    _isHttp = false;
                    Uri? uri = TrySourceAsUri;
                    _isLocal = uri != null ? uri.IsFile : false;
                }
            }
        }

        /// <summary>
        /// Returns null if Source is an invalid URI
        /// </summary>
        public Uri? TrySourceAsUri => UriUtility.TryCreateSourceUri(Source, UriKind.Absolute);

        /// <summary>
        /// Throws if Source is an invalid URI
        /// </summary>
        public Uri SourceUri => UriUtility.CreateSourceUri(Source, UriKind.Absolute);

        /// <summary>
        /// This does not represent just the NuGet Official Feed alone
        /// It may also represent a Default Package Source set by Configuration Defaults
        /// </summary>
        public bool IsOfficial { get; set; }

        public bool IsMachineWide { get; set; }

        public bool IsEnabled { get; set; }

        public PackageSourceCredential? Credentials { get; set; }

        public string? Description { get; set; }

        public bool IsPersistable { get; }

        public int MaxHttpRequestsPerSource { get; set; }

        public IReadOnlyList<X509Certificate>? ClientCertificates { get; set; }

        /// <summary>
        /// Gets or sets the protocol version of the source. Defaults to 2.
        /// </summary>
        public int ProtocolVersion { get; set; } = DefaultProtocolVersion;

        /// <summary>
        /// Whether the source is using the HTTP protocol, including HTTPS.
        /// </summary>
        public bool IsHttp => _isHttp;

        /// <summary>
        /// Whether the source is using the HTTPS protocol.
        /// </summary>
        public bool IsHttps => _isHttps;

        /// <summary>
        /// True if the source path is file based. Unc shares are not included.
        /// </summary>
        public bool IsLocal => _isLocal;

        public PackageSource(string source)
            : this(source, source, isEnabled: true)
        {
        }

        public PackageSource(string source, string name)
            : this(source, name, isEnabled: true)
        {
        }

        public PackageSource(string source, string name, bool isEnabled)
            : this(source, name, isEnabled, isOfficial: false)
        {
        }

        public PackageSource(
            string source,
            string name,
            bool isEnabled,
            bool isOfficial,
            bool isPersistable = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Source = _source = source ?? throw new ArgumentNullException(nameof(source));
            IsEnabled = isEnabled;
            IsOfficial = isOfficial;
            IsPersistable = isPersistable;
        }

        public SourceItem AsSourceItem()
        {
            string? protocolVersion = null;
            if (ProtocolVersion != DefaultProtocolVersion)
            {
                protocolVersion = $"{ProtocolVersion}";
            }
            return new SourceItem(Name, Source, protocolVersion);
        }

        public bool Equals(PackageSource? other)
        {
            if (other == null)
            {
                return false;
            }

            return Name.Equals(other.Name, StringComparison.CurrentCultureIgnoreCase) &&
                   Source.Equals(other.Source, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            var source = obj as PackageSource;
            if (source != null)
            {
                return Equals(source);
            }
            return base.Equals(obj);
        }

        public override string ToString() => Name + " [" + Source + "]";

        public override int GetHashCode() => _hashCode;

        public PackageSource Clone()
        {
            return new PackageSource(Source, Name, IsEnabled, IsOfficial, IsPersistable)
            {
                Description = Description,
                Credentials = Credentials?.Clone(),
                ClientCertificates = ClientCertificates?.ToList(),
                IsMachineWide = IsMachineWide,
                ProtocolVersion = ProtocolVersion,
            };
        }
    }
}
