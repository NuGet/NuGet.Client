// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class SettingText : SettingBase, IEquatable<SettingText>
    {
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(value));
                }

                _value = value;
            }
        }

        public SettingText(string value)
            : base()
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(value));
            }

            _value = value;
        }

        public bool Equals(SettingText other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public bool DeepEquals(SettingText other) => Equals(other);

        public override bool DeepEquals(SettingBase other) => Equals(other as SettingText);

        public override bool Equals(SettingBase other) => Equals(other as SettingText);

        public override bool Equals(object other) => Equals(other as SettingText);

        public override int GetHashCode() => Value.GetHashCode();

        internal override bool IsEmpty() => string.IsNullOrEmpty(Value);

        internal override SettingBase Clone()
        {
            var newSetting = new SettingText(Value);

            if (Origin != null)
            {
                newSetting.SetOrigin(Origin);
            }

            return newSetting;
        }

        internal SettingText(XText text, SettingsFile origin)
            : base(text, origin)
        {
            var value = text.Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
            }

            Value = value;
        }

        internal override XNode AsXNode()
        {
            if (Node is XText xText)
            {
                return xText;
            }

            return new XText(Value);
        }
    }
}
