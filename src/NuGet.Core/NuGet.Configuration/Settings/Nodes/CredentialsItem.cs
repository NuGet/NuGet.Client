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
    public sealed class CredentialsItem : SettingsItem, IEquatable<CredentialsItem>
    {
        public override string Name { get; protected set; }
        protected override bool CanHaveChildren => true;
        protected override HashSet<string> AllowedAttributes => new HashSet<string>();
        public override bool IsEmpty() => Username == null && Password == null;
        public AddItem Username { get; private set; }
        public AddItem Password => IsPasswordClearText ? _clearTextPassword : _encryptedPassword;
        public AddItem ValidAuthenticationTypes { get; private set; }
        public bool IsPasswordClearText { get; private set; }
        private AddItem _clearTextPassword { get; set; }
        private AddItem _encryptedPassword { get; set; }

        internal CredentialsItem(XElement element, ISettingsFile origin)
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
                    Username = item;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.PasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    _encryptedPassword = item;
                    IsPasswordClearText = false;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.ClearTextPasswordToken, StringComparison.OrdinalIgnoreCase))
                {
                    _clearTextPassword = item;
                    IsPasswordClearText = true;
                }
                else if (string.Equals(item.Key, ConfigurationConstants.ValidAuthenticationTypesToken, StringComparison.OrdinalIgnoreCase))
                {
                    ValidAuthenticationTypes = item;
                }
            }

            if (Username == null || Password == null)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
            }
        }

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

            Username = new AddItem(ConfigurationConstants.UsernameToken, username);

            if (isPasswordClearText)
            {
                _clearTextPassword = new AddItem(ConfigurationConstants.ClearTextPasswordToken, password);
            }
            else
            {
                _encryptedPassword = new AddItem(ConfigurationConstants.PasswordToken, password);
            }

            IsPasswordClearText = isPasswordClearText;

            if (!string.IsNullOrEmpty(validAuthenticationTypes))
            {
                ValidAuthenticationTypes = new AddItem(ConfigurationConstants.ValidAuthenticationTypesToken, validAuthenticationTypes);
            }
        }

        public override SettingsItem Copy()
        {
            return new CredentialsItem(Name, Username.Value, Password.Value, IsPasswordClearText, ValidAuthenticationTypes?.Value);
        }

        public override bool Update(SettingsItem item, bool isBatchOperation = false)
        {
            if (base.Update(item, isBatchOperation: true) && item is CredentialsItem credentials)
            {
                Username = credentials.Username;
                IsPasswordClearText = credentials.IsPasswordClearText;

                if (credentials.IsPasswordClearText)
                {
                    _clearTextPassword = credentials.Password;
                    _encryptedPassword = null;
                }
                else
                {
                    _clearTextPassword = null;
                    _encryptedPassword = credentials.Password;
                }

                ValidAuthenticationTypes = credentials.ValidAuthenticationTypes;

                var element = Node as XElement;
                if (element != null)
                {
                    element.RemoveNodes();
                    XElementUtility.AddIndented(element, Username.AsXNode());
                    XElementUtility.AddIndented(element, Password.AsXNode());

                    if (ValidAuthenticationTypes != null)
                    {
                        XElementUtility.AddIndented(element, ValidAuthenticationTypes.AsXNode());
                    }
                }

                if (!isBatchOperation)
                {
                    Origin.Save();
                }

                return true;
            }

            return false;
        }

        public override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name,
                Username.AsXNode(),
                Password.AsXNode());

            if (ValidAuthenticationTypes != null)
            {
                element.Add(ValidAuthenticationTypes.AsXNode());
            }

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            Node = element;

            return Node;
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

            var validAutheticationTypesEquals = ValidAuthenticationTypes == null ?
                other.ValidAuthenticationTypes == null :
                ValidAuthenticationTypes.DeepEquals(other.ValidAuthenticationTypes);

            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && Username.DeepEquals(other.Username)
                && IsPasswordClearText == other.IsPasswordClearText
                && Password.DeepEquals(other.Password)
                && validAutheticationTypesEquals;
        }

        public override bool DeepEquals(SettingsNode other) => DeepEquals(other as CredentialsItem);
        public override bool Equals(SettingsNode other) =>  Equals(other as CredentialsItem);
        public override bool Equals(object other) => Equals(other as CredentialsItem);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
