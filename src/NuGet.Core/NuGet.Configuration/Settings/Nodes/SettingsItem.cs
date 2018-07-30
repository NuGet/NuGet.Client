// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsItem : SettingsElement
    {
        protected virtual bool CanHaveChildren => false;

        public SettingsItem()
            : base()
        {
        }

        internal SettingsItem(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
            if (!CanHaveChildren && element.HasElements)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.ShowError_CannotHaveChildren, element.Name.LocalName, origin.ConfigFilePath));
            }
        }

        public abstract SettingsItem Copy();

        public virtual bool Update(SettingsItem item)
        {
            if (Equals(item) && !Origin.IsMachineWide)
            {
                Attributes.Clear();
                foreach (var attr in item.Attributes)
                {
                    Attributes.Add(attr);

                    if (!IsAbstract)
                    {
                        (Node as XElement)?.SetAttributeValue(attr.Key, attr.Value);
                    }
                }

                Origin.IsDirty = true;

                return true;
            }

            return false;
        }
    }
}
