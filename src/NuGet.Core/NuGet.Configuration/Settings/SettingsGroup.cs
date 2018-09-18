// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsGroup<T> : SettingElement, ISettingsGroup where T : SettingElement
    {
        // Until HashSet<T>TryGetValue(...) is available, we will use a dictionary to enable efficient element retrieval.
        protected Dictionary<T, T> ChildrenSet { get; private set; }

        protected virtual bool CanBeCleared => true;

        protected SettingsGroup()
            : base()
        {
            ChildrenSet = new Dictionary<T, T>();
        }

        protected SettingsGroup(IReadOnlyDictionary<string, string> attributes, IEnumerable<T> children)
            : base(attributes)
        {
            if (children == null)
            {
                ChildrenSet = new Dictionary<T, T>();
            }
            else
            {
                ChildrenSet = new Dictionary<T, T>(children.ToDictionary(c => c, c => c));
            }
        }

        internal override bool IsEmpty() => !ChildrenSet.Any() || ChildrenSet.All(c => c.Value.IsEmpty());

        internal SettingsGroup(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            ElementName = element.Name.LocalName;

            ChildrenSet = SettingFactory.ParseChildren<T>(element, origin, CanBeCleared).ToDictionary(c => c, c => c);

            foreach (var child in ChildrenSet)
            {
                child.Value.Parent = this;
            }
        }

        internal override XNode AsXNode()
        {
            if (Node is XElement)
            {
                return Node;
            }

            var element = new XElement(ElementName, ChildrenSet.Select(c => c.Value.AsXNode()));

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        internal override void SetOrigin(SettingsFile origin)
        {
            base.SetOrigin(origin);

            foreach (var child in ChildrenSet)
            {
                child.Value.SetOrigin(origin);
            }
        }

        internal override void RemoveFromSettings()
        {
            base.RemoveFromSettings();

            foreach (var child in ChildrenSet)
            {
                child.Value.RemoveFromSettings();
            }
        }

        internal virtual bool Add(T setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            if (!ChildrenSet.ContainsKey(setting) && !setting.IsEmpty())
            {
                ChildrenSet.Add(setting, setting);

                setting.SetOrigin(Origin);
                setting.SetNode(setting.AsXNode());

                XElementUtility.AddIndented(Node as XElement, setting.Node);
                Origin.IsDirty = true;

                setting.Parent = this;

                return true;
            }

            return false;
        }

        internal virtual void Remove(T setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            if (ChildrenSet.TryGetValue(setting, out var currentSetting) && ChildrenSet.Remove(currentSetting))
            {
                currentSetting.RemoveFromSettings();

                if (Parent != null && IsEmpty())
                {
                    Parent.Remove(this);
                }
            }
        }

        void ISettingsGroup.Remove(SettingElement setting)
        {
            Remove(setting as T);
        }
    }
}
