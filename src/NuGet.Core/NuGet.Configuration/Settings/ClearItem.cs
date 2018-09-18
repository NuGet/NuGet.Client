// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class ClearItem : SettingItem, IEquatable<ClearItem>
    {
        public override string ElementName => ConfigurationConstants.Clear;

        internal override bool IsEmpty() => false;

        public ClearItem()
        {
        }

        internal ClearItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        internal override SettingBase Clone() => new ClearItem();
        public bool Equals(ClearItem other) => other != null;
        public bool DeepEquals(ClearItem other) =>  Equals(other);
        public override bool DeepEquals(SettingBase other) => Equals(other as ClearItem);
        public override bool Equals(SettingBase other) => Equals(other as ClearItem);
        public override bool Equals(object other) => Equals(other as ClearItem);
        public override int GetHashCode() => ElementName.GetHashCode();
    }
}
