// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class ClearItem : SettingsItem, IEquatable<ClearItem>
    {
        public override string Name => ConfigurationConstants.Clear;

        public override bool IsEmpty() => false;

        protected override HashSet<string> AllowedAttributes => new HashSet<string>();

        public ClearItem()
        {
        }

        internal ClearItem(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
        }

        public override SettingsItem Copy()
        {
            return new ClearItem();
        }

        public override bool Update(SettingsItem item) => false;
        public bool Equals(ClearItem other) => other != null;
        public bool DeepEquals(ClearItem other) =>  Equals(other);
        public override bool DeepEquals(SettingsNode other) => Equals(other as ClearItem);
        public override bool Equals(SettingsNode other) => Equals(other as ClearItem);
        public override bool Equals(object other) => Equals(other as ClearItem);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
