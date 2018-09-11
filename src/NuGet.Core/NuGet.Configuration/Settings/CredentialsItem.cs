// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    /// <summary>
    /// A CredentialsItem has a name and it can have between 2 or 3 children:
    ///     - [Required] Username (AddItem)
    ///     - [Required] Either Password or ClearTextPassword (AddItem)
    ///     - [Optional] ValidAuthenticationTypes (AddItem)
    /// </summary>
    public sealed class CredentialsItem : SettingItem, IEquatable<CredentialsItem>
    {
        public override string Name { get; protected set; }

        public string Username
        {
            get => _username.Value;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, Resources.PropertyCannotBeNull, nameof(Username)));
                }

                _username.Value = value;
            }
        }

        public bool IsPasswordClearText { get; private set; }

        public string Password => _password.Value;

        public void UpdatePassword(string password, bool isPasswordClearText = true)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (IsPasswordClearText && !isPasswordClearText)
            {
                _password.UpdateAttribute(ConfigurationConstants.KeyAttribute, ConfigurationConstants.PasswordToken);
            }
            else if (!IsPasswordClearText && isPasswordClearText)
            {
                _password.UpdateAttribute(ConfigurationConstants.KeyAttribute, ConfigurationConstants.ClearTextPasswordToken);
            }

            IsPasswordClearText = isPasswordClearText;

            if (!string.Equals(Password, password, StringComparison.Ordinal))
            {
                _password.Value = password;
            }
        }

        public string ValidAuthenticationTypes
        {
            get => _validAuthenticationTypes?.Value;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _validAuthenticationTypes = null;
                }
                else
                {
                    if (_validAuthenticationTypes == null)
                    {
                        _validAuthenticationTypes = new AddItem(ConfigurationConstants.ValidAuthenticationTypesToken, value)
                        {
                            Origin = Origin
                        };
                    }
                    else
                    {
                        _validAuthenticationTypes.Value = value;
                    }
                }
            }
        }

        protected override bool CanHaveChildren => true;

        protected override HashSet<string> AllowedAttributes => new HashSet<string>();

        internal override bool IsEmpty() => string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password);

        internal AddItem _username { get; set; }

        internal AddItem _password { get; set; }

        internal AddItem _validAuthenticationTypes { get; set; }

        public CredentialsItem(string name, string username, string password, bool isPasswordClearText, string validAuthenticationTypes)
           : base()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            Name = name;

            _username = new AddItem(ConfigurationConstants.UsernameToken, username);

            var passwordKey = isPasswordClearText ? ConfigurationConstants.ClearTextPasswordToken : ConfigurationConstants.PasswordToken;
            _password = new AddItem(passwordKey, password);

            IsPasswordClearText = isPasswordClearText;

            if (!string.IsNullOrEmpty(validAuthenticationTypes))
            {
                _validAuthenticationTypes = new AddItem(ConfigurationConstants.ValidAuthenticationTypesToken, validAuthenticationTypes);
            }
        }

        internal CredentialsItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;

            var elementDescendants = element.Elements();
            var countOfDescendants = elementDescendants.Count();

            if (countOfDescendants != 2 && countOfDescendants != 3)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
            }

            var parsedItems = elementDescendants.Select(e => SettingFactory.Parse(e, origin) as AddItem).Where(i => i != null);

            foreach (var item in parsedItems)
            {
                if (string.Equals(item.Key, ConfigurationConstants.UsernameToken, StringComparison.OrdinalIgnoreCase))
                {
                    _username = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    _password = item;
                    IsPasswordClearText = false;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.ClearTextPasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    _password = item;
                    IsPasswordClearText = true;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.ValidAuthenticationTypesToken, StringComparison.OrdinalIgnoreCase))
                {
                    _validAuthenticationTypes = item;
                }
            }

            if (_username == null || _password == null)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
            }
        }

        internal override SettingBase Clone()
        {
            return new CredentialsItem(Name, Username, Password, IsPasswordClearText, ValidAuthenticationTypes) { Origin = Origin };
        }

        internal override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name,
                _username.AsXNode(),
                _password.AsXNode());

            if (_validAuthenticationTypes != null)
            {
                element.Add(_validAuthenticationTypes.AsXNode());
            }

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public bool Equals(CredentialsItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public bool DeepEquals(CredentialsItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            var validAutheticationTypesEquals = string.IsNullOrEmpty(ValidAuthenticationTypes) ?
                string.IsNullOrEmpty(other.ValidAuthenticationTypes) :
                string.Equals(ValidAuthenticationTypes, other.ValidAuthenticationTypes, StringComparison.Ordinal);

            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Username, other.Username, StringComparison.Ordinal)
                && IsPasswordClearText == other.IsPasswordClearText
                && string.Equals(Password, other.Password, StringComparison.Ordinal)
                && validAutheticationTypesEquals;
        }

        public override bool DeepEquals(SettingBase other) => DeepEquals(other as CredentialsItem);
        public override bool Equals(SettingBase other) => Equals(other as CredentialsItem);
        public override bool Equals(object other) => Equals(other as CredentialsItem);
        public override int GetHashCode() => Name.GetHashCode();

        /// <remarks>
        /// This method is internal because it updates directly the xElement behind this abstraction.
        /// It should only be called whenever the underlaying config file is intended to be changed.
        /// To persist changes to disk one must save the corresponding setting files
        /// </remarks>
        internal override void Update(SettingItem other)
        {
            base.Update(other);

            var credentials = other as CredentialsItem;

            if (!string.Equals(Username, credentials.Username, StringComparison.Ordinal))
            {
                _username.Update(credentials._username);
            }

            if (!string.Equals(Password, credentials.Password, StringComparison.Ordinal) ||
                IsPasswordClearText != credentials.IsPasswordClearText)
            {
                _password.Update(credentials._password);
                IsPasswordClearText = credentials.IsPasswordClearText;
            }

            if (!string.Equals(ValidAuthenticationTypes, credentials.ValidAuthenticationTypes, StringComparison.Ordinal))
            {
                if (_validAuthenticationTypes == null)
                {
                    _validAuthenticationTypes = new AddItem(ConfigurationConstants.ValidAuthenticationTypesToken, credentials.ValidAuthenticationTypes)
                    {
                        Origin = Origin,
                        Node = _validAuthenticationTypes.AsXNode()
                    };

                    var element = Node as XElement;
                    if (element != null)
                    {
                        XElementUtility.AddIndented(element, _validAuthenticationTypes.Node);
                    }
                }
                else if (credentials.ValidAuthenticationTypes == null)
                {
                    XElementUtility.RemoveIndented(_validAuthenticationTypes.Node);
                }
                else
                {
                    _validAuthenticationTypes.Update(credentials._validAuthenticationTypes);
                }
            }
        }
    }
}
