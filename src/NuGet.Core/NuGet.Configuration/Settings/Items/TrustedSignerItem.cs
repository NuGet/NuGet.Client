// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class TrustedSignerItem : SettingItem
    {
        public string Name => Attributes[ConfigurationConstants.NameAttribute];

        protected override bool CanHaveChildren => true;

        private IList<CertificateItem> _certificates { get; }

        public IReadOnlyList<CertificateItem> Certificates => _certificates.ToList().AsReadOnly();

        protected TrustedSignerItem(string name, IEnumerable<CertificateItem> certificates)
            : base()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            AddAttribute(ConfigurationConstants.NameAttribute, name);

            _certificates = new List<CertificateItem>();

            if (certificates == null || !certificates.Any())
            {
                throw new ArgumentException(Resources.TrustedSignerMustHaveCertificates);
            }

            foreach (var certificate in certificates)
            {
                _certificates.Add(certificate);
            }
        }

        internal TrustedSignerItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            var parsedCertificates = element.Elements().Select(e => SettingFactory.Parse(e, origin)).OfType<CertificateItem>().ToList();

            if (parsedCertificates.Count() < 1)
            {
                throw new NuGetConfigurationException(
                    string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, Resources.TrustedSignerMustHaveCertificates, origin.ConfigFilePath));
            }

            _certificates = parsedCertificates;
        }

        internal override void SetOrigin(SettingsFile origin)
        {
            base.SetOrigin(origin);

            foreach (var certificate in Certificates)
            {
                certificate.SetOrigin(origin);
            }
        }

        internal override void RemoveFromSettings()
        {
            base.RemoveFromSettings();

            foreach (var certificate in Certificates)
            {
                certificate.RemoveFromSettings();
            }
        }
    }
}
