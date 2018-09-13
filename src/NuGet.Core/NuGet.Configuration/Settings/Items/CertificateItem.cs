// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class CertificateItem : SettingItem, IEquatable<CertificateItem>
    {
        public string Fingerprint => Attributes[ConfigurationConstants.Fingerprint];

        public HashAlgorithmName HashAlgorithm => CryptoHashUtility.GetHashAlgorithmName(Attributes[ConfigurationConstants.HashAlgorithm]);

        public bool AllowUntrustedRoot
        {
            get
            {
                if (bool.TryParse(Attributes[ConfigurationConstants.AllowUntrustedRoot], out var parsedAttribute))
                {
                    return parsedAttribute;
                }

                return false;
            }
        }

        protected override HashSet<string> RequiredAttributes => new HashSet<string>() { ConfigurationConstants.Fingerprint, ConfigurationConstants.HashAlgorithm, ConfigurationConstants.AllowUntrustedRoot };

        public CertificateItem(string fingerprint, HashAlgorithmName hashAlgorithm, bool allowUntrustedRoot = false)
            : base()
        {
            if (string.IsNullOrEmpty(fingerprint))
            {
                throw new ArgumentNullException(nameof(fingerprint));
            }

            if (hashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new ArgumentException(Resources.UnknownHashAlgorithmNotSupported);
            }

            AddAttribute(ConfigurationConstants.Fingerprint, fingerprint);
            AddAttribute(ConfigurationConstants.HashAlgorithm, hashAlgorithm.ToString());
            AddAttribute(ConfigurationConstants.AllowUntrustedRoot, $"{allowUntrustedRoot}");
        }

        internal CertificateItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            if (HashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile,
                    string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedHashAlgorithm, Attributes[ConfigurationConstants.HashAlgorithm]),
                    origin.ConfigFilePath));
            }
        }

        internal override SettingBase Clone()
        {
            var newItem = new CertificateItem(Fingerprint, HashAlgorithm, AllowUntrustedRoot);

            if (Origin != null)
            {
                newItem.SetOrigin(Origin);
            }

            return newItem;
        }

        public bool Equals(CertificateItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public bool DeepEquals(CertificateItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.Attributes.Count == Attributes.Count)
            {
                return Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode() => Fingerprint.GetHashCode();
        public override bool Equals(SettingBase other) => Equals(other as CertificateItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as CertificateItem);
        public override bool Equals(object other) => Equals(other as CertificateItem);
    }
}
