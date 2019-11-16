using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class CertificateSearchItem : SettingItem
    {
        public static int MergeHash(params int[] hashes)
        {
            var result = 0;
            foreach (var hash in hashes)
            {
                result = (result * 397) ^ hash;
            }

            return result;
        }

        protected CertificateSearchItem(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(name));
            }

            AddAttribute(ConfigurationConstants.NameAttribute, name);
        }

        internal CertificateSearchItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        public string Name => Attributes[ConfigurationConstants.NameAttribute];
        public abstract ClientCertificatesSourceType SourceType { get; }

        protected override bool CanHaveChildren => true;

        protected override IReadOnlyCollection<string> RequiredAttributes { get; } = new HashSet<string> { ConfigurationConstants.NameAttribute };

        public override bool Equals(object other)
        {
            var item = other as CertificateSearchItem;

            if (item == null)
            {
                return false;
            }

            if (ReferenceEquals(this, item))
            {
                return true;
            }

            if (!string.Equals(ElementName, item.ElementName, StringComparison.Ordinal)) return false;
            if (!string.Equals(Name, item.Name, StringComparison.Ordinal)) return false;

            return true;
        }

        public override int GetHashCode()
        {
            return MergeHash(ElementName.GetHashCode(), Name.GetHashCode());
        }

        public abstract X509Certificate Search();

        protected void SetName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.PropertyCannotBeNullOrEmpty, nameof(Name)));
            }

            UpdateAttribute(ConfigurationConstants.NameAttribute, value);
        }

        public enum ClientCertificatesSourceType
        {
            File,
            PEM,
            Storage
        }
    }
}
