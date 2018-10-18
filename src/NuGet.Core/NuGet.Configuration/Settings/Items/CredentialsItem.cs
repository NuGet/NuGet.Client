// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    public sealed class CredentialsItem : SettingItem
    {
        public override string ElementName { get; protected set; }

        public string Username
        {
            get => _username.Value;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.PropertyCannotBeNullOrEmpty, nameof(Username)));
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
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(password));
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

        protected override bool CanHaveChildren => true;

        internal override bool IsEmpty() => string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password);

        internal readonly AddItem _username;

        internal readonly AddItem _password;

        public CredentialsItem(string name, string username, string password, bool isPasswordClearText)
           : base()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(name));
            }

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(username));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(password));
            }

            ElementName = name;

            _username = new AddItem(ConfigurationConstants.UsernameToken, username);

            var passwordKey = isPasswordClearText ? ConfigurationConstants.ClearTextPasswordToken : ConfigurationConstants.PasswordToken;
            _password = new AddItem(passwordKey, password);

            IsPasswordClearText = isPasswordClearText;
        }

        internal CredentialsItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            ElementName = element.Name.LocalName;

            var elementDescendants = element.Elements();
            var countOfDescendants = elementDescendants.Count();

            var parsedItems = elementDescendants.Select(e => SettingFactory.Parse(e, origin) as AddItem).Where(i => i != null);

            foreach (var item in parsedItems)
            {
                if (string.Equals(item.Key, ConfigurationConstants.UsernameToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_username == null)
                    {
                        _username = item;
                    }
                }
                else if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_password == null)
                    {
                        _password = item;
                        IsPasswordClearText = false;
                    }
                }
                else if (string.Equals(item.Key, ConfigurationConstants.ClearTextPasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    if (_password == null)
                    {
                        _password = item;
                        IsPasswordClearText = true;
                    }
                }
            }

            if (_username == null || _password == null)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, Resources.CredentialsItemMustHaveUsernamePassword, origin.ConfigFilePath));
            }
        }

        internal override SettingBase Clone()
        {
            var newSetting = new CredentialsItem(ElementName, Username, Password, IsPasswordClearText);

            if (Origin != null)
            {
                newSetting.SetOrigin(Origin);
            }

            foreach(var attr in Attributes)
            {
                newSetting.AddAttribute(attr.Key, attr.Value);
            }

            return newSetting;
        }

        internal override XNode AsXNode()
        {
            if (Node is XElement)
            {
                return Node;
            }

            var element = new XElement(ElementName,
                _username.AsXNode(),
                _password.AsXNode());

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public override bool Equals(object other)
        {
            var item = other as CredentialsItem;

            if (item == null)
            {
                return false;
            }

            if (ReferenceEquals(this, item))
            {
                return true;
            }

            return string.Equals(ElementName, item.ElementName, StringComparison.Ordinal);
        }

        public override int GetHashCode() => ElementName.GetHashCode();

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
        }

        internal override void SetOrigin(SettingsFile origin)
        {
            base.SetOrigin(origin);

            _username.SetOrigin(origin);
            _password.SetOrigin(origin);
        }

        internal override void RemoveFromSettings()
        {
            base.RemoveFromSettings();

            _username.RemoveFromSettings();
            _password.RemoveFromSettings();
        }
    }
}
