using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    ///     A FromCertItem have 2 children and body text:
    ///     - [Required] Hex certificate body or Path (AddItem)
    ///     - [Optional] Password (AddItem)
    /// </summary>
    public sealed class FromCertItem : CertificateSearchItem
    {
        #region Static members

        private static byte[] ReadStream(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        #endregion

        private readonly AddItem _password;
        private readonly AddItem _path;

        #region Constructors

        internal FromCertItem(string filePath = null, string base64Certificate = null, string password = null, SettingsFile origin = null)
        {
            ElementName = ConfigurationConstants.FromCert;
            SetOrigin(origin);

            _path = new AddItem(ConfigurationConstants.PathToken, filePath);
            _password = new AddItem(ConfigurationConstants.PasswordToken, password);
            Base64Certificate = base64Certificate;

            ValidateItem();
        }

        internal FromCertItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            ElementName = ConfigurationConstants.FromCert;

            IEnumerable<AddItem> parsedItems = element.Elements()
                                                      .Select(e => SettingFactory.Parse(e, origin) as AddItem)
                                                      .Where(i => i != null);

            foreach (AddItem item in parsedItems)
            {
                if (string.Equals(item.Key, ConfigurationConstants.PathToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_path != null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                            Resources.UserSettings_UnableToParseConfigFile,
                                                                            Resources.Error_MoreThanOnePath,
                                                                            origin.ConfigFilePath));
                    }

                    _path = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
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

            if (_path == null) _path = new AddItem(ConfigurationConstants.PathToken, null);
            if (_password == null) _password = new AddItem(ConfigurationConstants.PasswordToken, null);

            Base64Certificate = element.Value.Trim(' ', '\n', '\r');

            ValidateItem();
        }

        #endregion

        #region Properties

        public string Base64Certificate { get; set; }

        public string Password
        {
            get => _password.Value;
            set => _password.Value = value;
        }

        public string Path
        {
            get => _path.Value;
            set => _path.Value = value;
        }

        #endregion

        #region Override members

        public override SettingBase Clone()
        {
            return new FromCertItem(Path, Base64Certificate, Password, Origin);
        }

        public override X509Certificate Search()
        {
            byte[] certificateData;
            if (string.IsNullOrWhiteSpace(Base64Certificate))
            {
                //Read certificate from file
                using (FileStream stream = File.OpenRead(FindAbsoluteFilePath()))
                {
                    certificateData = ReadStream(stream);
                }
            }
            else
            {
                //Transform base64 certificate to bytes
                certificateData = Encoding.UTF8.GetBytes(Base64Certificate);
            }

            //If password not set try to create certificate from file stream
            if (string.IsNullOrWhiteSpace(Password)) return new X509Certificate2(certificateData);

            //If password is set decrypt it first and try to create certificate from file stream and decrypted password
            var decryptedPassword = EncryptionUtility.DecryptString(Password);
            return new X509Certificate2(certificateData, decryptedPassword);

        }

        #endregion

        #region Members

        private string FindAbsoluteFilePath()
        {
            var originalValue = _path.Value;
            var expectedFiles = new List<string>
            {
                originalValue //Means absolute path
            };

            if (PathValidator.IsValidRelativePath(originalValue))
            {
                if (Origin != null)
                {
                    //Relative to config file path
                    expectedFiles.Add(PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Origin.DirectoryPath), originalValue));
                }

                //Relative to current directory file path
                expectedFiles.Add(PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()), originalValue));
            }

            return expectedFiles.FirstOrDefault(File.Exists);
        }

        private void ValidateItem()
        {
            if (string.IsNullOrWhiteSpace(Base64Certificate) && string.IsNullOrWhiteSpace(Path))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromCertItemPathFileAndBase64NotSet,
                                                                    Origin.ConfigFilePath));
            }

            if (!string.IsNullOrWhiteSpace(Base64Certificate) && !string.IsNullOrWhiteSpace(Path))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Resources.UserSettings_UnableToParseConfigFile,
                                                                    Resources.FromCertItemPathFileAndBase64Set,
                                                                    Origin.ConfigFilePath));
            }

            if (string.IsNullOrWhiteSpace(Base64Certificate))
            {
                var filePath = FindAbsoluteFilePath();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture,
                                                                        Resources.UserSettings_UnableToParseConfigFile,
                                                                        Resources.FromCertItemPathFileNotExist,
                                                                        Origin.ConfigFilePath));
                }
            }
        }

        #endregion
    }
}
