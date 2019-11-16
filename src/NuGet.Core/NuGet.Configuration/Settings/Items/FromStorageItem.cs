using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    /// <summary>
    ///     A FromStorageItem have 4 children:
    ///     - [Optional] StoreLocation (AddItem). StoreLocation.CurrentUser by default.
    ///     - [Optional] StoreName (AddItem). StoreName.My by default.
    ///     - [Optional] FindType (AddItem). X509FindType.FindByThumbprint by default.
    ///     - [Required] FindValue (AddItem)
    /// </summary>
    public sealed class FromStorageItem : CertificateSearchItem
    {
        private const X509FindType DefaultFindType = X509FindType.FindByThumbprint;
        private const StoreLocation DefaultStoreLocation = StoreLocation.CurrentUser;
        private const StoreName DefaultStoreName = StoreName.My;
        private readonly AddItem _findType;
        private readonly AddItem _findValue;
        private readonly AddItem _storeLocation;
        private readonly AddItem _storeName;

        public FromStorageItem(string name,
                               string findValue,
                               StoreLocation? storeLocation = null,
                               StoreName? storeName = null,
                               X509FindType? findType = null)
            : base(name)
        {
            ElementName = ConfigurationConstants.FromStorage;

            if (string.IsNullOrEmpty(findValue))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(findValue));
            }

            if (!storeLocation.HasValue) storeLocation = DefaultStoreLocation;
            if (!storeName.HasValue) storeName = DefaultStoreName;
            if (!findType.HasValue) findType = DefaultFindType;

            _storeLocation = new AddItem(ConfigurationConstants.StoreLocationToken, storeLocation.ToString());
            _storeName = new AddItem(ConfigurationConstants.StoreNameToken, storeName.ToString());
            _findType = new AddItem(ConfigurationConstants.FindTypeToken, findType.ToString());
            _findValue = new AddItem(ConfigurationConstants.FindValueToken, findValue);
        }

        internal FromStorageItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            ElementName = ConfigurationConstants.FromStorage;

            IEnumerable<AddItem> parsedItems = element.Elements()
                                                      .Select(e => SettingFactory.Parse(e, origin) as AddItem)
                                                      .Where(i => i != null);

            foreach (AddItem item in parsedItems)
            {
                if (string.Equals(item.Key, ConfigurationConstants.StoreLocationToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_storeLocation != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOneStoreLocation,
                                                                            origin.ConfigFilePath));
                    }

                    _storeLocation = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.StoreNameToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_storeName != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOneStoreName,
                                                                            origin.ConfigFilePath));
                    }

                    _storeName = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.FindTypeToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_findType != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOneFindType,
                                                                            origin.ConfigFilePath));
                    }

                    _findType = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.FindValueToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_findValue != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOneFindValue,
                                                                            origin.ConfigFilePath));
                    }

                    _findValue = item;
                }
            }

            if (_storeLocation == null)
            {
                _storeLocation = new AddItem(ConfigurationConstants.StoreLocationToken, DefaultStoreLocation.ToString());
            }

            if (_storeName == null)
            {
                _storeName = new AddItem(ConfigurationConstants.StoreNameToken, DefaultStoreName.ToString());
            }

            if (_findType == null)
            {
                _findType = new AddItem(ConfigurationConstants.FindTypeToken, DefaultFindType.ToString());
            }

            if (_findValue == null)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromStorageItemMustHaveFindValue,
                                                                    origin.ConfigFilePath));
            }

            ValidateItem();
        }

        public X509FindType FindType
        {
            get
            {
                if (Enum.TryParse(_findType.Value, true, out X509FindType result)) return result;
                return DefaultFindType;
            }
            set
            {
                var stringValue = value.ToString();
                _findType.Value = stringValue;
            }
        }

        public string FindValue
        {
            get => _findValue.Value;
            set => _findValue.Value = value;
        }

        public new string Name
        {
            get => base.Name;
            set => SetName(value);
        }

        public override ClientCertificatesSourceType SourceType => ClientCertificatesSourceType.Storage;

        public StoreLocation StoreLocation
        {
            get
            {
                if (Enum.TryParse(_storeLocation.Value, true, out StoreLocation result)) return result;
                return DefaultStoreLocation;
            }
            set
            {
                var stringValue = value.ToString();
                _storeLocation.Value = stringValue;
            }
        }

        public StoreName StoreName
        {
            get
            {
                if (Enum.TryParse(_storeName.Value, true, out StoreName result)) return result;
                return DefaultStoreName;
            }
            set
            {
                var stringValue = value.ToString();
                _storeName.Value = stringValue;
            }
        }

        internal override XNode AsXNode()
        {
            if (Node is XElement)
            {
                return Node;
            }

            var element = new XElement(ElementName,
                                       _storeLocation.AsXNode(),
                                       _storeName.AsXNode(),
                                       _findType.AsXNode(),
                                       _findValue.AsXNode());

            foreach (KeyValuePair<string, string> attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public override SettingBase Clone()
        {
            return new FromStorageItem(Name, FindValue, StoreLocation, StoreName, FindType);
        }

        public override X509Certificate Search()
        {
            var store = new X509Store(StoreName, StoreLocation);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection foundCertificates = store.Certificates.Find(FindType, FindValue, true);
                if (foundCertificates.Count == 0)
                {
                    throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                        Resources.Error_FromStorageCertificateNotFound,
                                                                        StoreLocation,
                                                                        StoreName,
                                                                        FindType,
                                                                        FindValue
                                                          ));
                }

                if (foundCertificates.Count > 1)
                {
                    throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                        Resources.Error_FromStorageCertificateNotFound,
                                                                        foundCertificates.Count,
                                                                        StoreLocation,
                                                                        StoreName,
                                                                        FindType,
                                                                        FindValue
                                                          ));
                }

                return foundCertificates[0];
            }
            finally
            {
                store.Close();
            }
        }

        private void ValidateItem()
        {
            if (!Enum.TryParse(_storeLocation?.Value, true, out StoreLocation _))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromStorageItemStoreLocationNotSupported,
                                                                    Origin.ConfigFilePath));
            }

            if (!Enum.TryParse(_storeName.Value, true, out StoreName _))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromStorageItemStoreNameNotSupported,
                                                                    Origin.ConfigFilePath));
            }

            if (!Enum.TryParse(_findType.Value, true, out X509FindType _))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromStorageItemFindTypeNotSupported,
                                                                    Origin.ConfigFilePath));
            }

            if (string.IsNullOrWhiteSpace(_findValue?.Value))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromStorageItemMustHaveFindValue,
                                                                    Origin.ConfigFilePath));
            }
        }
    }
}
