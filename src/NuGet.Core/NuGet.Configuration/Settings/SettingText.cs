// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class SettingText : SettingBase, IEquatable<SettingText>
    {
        public string Value { get; set; }

        public SettingText(string value)
            : base()
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
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
            return new SettingText(Value) { Origin = Origin };
        }

        internal SettingText(XText text, SettingsFile origin)
            : base(text, origin)
        {
            Value = text.Value;
        }

        internal override XNode AsXNode()
        {
            if (Node != null && Node is XText xText)
            {
                return xText;
            }

            return new XText(Value);
        }

        internal bool Update(SettingText setting)
        {
            if (Origin != null && Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            if (Node != null && Node is XText xText)
            {
                xText.Value = setting.Value;
                Origin.IsDirty = true;

                return true;
            }

            return false;
        }
    }
}
