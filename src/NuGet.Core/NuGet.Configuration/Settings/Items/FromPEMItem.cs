using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    /// <summary>
    ///     A FromPEMItem have one children and body text:
    ///     - [Required] Hex certificate body in PEM format
    ///     - [Optional] Password (AddItem)
    /// </summary>
    public sealed class FromPEMItem : CertificateSearchItem
    {
        private readonly AddItem _password;

        internal FromPEMItem(string base64Certificate = null, string password = null, SettingsFile origin = null)
        {
            ElementName = ConfigurationConstants.FromPEM;
            SetOrigin(origin);

            _password = new AddItem(ConfigurationConstants.PasswordToken, password);
            Base64Certificate = base64Certificate;

            ValidateItem();
        }

        internal FromPEMItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            ElementName = ConfigurationConstants.FromPEM;

            IEnumerable<AddItem> parsedItems = element.Elements()
                                                      .Select(e => SettingFactory.Parse(e, origin) as AddItem)
                                                      .Where(i => i != null);

            foreach (AddItem item in parsedItems)
            {
                if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_password != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOneCertificatePassword,
                                                                            origin.ConfigFilePath));
                    }

                    _password = item;
                }
            }

            if (_password == null) _password = new AddItem(ConfigurationConstants.PasswordToken, null);

            Base64Certificate = element.Value.Trim(' ', '\n', '\r');

            ValidateItem();
        }

        public string Base64Certificate { get; set; }

        public string Password
        {
            get => _password.Value;
            set => _password.Value = value;
        }

        public override SettingBase Clone()
        {
            return new FromPEMItem(Base64Certificate, Password, Origin);
        }

        public override X509Certificate Search()
        {
            //Transform base64 certificate to bytes
            byte[] certificateData = Encoding.UTF8.GetBytes(Base64Certificate);

            //If password not set try to create certificate from file stream
            if (string.IsNullOrWhiteSpace(Password)) return new X509Certificate2(certificateData);

            //If password is set decrypt it first and try to create certificate from file stream and decrypted password
            var decryptedPassword = EncryptionUtility.DecryptString(Password);
            return new X509Certificate2(certificateData, decryptedPassword);
        }

        private void ValidateItem()
        {
            if (string.IsNullOrWhiteSpace(Base64Certificate))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromPEMItemBase64Set,
                                                                    Origin.ConfigFilePath));
            }
        }
    }
}
