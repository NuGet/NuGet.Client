// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class SettingsTextContent : SettingsNode, IEquatable<SettingsTextContent>
    {
        public string Value { get; private set; }

        public override bool IsEmpty() => string.IsNullOrEmpty(Value);

        public SettingsTextContent(string value)
            : base()
        {
            Value = value;
        }

        internal SettingsTextContent(XText text, ISettingsFile origin)
            : base(text, origin)
        {
            Value = text.Value;
        }

        public bool Update(string newValue, bool isBatchOperation = false)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                throw new ArgumentNullException(nameof(newValue));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (Node != null && Node is XText xText)
            {
                xText.Value = newValue;

                if (!isBatchOperation)
                {
                    Origin.Save();
                }
            }

            Value = newValue;

            return true;
        }

        public override XNode AsXNode()
        {
            if (Node != null && Node is XText xText)
            {
                return xText;
            }

            Node = new XText(Value);

            return Node;
        }

        public bool Equals(SettingsTextContent other)
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

        public bool DeepEquals(SettingsTextContent other) => Equals(other);

        public override bool DeepEquals(SettingsNode other) => Equals(other as SettingsTextContent);

        public override bool Equals(SettingsNode other) => Equals(other as SettingsTextContent);

        public override bool Equals(object other) => Equals(other as SettingsTextContent);

        public override int GetHashCode() => Value.GetHashCode();
    }
}
